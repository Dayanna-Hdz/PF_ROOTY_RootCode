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
                    string query = "SELECT COUNT(*) FROM USUARIOS WHERE correo = @correo AND password_hash = @password";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@correo", Correo);
                    cmd.Parameters.AddWithValue("@password", Password);

                    con.Open();
                    int existe = (int)cmd.ExecuteScalar();

                    if (existe > 0)
                    {
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
            catch (Exception ex)
            {
                Mensaje = "Error: " + ex.Message;
                return Page();
            }
        }
    }
}