namespace VIPS.Web.Models
{
    public class FleetViewModel
    {
        public int      IdCamion                { get; set; }
        public string   Patente                 { get; set; }
        public string   CapacidadPeso           { get; set; }
        public string   CapacidadVolumen        { get; set; }
        public DateTime FechaCreacion           { get; set; }
        public int      Estado                  { get; set; }
        public int      Asignado                { get; set; }

    }
}