namespace VIPS.Web.Models
{
    public class UsuarioModel
    {
        public string Usuario{ get; set; }

        public string Dni { get; set; }

        public string Nombre { get; set; }

        public string Apellido { get; set; }

        public string Email { get; set; }

        public string Telefono { get; set; }

        public string? TelefonoEmergencia { get; set; }

        public string Contrasenia { get; set; }

        public int IdRol { get; set; }
    }
}