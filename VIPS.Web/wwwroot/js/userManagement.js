function confirmarEliminacion(usuario) {
    console.log(usuario)
    // Mostrar el cuadro de confirmaci�n
    if (confirm('�Est� seguro de que desea eliminar este usuario?')) {
        // Si el usuario confirma, redirigir a la acci�n de eliminaci�n
        window.location.href = '/AdminGeneral/DeleteUser/' + usuario;
    }
}




if (TempData["MensajeExitoFormularioCrearUsuario"] != null) {
    <script>
        alert('@TempData["MensajeExitoFormularioCrearUsuario"]');
    </script>
}