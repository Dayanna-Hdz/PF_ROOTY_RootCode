using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

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

        // ------------------ METODOS Y LOGICA ------------------
        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(Correo) || string.IsNullOrEmpty(Password))
            {
                Mensaje = "Ingresa tu correo y contraseña.";
                return Page();
            }

            try
            {
                using (SqlConnection con = new SqlConnection(conexion))
                {
                    string query = "SELECT usuario_id, nombre FROM USUARIOS WHERE correo = @correo AND password_hash = @password";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@correo", Correo);
                    cmd.Parameters.AddWithValue("@password", Password);

                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Guardar ID y Nombre en la sesión para usarlos en MenuPrincipal y todo el sistema
                            int usuarioId = reader.GetInt32(0);
                            string nombreCompleto = reader.GetString(1);

                            HttpContext.Session.SetInt32("UsuarioId", usuarioId);
                            HttpContext.Session.SetString("UsuarioNombre", nombreCompleto);

                            // ------------------ REDIRECCION AL MENU PRINCIPAL ------------------
                            return RedirectToPage("/MenuPrincipal");
                        }
                        else
                        {
                            Mensaje = "Correo o contraseña incorrectos.";
                            return Page();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error: " + ex.Message;
                return Page();
            }
        }
    }
}