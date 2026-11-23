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

            // Se regresa a la página de inicio
            return Redirect("/");
        }
    }
}