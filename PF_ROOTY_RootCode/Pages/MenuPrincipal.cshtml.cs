using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PF_ROOTY_RootCode.Pages
{
    public class MenuPrincipalModel : PageModel
    {
        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=localhost\\SQLEXPRESS;Database=ReciclajeDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // ------------------ PROPIEDADES DE LA VISTA ------------------
        public string NombreCompleto { get; set; } = string.Empty;
        public string Inicial { get; set; } = "R";
        public string NivelActual { get; set; } = "Aliado sin nivel";
        public int PuntosDisponibles { get; set; }
        public decimal TotalKgReciclado { get; set; }
        public decimal TotalCo2Evitado { get; set; }
        public int InsigniasObtenidasCount { get; set; }
        public List<InsigniaDashboard> ListaInsignias { get; set; } = new();

        // ------------------ CARGA DEL DASHBOARD (SOLO LECTURA) ------------------
        public IActionResult OnGet()
        {
            // Evita que el navegador guarde caché (bloquea volver con la flecha Atrás tras Logout)
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            int? usuarioId = HttpContext.Session.GetInt32("UsuarioId");
            string? nombreSesion = HttpContext.Session.GetString("UsuarioNombre");

            if (usuarioId == null || string.IsNullOrEmpty(nombreSesion))
            {
                return RedirectToPage("/Login");
            }

            using (SqlConnection con = new SqlConnection(conexion))
            {
                con.Open();

                // 0. Datos de perfil del usuario (nombre completo y saldo de puntos)
                bool usuarioExiste = false;
                string sqlUsuario = "SELECT nombre, puntos_acumulados FROM USUARIOS WHERE usuario_id = @uid";
                using (SqlCommand cmd = new SqlCommand(sqlUsuario, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            usuarioExiste = true;
                            NombreCompleto = dr.GetString(0);
                            PuntosDisponibles = dr.GetInt32(1);
                        }
                    }
                }

                // Si el usuario ya no existe en la base de datos, se cierra la sesión
                if (!usuarioExiste)
                {
                    HttpContext.Session.Clear();
                    return RedirectToPage("/Login");
                }

                Inicial = string.IsNullOrWhiteSpace(NombreCompleto)
                    ? "R"
                    : NombreCompleto.Trim().Substring(0, 1).ToUpper();

                // 1. Kilogramos totales reciclados y CO2 evitado
                string sqlMetricas = @"
                    SELECT 
                        ISNULL(SUM(r.peso_kg), 0) AS TotalKg,
                        ISNULL(SUM(r.peso_kg * m.co2_por_kg), 0) AS TotalCo2
                    FROM REGISTROS_RECICLAJE r
                    INNER JOIN MATERIALES m ON r.material_id = m.material_id
                    WHERE r.usuario_id = @uid";

                using (SqlCommand cmd = new SqlCommand(sqlMetricas, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            TotalKgReciclado = dr.GetDecimal(0);
                            TotalCo2Evitado = dr.GetDecimal(1);
                        }
                    }
                }

                // 2. Las 10 insignias ordenadas por meta de CO2 para la línea del tiempo en zig-zag
                string sqlTimeline = @"
                    SELECT i.insignia_id, i.nombre, i.co2_requerido, i.puntos_recompensa,
                           CASE WHEN ui.usuario_id IS NOT NULL THEN 1 ELSE 0 END AS Desbloqueada
                    FROM INSIGNIAS i
                    LEFT JOIN USUARIOS_INSIGNIAS ui 
                           ON i.insignia_id = ui.insignia_id AND ui.usuario_id = @uid
                    ORDER BY i.co2_requerido ASC";

                using (SqlCommand cmd = new SqlCommand(sqlTimeline, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            bool desbloqueada = dr.GetInt32(4) == 1;
                            if (desbloqueada) InsigniasObtenidasCount++;

                            ListaInsignias.Add(new InsigniaDashboard
                            {
                                Nombre = dr.GetString(1),
                                Co2Requerido = dr.GetDecimal(2),
                                PuntosRecompensa = dr.GetInt32(3),
                                Desbloqueada = desbloqueada
                            });
                        }
                    }
                }

                // 3. Rango más alto desbloqueado (si no tiene, queda "Aliado sin nivel")
                var ultimaDesbloqueada = ListaInsignias.LastOrDefault(x => x.Desbloqueada);
                if (ultimaDesbloqueada != null)
                {
                    NivelActual = ultimaDesbloqueada.Nombre;
                }
            }

            return Page();
        }
    }

    // ------------------ CLASE DE APOYO ------------------
    public class InsigniaDashboard
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal Co2Requerido { get; set; }
        public int PuntosRecompensa { get; set; }
        public bool Desbloqueada { get; set; }
    }
}