namespace VIPS.Web.Models
{
    public class RouteViewModel
    {
        public int IdRuta { get; set; }
        public string Patente { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CantPedidos { get; set; }
        public string Estado { get; set; }
        public int IdCamion { get; set; }
    }
}