namespace VIPS.Web.Models
{
    public class FleetRouteModel
    {
        public int      IdCamion            { get; set; }
        public decimal  Ancho               { get; set; }
        public decimal  Largo               { get; set; }
        public decimal  Alto                { get; set; }
        public decimal  CapacidadPeso                { get; set; }
        public decimal CapacidadVolumen  { get; set; }

        public decimal  Latitud             { get; set; }
        public decimal  Longitud            { get; set; }
    }
}