using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace PF_ROOTY_RootCode.Pages
{
    public class PerfilModel : PageModel
    {
        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=db82448.databaseasp.net;Database=db82448;User Id=db82448;Password=tX@6!x5Tp8B?;Encrypt=False;MultipleActiveResultSets=True;";

        // ------------------ PROPIEDADES DE LA VISTA ------------------
        public string NombreCompleto { get; set; } = string.Empty;
        public string Inicial { get; set; } = "R";
        public string NivelActual { get; set; } = "Aliado sin nivel";
        public int PuntosDisponibles { get; set; }
        public decimal TotalKgReciclado { get; set; }
        public decimal TotalCo2Evitado { get; set; }
        public int InsigniasObtenidasCount { get; set; }
        public int InsigniasTotal { get; set; }
        public List<AporteHistorial> HistorialAportes { get; set; } = new();
        public List<CanjeHistorial> HistorialCanjes { get; set; } = new();

        // ------------------ CARGA DE LA PAGINA (SOLO LECTURA) ------------------
        public IActionResult OnGet()
        {
            // Evita que el navegador guarde caché (bloquea volver con la flecha Atrás tras Logout)
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            int? usuarioId = HttpContext.Session.GetInt32("UsuarioId");
            if (usuarioId == null)
            {
                return RedirectToPage("/Login");
            }

            using (SqlConnection con = new SqlConnection(conexion))
            {
                con.Open();

                // 0. Datos de perfil del usuario
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

                if (!usuarioExiste)
                {
                    HttpContext.Session.Clear();
                    return RedirectToPage("/Login");
                }

                Inicial = string.IsNullOrWhiteSpace(NombreCompleto)
                    ? "R"
                    : NombreCompleto.Trim().Substring(0, 1).ToUpper();

                // 1. Kilogramos totales y CO2 evitado
                string sqlMetricas = @"
                    SELECT
                        ISNULL(SUM(r.peso_kg), 0),
                        ISNULL(SUM(r.peso_kg * m.co2_por_kg), 0)
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

                // 2. Niveles desbloqueados y total de niveles
                string sqlConteo = @"
                    SELECT
                        (SELECT COUNT(*) FROM INSIGNIAS),
                        (SELECT COUNT(*) FROM USUARIOS_INSIGNIAS WHERE usuario_id = @uid)";
                using (SqlCommand cmd = new SqlCommand(sqlConteo, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            InsigniasTotal = dr.GetInt32(0);
                            InsigniasObtenidasCount = dr.GetInt32(1);
                        }
                    }
                }

                // 3. Rango actual (la insignia desbloqueada más alta)
                string sqlNivel = @"
                    SELECT TOP 1 i.nombre
                    FROM INSIGNIAS i
                    INNER JOIN USUARIOS_INSIGNIAS ui ON i.insignia_id = ui.insignia_id
                    WHERE ui.usuario_id = @uid
                    ORDER BY i.co2_requerido DESC";
                using (SqlCommand cmd = new SqlCommand(sqlNivel, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    object? res = cmd.ExecuteScalar();
                    if (res != null) NivelActual = res.ToString()!;
                }

                // 4. Historial de aportaciones (las 50 más recientes)
                string sqlAportes = @"
                    SELECT TOP 50 r.fecha, p.nombre_punto, m.nombre_material,
                           r.peso_kg, r.puntos_otorgados, (r.peso_kg * m.co2_por_kg)
                    FROM REGISTROS_RECICLAJE r
                    INNER JOIN PUNTOS_ACOPIO p ON r.punto_id = p.punto_id
                    INNER JOIN MATERIALES m ON r.material_id = m.material_id
                    WHERE r.usuario_id = @uid
                    ORDER BY r.fecha DESC, r.registro_id DESC";
                using (SqlCommand cmd = new SqlCommand(sqlAportes, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            HistorialAportes.Add(new AporteHistorial
                            {
                                Fecha = dr.GetDateTime(0),
                                Centro = dr.GetString(1),
                                Material = dr.GetString(2),
                                PesoKg = dr.GetDecimal(3),
                                Puntos = dr.GetInt32(4),
                                Co2 = dr.GetDecimal(5)
                            });
                        }
                    }
                }

                // 5. Historial de canjes (los 50 más recientes)
                string sqlCanjes = @"
                    SELECT TOP 50 c.fecha, r.titulo, r.costo_puntos, c.codigo_canje, c.estado
                    FROM CANJES c
                    INNER JOIN RECOMPENSAS r ON c.recompensa_id = r.recompensa_id
                    WHERE c.usuario_id = @uid
                    ORDER BY c.fecha DESC, c.canje_id DESC";
                using (SqlCommand cmd = new SqlCommand(sqlCanjes, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            HistorialCanjes.Add(new CanjeHistorial
                            {
                                Fecha = dr.GetDateTime(0),
                                Recompensa = dr.GetString(1),
                                CostoPuntos = dr.GetInt32(2),
                                Codigo = dr.GetString(3),
                                Estado = dr.GetString(4)
                            });
                        }
                    }
                }
            }

            return Page();
        }
    }

    // ------------------ CLASES DE APOYO ------------------
    public class AporteHistorial
    {
        public DateTime Fecha { get; set; }
        public string Centro { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public decimal PesoKg { get; set; }
        public int Puntos { get; set; }
        public decimal Co2 { get; set; }
    }

    public class CanjeHistorial
    {
        public DateTime Fecha { get; set; }
        public string Recompensa { get; set; } = string.Empty;
        public int CostoPuntos { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}