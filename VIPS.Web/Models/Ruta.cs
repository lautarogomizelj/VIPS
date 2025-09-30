namespace VIPS.Web.Models
{
    public class Ruta
    {
        public int IdRuta { get; set; }
        public int IdCamion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int idEstadoRuta { get; set; } 
        public string? Estado {  get; set; }
    }
}