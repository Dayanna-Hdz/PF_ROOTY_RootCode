using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PF_ROOTY_RootCode.Pages
{
    public class IndexModel : PageModel
    {
        // ------------------ CONEXION BASE DE DATOS ------------------
        private string conexion = "Server=db82448.databaseasp.net;Database=db82448;User Id=db82448;Password=tX@6!x5Tp8B?;Encrypt=False;MultipleActiveResultSets=True;";

        // ------------------ PROPIEDADES DE LA VISTA ------------------
        public List<PuntoAcopioItem> ListaPuntos { get; set; } = new();
        public string PuntosJson { get; set; } = "[]";

        // ------------------ CARGA DE LA LANDING PAGE ------------------
        public void OnGet()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(conexion))
                {
                    con.Open();

                    // Centros de acopio activos para mostrarlos en el mapa público
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
                }

                PuntosJson = JsonSerializer.Serialize(ListaPuntos);
            }
            catch (Exception)
            {
                // Si falla la base de datos, la página principal sigue cargando sin puntos
                ListaPuntos = new List<PuntoAcopioItem>();
                PuntosJson = "[]";
            }
        }
    }
}