using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PF_ROOTY_RootCode.Pages
{
    public class LogoutModel : PageModel
    {
        // ------------------ CIERRE DE SESION ------------------
        public IActionResult OnGet()
        {
            // Se borran todos los datos de la sesión (UsuarioId, UsuarioNombre)
            HttpContext.Session.Clear();

            // Se elimina la cookie de sesión en el navegador
            Response.Cookies.Delete(".AspNetCore.Session");

            // Se fuerza a no guardar caché en esta respuesta
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            // Se regresa a la página de inicio
            return RedirectToPage("/Index");
        }
    }
}