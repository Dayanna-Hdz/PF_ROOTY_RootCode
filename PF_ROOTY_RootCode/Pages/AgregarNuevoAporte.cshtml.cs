using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PF_ROOTY_RootCode.Pages
{
    public class AgregarNuevoAporteModel : PageModel
    {
        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=db82448.databaseasp.net;Database=db82448;User Id=db82448;Password=tX@6!x5Tp8B?;Encrypt=False;MultipleActiveResultSets=True;";

        // ------------------ LIMITES DE VALIDACION ------------------
        private const decimal PesoMinimoKg = 0.1m;
        private const decimal PesoMaximoKg = 500m;

        // Folio: letras mayusculas, numeros y guiones, de 6 a 20 caracteres (ej. ROOTY-4821-9F3A)
        private static readonly Regex PatronFolio = new Regex(@"^[A-Z0-9\-]{6,20}$", RegexOptions.Compiled);

        // ------------------ PROPIEDADES DE LA VISTA ------------------
        public string PrimerNombre { get; set; } = "Reciclador";
        public List<PuntoAcopioItem> ListaPuntos { get; set; } = new();
        public List<MaterialItem> ListaMateriales { get; set; } = new();
        public string PuntosJson { get; set; } = "[]";
        public string MaterialesJson { get; set; } = "[]";

        public string MensajeError { get; set; } = string.Empty;
        public string MensajeExito { get; set; } = string.Empty;
        public int PuntosGanadosUltimo { get; set; }
        public decimal Co2GanadoUltimo { get; set; }
        public string NombrePuntoComprobante { get; set; } = string.Empty;

        // ------------------ INSIGNIAS DESBLOQUEADAS AL GUARDAR ------------------
        public List<string> InsigniasNuevas { get; set; } = new();
        public int PuntosExtraInsignias { get; set; }

        // ------------------ PROPIEDADES DEL FORMULARIO ------------------
        [BindProperty]
        public int PuntoId { get; set; }

        [BindProperty]
        public int MaterialId { get; set; }

        [BindProperty]
        public decimal PesoKg { get; set; }

        [BindProperty]
        public DateTime FechaEntrega { get; set; } = DateTime.Today;

        // Folio del comprobante que entrega el centro de acopio
        [BindProperty]
        public string Folio { get; set; } = string.Empty;

        // ------------------ CARGA DE LA PAGINA ------------------
        public IActionResult OnGet()
        {
            // Evita que el navegador guarde caché (bloquea volver con la flecha Atrás tras Logout)
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            int? usuarioId = HttpContext.Session.GetInt32("UsuarioId");
            string? nombreCompleto = HttpContext.Session.GetString("UsuarioNombre");

            if (usuarioId == null || string.IsNullOrEmpty(nombreCompleto))
            {
                return RedirectToPage("/Login");
            }

            PrimerNombre = nombreCompleto.Trim().Split(' ')[0];
            FechaEntrega = DateTime.Today;
            CargarCatalogos();
            return Page();
        }

        // ------------------ REGISTRO DEL APORTE ------------------
        public IActionResult OnPost()
        {
            // Evita que el navegador guarde caché en la respuesta POST
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            int? usuarioId = HttpContext.Session.GetInt32("UsuarioId");
            string? nombreCompleto = HttpContext.Session.GetString("UsuarioNombre");

            if (usuarioId == null || string.IsNullOrEmpty(nombreCompleto))
            {
                return RedirectToPage("/Login");
            }

            PrimerNombre = nombreCompleto.Trim().Split(' ')[0];
            CargarCatalogos();

            // --------------------- VALIDACIONES DE SERVIDOR ---------------------
            var puntoElegido = ListaPuntos.FirstOrDefault(p => p.PuntoId == PuntoId);
            if (puntoElegido == null)
            {
                MensajeError = "Por favor selecciona un Centro de Acopio válido en el mapa o en la lista.";
                PuntoId = 0;
                return Page();
            }

            if (!ListaMateriales.Any(m => m.MaterialId == MaterialId))
            {
                MensajeError = "Selecciona un material válido.";
                return Page();
            }

            PesoKg = Math.Round(PesoKg, 2);
            if (PesoKg < PesoMinimoKg || PesoKg > PesoMaximoKg)
            {
                MensajeError = $"El peso debe estar entre {PesoMinimoKg:0.0} kg y {PesoMaximoKg:0} kg.";
                return Page();
            }

            if (FechaEntrega == default || FechaEntrega.Date > DateTime.Today)
            {
                MensajeError = "La fecha de entrega no puede ser mayor a hoy.";
                return Page();
            }

            // --------------------- VALIDACION DEL FOLIO DEL COMPROBANTE ---------------------
            Folio = (Folio ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(Folio))
            {
                MensajeError = "Escribe el folio del comprobante que te dio el centro de acopio.";
                return Page();
            }
            if (!PatronFolio.IsMatch(Folio))
            {
                MensajeError = "El folio no es válido. Usa de 6 a 20 caracteres entre letras, números y guiones (ej. ROOTY-4821-9F3A).";
                return Page();
            }

            FechaEntrega = FechaEntrega.Date;
            var materialElegido = ListaMateriales.First(m => m.MaterialId == MaterialId);

            int puntosOtorgados = (int)Math.Round(PesoKg * materialElegido.PuntosPorKg);
            decimal co2Evitado = PesoKg * materialElegido.Co2PorKg;

            // --------------------- TRANSACCION: APORTE + PUNTOS + INSIGNIAS ---------------------
            try
            {
                using (SqlConnection con = new SqlConnection(conexion))
                {
                    con.Open();
                    using (SqlTransaction tx = con.BeginTransaction())
                    {
                        try
                        {
                            // 0. Se bloquea la fila del usuario para evitar duplicados por solicitudes simultáneas
                            string sqlBloqueo = "SELECT usuario_id FROM USUARIOS WITH (UPDLOCK, ROWLOCK) WHERE usuario_id = @uid";
                            using (SqlCommand cmd = new SqlCommand(sqlBloqueo, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@uid", usuarioId);
                                object? existe = cmd.ExecuteScalar();
                                if (existe == null)
                                {
                                    tx.Rollback();
                                    return RedirectToPage("/Login");
                                }
                            }

                            // 1. Insertar el registro de reciclaje con la fecha elegida y el folio (el folio no se puede repetir)
                            string sqlInsert = @"
                                INSERT INTO REGISTROS_RECICLAJE (usuario_id, punto_id, material_id, peso_kg, puntos_otorgados, fecha, folio_comprobante)
                                VALUES (@uid, @pid, @mid, @peso, @pts, @fecha, @folio)";
                            using (SqlCommand cmd = new SqlCommand(sqlInsert, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@uid", usuarioId);
                                cmd.Parameters.AddWithValue("@pid", PuntoId);
                                cmd.Parameters.AddWithValue("@mid", MaterialId);
                                cmd.Parameters.AddWithValue("@peso", PesoKg);
                                cmd.Parameters.AddWithValue("@pts", puntosOtorgados);
                                cmd.Parameters.AddWithValue("@fecha", FechaEntrega);
                                cmd.Parameters.AddWithValue("@folio", Folio);
                                cmd.ExecuteNonQuery();
                            }

                            // 2. Sumar los puntos del aporte al usuario
                            string sqlUpdateUser = @"
                                UPDATE USUARIOS
                                SET puntos_acumulados = puntos_acumulados + @pts
                                WHERE usuario_id = @uid";
                            using (SqlCommand cmd = new SqlCommand(sqlUpdateUser, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@pts", puntosOtorgados);
                                cmd.Parameters.AddWithValue("@uid", usuarioId);
                                cmd.ExecuteNonQuery();
                            }

                            // 3. CO2 total acumulado del usuario (ya incluye este aporte)
                            decimal co2Total = 0;
                            string sqlCo2Total = @"
                                SELECT ISNULL(SUM(r.peso_kg * m.co2_por_kg), 0)
                                FROM REGISTROS_RECICLAJE r
                                INNER JOIN MATERIALES m ON r.material_id = m.material_id
                                WHERE r.usuario_id = @uid";
                            using (SqlCommand cmd = new SqlCommand(sqlCo2Total, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@uid", usuarioId);
                                co2Total = Convert.ToDecimal(cmd.ExecuteScalar());
                            }

                            // 4. Insignias nuevas alcanzadas que el usuario aún no tiene
                            var nuevas = new List<(int id, string nombre, int puntosExtra)>();
                            string sqlNuevas = @"
                                SELECT insignia_id, nombre, puntos_recompensa
                                FROM INSIGNIAS
                                WHERE co2_requerido <= @co2
                                  AND insignia_id NOT IN (
                                      SELECT insignia_id FROM USUARIOS_INSIGNIAS WHERE usuario_id = @uid
                                  )
                                ORDER BY co2_requerido ASC";
                            using (SqlCommand cmd = new SqlCommand(sqlNuevas, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@co2", co2Total);
                                cmd.Parameters.AddWithValue("@uid", usuarioId);
                                using (SqlDataReader dr = cmd.ExecuteReader())
                                {
                                    while (dr.Read())
                                    {
                                        nuevas.Add((dr.GetInt32(0), dr.GetString(1), dr.GetInt32(2)));
                                    }
                                }
                            }

                            // 5. Registrar cada insignia nueva y sumar sus puntos extra
                            foreach (var insignia in nuevas)
                            {
                                string sqlInsertInsignia = @"
                                    INSERT INTO USUARIOS_INSIGNIAS (usuario_id, insignia_id, fecha_obtenida)
                                    VALUES (@uid, @iid, GETDATE())";
                                using (SqlCommand cmd = new SqlCommand(sqlInsertInsignia, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                                    cmd.Parameters.AddWithValue("@iid", insignia.id);
                                    cmd.ExecuteNonQuery();
                                }

                                string sqlSumarExtra = @"
                                    UPDATE USUARIOS
                                    SET puntos_acumulados = puntos_acumulados + @pts
                                    WHERE usuario_id = @uid";
                                using (SqlCommand cmd = new SqlCommand(sqlSumarExtra, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@pts", insignia.puntosExtra);
                                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                                    cmd.ExecuteNonQuery();
                                }

                                InsigniasNuevas.Add(insignia.nombre);
                                PuntosExtraInsignias += insignia.puntosExtra;
                            }

                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex) when ((ex.Number == 2601 || ex.Number == 2627) && ex.Message.Contains("UX_REGISTROS_FOLIO"))
            {
                // El folio ya fue utilizado en otro aporte
                InsigniasNuevas.Clear();
                PuntosExtraInsignias = 0;
                MensajeError = "Este folio ya fue utilizado. Cada comprobante solo se puede registrar una vez.";
                return Page();
            }
            catch (Exception)
            {
                // Se limpian los resultados parciales y se conservan los datos del formulario
                InsigniasNuevas.Clear();
                PuntosExtraInsignias = 0;
                MensajeError = "Ocurrió un error al guardar tu aporte. Intenta de nuevo.";
                return Page();
            }

            // --------------------- DATOS DEL COMPROBANTE ---------------------
            NombrePuntoComprobante = puntoElegido.NombrePunto;
            PuntosGanadosUltimo = puntosOtorgados;
            Co2GanadoUltimo = co2Evitado;
            MensajeExito = $"¡Aporte registrado el {FechaEntrega:dd/MM/yyyy}!";

            return Page();
        }

        // ------------------ CATALOGOS (CENTROS Y MATERIALES) ------------------
        private void CargarCatalogos()
        {
            using (SqlConnection con = new SqlConnection(conexion))
            {
                con.Open();

                string sqlPuntos = @"
                    SELECT punto_id, nombre_punto, ubicacion_detalle, latitud, longitud 
                    FROM PUNTOS_ACOPIO 
                    WHERE activo = 1 
                    ORDER BY punto_id ASC";

                using (SqlCommand cmd = new SqlCommand(sqlPuntos, con))
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        ListaPuntos.Add(new PuntoAcopioItem
                        {
                            PuntoId = dr.GetInt32(0),
                            NombrePunto = dr.GetString(1),
                            UbicacionDetalle = dr.GetString(2),
                            Latitud = dr.GetDecimal(3),
                            Longitud = dr.GetDecimal(4)
                        });
                    }
                }

                string sqlMateriales = @"
                    SELECT material_id, nombre_material, puntos_por_kg, co2_por_kg 
                    FROM MATERIALES 
                    ORDER BY material_id ASC";

                using (SqlCommand cmd = new SqlCommand(sqlMateriales, con))
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        ListaMateriales.Add(new MaterialItem
                        {
                            MaterialId = dr.GetInt32(0),
                            NombreMaterial = dr.GetString(1),
                            PuntosPorKg = dr.GetInt32(2),
                            Co2PorKg = dr.GetDecimal(3)
                        });
                    }
                }
            }

            PuntosJson = JsonSerializer.Serialize(ListaPuntos);
            MaterialesJson = JsonSerializer.Serialize(ListaMateriales);
        }
    }

    // ------------------ CLASES DE APOYO ------------------
    public class PuntoAcopioItem
    {
        public int PuntoId { get; set; }
        public string NombrePunto { get; set; } = string.Empty;
        public string UbicacionDetalle { get; set; } = string.Empty;
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
    }

    public class MaterialItem
    {
        public int MaterialId { get; set; }
        public string NombreMaterial { get; set; } = string.Empty;
        public int PuntosPorKg { get; set; }
        public decimal Co2PorKg { get; set; }
    }
}