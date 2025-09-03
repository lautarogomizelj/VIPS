namespace VIPS.Web.Models
{
    public class OrderViewModel
    {
        public int IdPedido             { get; set; }
        public string  NombreCliente            { get; set; }
        public string Peso             { get; set; }
        public DateTime FechaCreacion   { get; set; }
        public DateTime? FechaDespacho   { get; set; }
        public string EstadoPedido      { get; set; }
        public string Direccion         { get; set; }
    }
}