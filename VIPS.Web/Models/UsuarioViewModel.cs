namespace VIPS.Web.Models
{
    public class UsuarioViewModel
    {
        public string Usuario { get; set; }
        public string Rol { get; set; }
        public DateTime FechaAlta { get; set; }
        public DateTime? FechaUltimoAcceso { get; set; }
        public DateTime? FechaUltimoAccesoFallido { get; set; }
    }
}