using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace PF_ROOTY_RootCode.Pages
{
    public class RecompensasModel : PageModel
    {
        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=localhost\\SQLEXPRESS;Database=ReciclajeDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // ------------------ PROPIEDADES DE LA VISTA ------------------
        public int PuntosDisponibles { get; set; }
        public List<RecompensaItem> ListaRecompensas { get; set; } = new();

        // ------------------ CARGA DE LA PAGINA ------------------
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

                // 0. Saldo de puntos del usuario
                string sqlPuntos = "SELECT puntos_acumulados FROM USUARIOS WHERE usuario_id = @uid";
                using (SqlCommand cmd = new SqlCommand(sqlPuntos, con))
                {
                    cmd.Parameters.AddWithValue("@uid", usuarioId);
                    object? res = cmd.ExecuteScalar();
                    PuntosDisponibles = res != null ? Convert.ToInt32(res) : 0;
                }

                // 1. Catálogo de recompensas
                string sqlRecompensas = @"
                    SELECT recompensa_id, titulo, descripcion, costo_puntos, stock,
                           ISNULL(color_marca, '#2e7d32'), ISNULL(imagen_archivo, '')
                    FROM RECOMPENSAS
                    ORDER BY recompensa_id ASC";

                using (SqlCommand cmd = new SqlCommand(sqlRecompensas, con))
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string titulo = dr.GetString(1);
                        string nombreMarca = titulo.Replace("Tarjeta ", "").Trim();

                        ListaRecompensas.Add(new RecompensaItem
                        {
                            RecompensaId = dr.GetInt32(0),
                            Titulo = titulo,
                            Descripcion = dr.GetString(2),
                            CostoPuntos = dr.GetInt32(3),
                            Stock = dr.GetInt32(4),
                            ColorMarca = dr.GetString(5),
                            ImagenArchivo = dr.GetString(6),
                            Inicial = nombreMarca.Length > 0 ? nombreMarca.Substring(0, 1).ToUpper() : "R"
                        });
                    }
                }
            }

            return Page();
        }

        // ------------------ CANJE DE UNA RECOMPENSA (LLAMADO DESDE JAVASCRIPT) ------------------
        public JsonResult OnPostCanjear(int recompensaId)
        {
            // Evita que el navegador guarde caché en la respuesta JSON
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            int? usuarioId = HttpContext.Session.GetInt32("UsuarioId");
            if (usuarioId == null)
            {
                return new JsonResult(new { ok = false, mensaje = "Tu sesión expiró. Inicia sesión de nuevo." });
            }

            using (SqlConnection con = new SqlConnection(conexion))
            {
                con.Open();
                using (SqlTransaction tx = con.BeginTransaction())
                {
                    try
                    {
                        // 0. Saldo actual del usuario (bloqueado durante la transacción)
                        int saldo;
                        string sqlSaldo = "SELECT puntos_acumulados FROM USUARIOS WITH (UPDLOCK, ROWLOCK) WHERE usuario_id = @uid";
                        using (SqlCommand cmd = new SqlCommand(sqlSaldo, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@uid", usuarioId);
                            object? res = cmd.ExecuteScalar();
                            if (res == null)
                            {
                                tx.Rollback();
                                return new JsonResult(new { ok = false, mensaje = "Usuario no encontrado." });
                            }
                            saldo = Convert.ToInt32(res);
                        }

                        // 1. Datos de la recompensa (bloqueada durante la transacción)
                        string titulo;
                        int costo;
                        int stock;
                        string urlDestino;

                        string sqlRecompensa = @"
                            SELECT titulo, costo_puntos, stock, ISNULL(url_destino, 'https://www.google.com')
                            FROM RECOMPENSAS WITH (UPDLOCK, ROWLOCK)
                            WHERE recompensa_id = @rid";
                        using (SqlCommand cmd = new SqlCommand(sqlRecompensa, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@rid", recompensaId);
                            using (SqlDataReader dr = cmd.ExecuteReader())
                            {
                                if (!dr.Read())
                                {
                                    dr.Close();
                                    tx.Rollback();
                                    return new JsonResult(new { ok = false, mensaje = "La recompensa no existe." });
                                }
                                titulo = dr.GetString(0);
                                costo = dr.GetInt32(1);
                                stock = dr.GetInt32(2);
                                urlDestino = dr.GetString(3);
                            }
                        }

                        // 2. Validaciones de stock y puntos
                        if (stock <= 0)
                        {
                            tx.Rollback();
                            return new JsonResult(new { ok = false, mensaje = "Esta recompensa ya está agotada." });
                        }

                        if (saldo < costo)
                        {
                            tx.Rollback();
                            return new JsonResult(new { ok = false, mensaje = "No tienes puntos suficientes para este canje." });
                        }

                        // 3. Descontar puntos al usuario
                        string sqlDescontar = "UPDATE USUARIOS SET puntos_acumulados = puntos_acumulados - @costo WHERE usuario_id = @uid";
                        using (SqlCommand cmd = new SqlCommand(sqlDescontar, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@costo", costo);
                            cmd.Parameters.AddWithValue("@uid", usuarioId);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Descontar una unidad de stock
                        string sqlStock = "UPDATE RECOMPENSAS SET stock = stock - 1 WHERE recompensa_id = @rid AND stock > 0";
                        using (SqlCommand cmd = new SqlCommand(sqlStock, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@rid", recompensaId);
                            cmd.ExecuteNonQuery();
                        }

                        // 5. Registrar el canje con su código único
                        string codigo = GenerarCodigoCanje();
                        string sqlCanje = @"
                            INSERT INTO CANJES (usuario_id, recompensa_id, codigo_canje)
                            VALUES (@uid, @rid, @codigo)";
                        using (SqlCommand cmd = new SqlCommand(sqlCanje, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@uid", usuarioId);
                            cmd.Parameters.AddWithValue("@rid", recompensaId);
                            cmd.Parameters.AddWithValue("@codigo", codigo);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();

                        return new JsonResult(new
                        {
                            ok = true,
                            mensaje = "¡Canje exitoso!",
                            titulo = titulo,
                            codigo = codigo,
                            url = urlDestino,
                            puntosRestantes = saldo - costo
                        });
                    }
                    catch (Exception)
                    {
                        tx.Rollback();
                        return new JsonResult(new { ok = false, mensaje = "Ocurrió un error al procesar el canje. Intenta de nuevo." });
                    }
                }
            }
        }

        // ------------------ GENERADOR DE CODIGO DE CANJE (ROOTY-XXXX-XXXX-XXXX) ------------------
        private static string GenerarCodigoCanje()
        {
            string guid = Guid.NewGuid().ToString("N").ToUpper();
            return $"ROOTY-{guid.Substring(0, 4)}-{guid.Substring(4, 4)}-{guid.Substring(8, 4)}";
        }
    }

    // ------------------ CLASE DE APOYO ------------------
    public class RecompensaItem
    {
        public int RecompensaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int CostoPuntos { get; set; }
        public int Stock { get; set; }
        public string ColorMarca { get; set; } = "#2e7d32";
        public string ImagenArchivo { get; set; } = string.Empty;
        public string Inicial { get; set; } = "R";
    }
}