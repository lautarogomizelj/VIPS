function toggleDetails(orderId) {
    const details = document.getElementById(`details-${orderId}`);
    const icon = document.getElementById(`icon-${orderId}`);

    if (details.classList.contains('expanded')) {
        details.classList.remove('expanded');
        icon.style.transform = 'rotate(0deg)';
    } else {
        details.classList.add('expanded');
        icon.style.transform = 'rotate(180deg)';
    }
}


/*-------------------------------------------*/

function toggleDetails(orderId) {
    const details = document.getElementById(`details-${orderId}`);
    const icon = document.getElementById(`icon-${orderId}`);
    if (details.classList.contains('expanded')) {
        details.classList.remove('expanded');
        icon.style.transform = 'rotate(0deg)';
    } else {
        details.classList.add('expanded');
        icon.style.transform = 'rotate(180deg)';
    }
}

/*-------------------------------------------*/
// Simulación del proceso de generación
function startGeneration() {
    simulateProgress();
}

function simulateProgress() {
    const progressBar = document.getElementById('progress');
    const progressText = document.getElementById('progress-text');
    let progress = 0;

    const interval = setInterval(() => {
        progress += Math.random() * 15;
        if (progress > 100) progress = 100;

        if (progressBar) progressBar.style.width = progress + '%';
        if (progressText) progressText.textContent = Math.round(progress) + '%';

        if (progress >= 100) {
            clearInterval(interval);
            setTimeout(() => {
                showResult();
            }, 1000);
        }
    }, 200);
}

function showResult() {
    // Ocultar pantalla de carga
    const loadingScreen = document.getElementById('loading-screen');
    if (loadingScreen) {
        loadingScreen.style.display = 'none';
    }

    // Mostrar la pantalla de resultado correspondiente
    const successScreen = document.getElementById('success-screen');
    const errorScreen = document.getElementById('error-screen');

    if (successScreen) {
        successScreen.style.display = 'block';
    }

    if (errorScreen) {
        errorScreen.style.display = 'block';
    }
}

// Inicializar cuando la página cargue
document.addEventListener('DOMContentLoaded', function () {
    // Iniciar la animación de carga
    startGeneration();
});