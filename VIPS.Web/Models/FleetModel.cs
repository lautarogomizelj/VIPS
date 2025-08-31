namespace VIPS.Web.Models
{
    public class FleetModel
    {
        public string   Patente                 { get; set; }
        public decimal  Ancho                   { get; set; }
        public decimal  Largo                   { get; set; }
        public decimal  Alto                    { get; set; }
        public decimal  CapacidadPeso           { get; set; }
        public decimal  CapacidadVolumen        { get; set; }
        public int      Estado                  { get; set; }
    }
}