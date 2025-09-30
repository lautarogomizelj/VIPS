
    const reasonSelect = document.getElementById('reasonSelect');
    const detailsSection = document.getElementById('detailsSection');
    const detailsTextarea = document.getElementById('detailsTextarea');

// Evento para cambiar visibilidad del textarea
reasonSelect.addEventListener('change', () => {
    if (reasonSelect.value === 'otro') {
        detailsSection.style.display = 'block'; // Mostrar
    detailsTextarea.required = true;        // Hacer obligatorio
    } else {
        detailsSection.style.display = 'none';  // Ocultar
    detailsTextarea.value = '';             // Limpiar texto
    detailsTextarea.required = false;       // No obligatorio
    }
});


