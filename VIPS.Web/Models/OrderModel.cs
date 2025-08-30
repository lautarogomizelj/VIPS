namespace VIPS.Web.Models
{
    public class OrderModel
    {
        public int      IdPedido            { get; set; }
        public decimal  Ancho               { get; set; }
        public decimal  Largo               { get; set; }
        public decimal  Alto                { get; set; }
        public decimal  Peso                { get; set; }
        public int      IdCliente           { get; set; }
        public string   DomicilioEntrega    { get; set; }

        public int      IdEstadoPedido { get; set; }

    }
}