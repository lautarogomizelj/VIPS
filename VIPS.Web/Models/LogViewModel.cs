namespace VIPS.Web.Models
{
    public class LogViewModel
    {
        public string idLogActividad { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaHora { get; set; }
        public String Accion { get; set; }
        public String? Detalle { get; set; }
    }
}