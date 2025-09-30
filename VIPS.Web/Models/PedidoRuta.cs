namespace VIPS.Web.Models
{
    public class PedidoRuta
    {
        public int IdPedido { get; set; }
        public string Cliente { get; set; }
        public string Direccion { get; set; }
        public int OrdenEntrega { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public string Estado { get; set; }
        public string? TelefonoCliente { get; set; }
        public int? CantCompletados { get; set; }
        public int? CantTotal { get; set; }
    }
}