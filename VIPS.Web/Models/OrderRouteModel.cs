namespace VIPS.Web.Models
{
    public class OrderRouteModel
    {
        public int      IdPedido            { get; set; }
        public decimal  Ancho               { get; set; }
        public decimal  Largo               { get; set; }
        public decimal  Alto                { get; set; }
        public decimal  Peso                { get; set; }
        public decimal  Latitud             { get; set; }
        public decimal  Longitud            { get; set; }
    }
}