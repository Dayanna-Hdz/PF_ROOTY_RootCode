using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;

namespace PF_ROOTY_RootCode.Pages
{
    public class LoginModel : PageModel
    {
        // ------------------ PROPIEDADES DEL FORMULARIO ------------------
        [BindProperty]
        public string Correo { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;

        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=localhost\\SQLEXPRESS;Database=ReciclajeDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // ------------------ LIMITE DE INTENTOS FALLIDOS ------------------
        private const int MaximoIntentos = 5;
        private const int MinutosBloqueo = 2;
        private const string ClaveIntentos = "LoginIntentosFallidos";
        private const string ClaveBloqueo = "LoginBloqueoHasta";

        // ------------------ METODOS Y LOGICA ------------------
        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            // --------------------- 0. REVISAR SI ESTA BLOQUEADO TEMPORALMENTE ---------------------
            string? bloqueoTexto = HttpContext.Session.GetString(ClaveBloqueo);
            if (long.TryParse(bloqueoTexto, out long bloqueoTicks) && bloqueoTicks > DateTime.UtcNow.Ticks)
            {
                int segundos = (int)Math.Ceiling(new TimeSpan(bloqueoTicks - DateTime.UtcNow.Ticks).TotalSeconds);
                Mensaje = $"Demasiados intentos fallidos. Intenta de nuevo en {segundos} segundos.";
                return Page();
            }

            // --------------------- 1. LIMPIAR Y VALIDAR LOS DATOS ---------------------
            Correo = (Correo ?? string.Empty).Trim().ToLowerInvariant();
            Password = Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Correo) || string.IsNullOrEmpty(Password))
            {
                Mensaje = "Ingresa tu correo y contraseña.";
                return Page();
            }

            // Datos con tamaño imposible se tratan como credenciales incorrectas
            if (Correo.Length > 100 || Password.Length > 64)
            {
                return RegistrarFallo();
            }

            // --------------------- 2. BUSCAR AL USUARIO ---------------------
            try
            {
                using (SqlConnection con = new SqlConnection(conexion))
                {
                    // La contraseña se compara distinguiendo mayúsculas y minúsculas
                    string query = @"
                        SELECT usuario_id, nombre
                        FROM USUARIOS
                        WHERE correo = @correo
                          AND password_hash COLLATE Latin1_General_CS_AS = @password";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@correo", Correo);
                    cmd.Parameters.AddWithValue("@password", Password);

                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int usuarioId = reader.GetInt32(0);
                            string nombreCompleto = reader.GetString(1);

                            // Se reinicia la sesión y se guardan ID y nombre para todo el sistema
                            HttpContext.Session.Clear();
                            HttpContext.Session.SetInt32("UsuarioId", usuarioId);
                            HttpContext.Session.SetString("UsuarioNombre", nombreCompleto);

                            // ------------------ REDIRECCION AL MENU PRINCIPAL ------------------
                            return RedirectToPage("/MenuPrincipal");
                        }
                    }
                }

                // Correo o contraseña incorrectos
                return RegistrarFallo();
            }
            catch (Exception)
            {
                // No se muestran detalles técnicos al usuario
                Mensaje = "No se pudo iniciar sesión en este momento. Intenta de nuevo.";
                return Page();
            }
        }

        // ------------------ REGISTRAR UN INTENTO FALLIDO ------------------
        private IActionResult RegistrarFallo()
        {
            int intentos = (HttpContext.Session.GetInt32(ClaveIntentos) ?? 0) + 1;

            if (intentos >= MaximoIntentos)
            {
                HttpContext.Session.SetInt32(ClaveIntentos, 0);
                HttpContext.Session.SetString(ClaveBloqueo, DateTime.UtcNow.AddMinutes(MinutosBloqueo).Ticks.ToString());
                Mensaje = $"Demasiados intentos fallidos. Intenta de nuevo en {MinutosBloqueo} minutos.";
            }
            else
            {
                HttpContext.Session.SetInt32(ClaveIntentos, intentos);
                Mensaje = "Correo o contraseña incorrectos.";
            }

            return Page();
        }
    }
}