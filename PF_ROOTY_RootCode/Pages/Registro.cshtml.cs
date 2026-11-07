using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

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

        public string Mensaje { get; set; } = string.Empty;

        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=localhost\\SQLEXPRESS;Database=ReciclajeDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // ------------------ METODOS Y LOGICA ------------------
        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(Nombre) || string.IsNullOrEmpty(Correo) || string.IsNullOrEmpty(Password))
            {
                Mensaje = "Llena todos los campos.";
                return Page();
            }

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
                    Mensaje = "Error en la base de datos: " + ex.Message;
                }
                return Page();
            }
            catch (Exception ex)
            {
                Mensaje = "Error inesperado: " + ex.Message;
                return Page();
            }
        }
    }
}