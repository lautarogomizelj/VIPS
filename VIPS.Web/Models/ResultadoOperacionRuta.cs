namespace VIPS.Web.Models
{
    public class ResultadoOperacionRuta
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public int CodigoError { get; set; }

        // Si Exito = false
        public int CantidadPedidosPendientes { get; set; }

        // Si Exito = true
        public int CantidadRutasGeneradas { get; set; }
        public int CantidadPedidos { get; set; }
        public int CantidadVehiculos { get; set; }
    }
}