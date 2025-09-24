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