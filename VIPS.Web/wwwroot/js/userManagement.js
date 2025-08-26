function confirmarEliminacion(usuario) {
    console.log(usuario)
    // Mostrar el cuadro de confirmación
    if (confirm('¿Está seguro de que desea eliminar este usuario?')) {
        // Si el usuario confirma, redirigir a la acción de eliminación
        window.location.href = '/AdminGeneral/DeleteUser/' + usuario;
    }
}




if (TempData["MensajeExitoFormularioCrearUsuario"] != null) {
    <script>
        alert('@TempData["MensajeExitoFormularioCrearUsuario"]');
    </script>
}