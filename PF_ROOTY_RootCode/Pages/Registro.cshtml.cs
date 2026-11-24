using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Text.RegularExpressions;

namespace PF_ROOTY_RootCode.Pages
{
    public class RegistroModel : PageModel
    {
        // ------------------ PROPIEDADES DEL FORMULARIO ------------------
        [BindProperty]
        public string Nombre { get; set; } = string.Empty;

        [BindProperty]
        public string Correo { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmarPassword { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;

        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=localhost\\SQLEXPRESS;Database=ReciclajeDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // ------------------ REGLAS DE VALIDACION ------------------
        private const int LargoMinimoPassword = 8;
        private const int LargoMaximoPassword = 64;

        // Correo: texto@dominio.extension (el dominio debe llevar punto y extension de 2+ letras)
        private static readonly Regex PatronCorreo = new Regex(
            @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$",
            RegexOptions.Compiled);

        // Nombre: letras (con acentos), espacios, punto, apostrofo y guion
        private static readonly Regex PatronNombre = new Regex(
            @"^\p{L}[\p{L} .'\-]*$",
            RegexOptions.Compiled);

        // ------------------ METODOS Y LOGICA ------------------
        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            Nombre = (Nombre ?? string.Empty).Trim();
            Correo = (Correo ?? string.Empty).Trim().ToLowerInvariant();
            Password = Password ?? string.Empty;
            ConfirmarPassword = ConfirmarPassword ?? string.Empty;

            // --------------------- VALIDACIONES DE SERVIDOR ---------------------
            string? error = ValidarDatos();
            if (error != null)
            {
                Mensaje = error;
                return Page();
            }

            // --------------------- GUARDAR USUARIO ---------------------
            try
            {
                using (SqlConnection con = new SqlConnection(conexion))
                {
                    string query = "INSERT INTO USUARIOS (nombre, correo, password_hash) VALUES (@nombre, @correo, @password)";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@nombre", Nombre);
                    cmd.Parameters.AddWithValue("@correo", Correo);
                    cmd.Parameters.AddWithValue("@password", Password);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                // ------------------ REDIRECCION AL LOGIN ------------------
                return RedirectToPage("/Login");
            }
            catch (SqlException ex)
            {
                // ------------------ CONTROL DE ERRORES SQL SERVER ------------------
                if (ex.Number == 2627 || ex.Number == 2601)
                {
                    Mensaje = "El correo electrónico ya está registrado. Intenta iniciar sesión.";
                }
                else
                {
                    Mensaje = "No se pudo crear la cuenta en este momento. Intenta de nuevo.";
                }
                return Page();
            }
            catch (Exception)
            {
                Mensaje = "Ocurrió un error inesperado. Intenta de nuevo.";
                return Page();
            }
        }

        // ------------------ VALIDACION DE LOS DATOS DEL FORMULARIO ------------------
        private string? ValidarDatos()
        {
            if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Correo) ||
                string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmarPassword))
            {
                return "Llena todos los campos.";
            }

            // Nombre
            if (Nombre.Length < 2 || Nombre.Length > 100)
            {
                return "El nombre debe tener entre 2 y 100 caracteres.";
            }
            if (!PatronNombre.IsMatch(Nombre))
            {
                return "El nombre solo puede llevar letras, espacios, punto, apóstrofo y guion.";
            }

            // Correo
            if (Correo.Length > 100)
            {
                return "El correo debe tener máximo 100 caracteres.";
            }
            if (!PatronCorreo.IsMatch(Correo))
            {
                return "Escribe un correo válido, por ejemplo: nombre@gmail.com";
            }

            // Contraseña
            if (Password.Length < LargoMinimoPassword)
            {
                return $"La contraseña debe tener al menos {LargoMinimoPassword} caracteres.";
            }
            if (Password.Length > LargoMaximoPassword)
            {
                return $"La contraseña debe tener máximo {LargoMaximoPassword} caracteres.";
            }
            if (!Regex.IsMatch(Password, "[A-Z]"))
            {
                return "La contraseña debe incluir al menos una letra mayúscula.";
            }
            if (!Regex.IsMatch(Password, "[a-z]"))
            {
                return "La contraseña debe incluir al menos una letra minúscula.";
            }
            if (!Regex.IsMatch(Password, "[0-9]"))
            {
                return "La contraseña debe incluir al menos un número.";
            }

            // Confirmación
            if (Password != ConfirmarPassword)
            {
                return "Las contraseñas no coinciden.";
            }

            return null;
        }
    }
}