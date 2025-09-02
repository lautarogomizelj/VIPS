namespace VIPS.Web.Models
{
    public class OrderModel
    {
        public int      IdPedido            { get; set; }

        public int      IdCliente           { get; set; }
        public string   Ancho               { get; set; }
        public string   Largo               { get; set; }
        public string   Alto                { get; set; }
        public string   Peso                { get; set; }
        public string   DomicilioEntrega    { get; set; }
        public string   Ciudad              { get; set; }
        public string   Provincia           { get; set; }

        public int      IdEstadoPedido { get; set; }

    }
}