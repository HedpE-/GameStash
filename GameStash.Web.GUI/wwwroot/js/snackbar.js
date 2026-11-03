/**
 * Global Floating Snackbar Notification Engine (Navbar Docked)
 */

function ensureSnackbarContainer() {
    let container = document.getElementById('snackbar-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'snackbar-container';
        container.className = 'snackbar-container';

        // Ancorado dentro da sidebar
        const sidebar = document.querySelector('.sidebar') || document.body;
        sidebar.appendChild(container);
    }
    return container;
}

/**
 * Displays a floating notification panel rising from the bottom of the sidebar.
 * @param {string} message - Notification text
 * @param {'success' | 'error' | 'warning' | 'info'} type - Toast type
 * @param {number} duration - Auto-dismiss timeout in ms (default: 4000)
 */
export function showSnackbar(message, type = 'info', duration = 4000) {
    const container = ensureSnackbarContainer();

    const toast = document.createElement('div');
    toast.className = `snackbar-toast snackbar-${type}`;

    const icons = {
        success: '✓',
        error: '✕',
        warning: '!',
        info: 'ℹ'
    };

    toast.innerHTML = `
        <span class="snackbar-icon">${icons[type] || 'ℹ'}</span>
        <span class="snackbar-message">${message}</span>
        <button type="button" class="snackbar-close-btn" aria-label="Close">✕</button>
    `;

    const closeBtn = toast.querySelector('.snackbar-close-btn');
    closeBtn.addEventListener('click', () => dismissToast(toast));

    container.appendChild(toast);

    // Trigger slide-up animation
    requestAnimationFrame(() => {
        toast.classList.add('snackbar-visible');
    });

    // Auto-dismiss timer
    let timeoutId = setTimeout(() => {
        dismissToast(toast);
    }, duration);

    // Pause on hover
    toast.addEventListener('mouseenter', () => clearTimeout(timeoutId));
    toast.addEventListener('mouseleave', () => {
        timeoutId = setTimeout(() => dismissToast(toast), 1500);
    });
}

function dismissToast(toast) {
    if (!toast || toast.classList.contains('snackbar-hiding')) return;
    toast.classList.remove('snackbar-visible');
    toast.classList.add('snackbar-hiding');
    toast.addEventListener('transitionend', () => {
        toast.remove();
    });
}

// Convenient helper aliases
export const showSuccess = (msg, duration) => showSnackbar(msg, 'success', duration);
export const showError = (msg, duration) => showSnackbar(msg, 'error', duration);
export const showWarning = (msg, duration) => showSnackbar(msg, 'warning', duration);
export const showInfo = (msg, duration) => showSnackbar(msg, 'info', duration);