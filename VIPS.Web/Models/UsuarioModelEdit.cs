namespace VIPS.Web.Models
{
    public class UsuarioModelEdit
    {
        public int IdUsuario { get; set; }

        public string Usuario{ get; set; }

        public string Dni { get; set; }

        public string Nombre { get; set; }

        public string Apellido { get; set; }

        public string Email { get; set; }

        public string Telefono { get; set; }

        public int IdRol { get; set; }
    }
}