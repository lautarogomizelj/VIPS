namespace VIPS.Web.Models
{
    public class FleetViewModel
    {
        public int      IdCamion                { get; set; }
        public string   Patente                 { get; set; }
        public decimal  CapacidadPeso           { get; set; }
        public decimal  CapacidadVolumen        { get; set; }
        public DateTime FechaCreacion  { get; set; }
        public int   Estado                  { get; set; }
    }
}