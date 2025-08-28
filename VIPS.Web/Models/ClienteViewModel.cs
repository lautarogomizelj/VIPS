namespace VIPS.Web.Models
{
    public class ClienteViewModel
    {
        public int    IdCliente     { get; set; }
        public string Nombre        { get; set; }
        public string Apellido      { get; set; }
        public string DomicilioLegal{ get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}