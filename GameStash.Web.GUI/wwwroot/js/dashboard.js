import { createGameCard } from './gamecard.js';
import { showSuccess, showError, showWarning } from './snackbar.js';

export async function loadDashboard() {
    try {
        // 1. Check whether credentials exist.
        const statusResponse = await fetch('/api/settings/igdb/status');
        if (!statusResponse.ok) {
            const status = await statusResponse.json();
            renderCredentialsWarning(status.Message);
            return;
        }

        // 2. Load the dashboard normally when credentials exist.
        const response = await fetch('/api/home/dashboard');
        if (!response.ok) {
            showHomeError("Failed to fetch dashboard data from server operations.");
            return;
        }
        const data = await response.json();

        renderHomeStrip('anticipated-strip', data.MostAnticipated);
        renderHomeStrip('releases-strip', data.LatestReleases);
        renderHomeStrip('rated-strip', data.Top10RatedGames, 'Rating');
    } catch (err) {
        showHomeError("Unable to securely connect to the backend core framework service.");
    }
}

function renderCredentialsWarning(message) {
    const container = document.querySelector('.home-container');
    if (!container) return;

    // Detect if the issue is a validation crash or missing fields to swap titles dynamically
    const isAuthenticationFailure = message.includes("Twitch");
    const titleText = isAuthenticationFailure ? "Authentication Failure" : "Incomplete Configuration";

    container.innerHTML = `
        <div class="credentials-warning-box">
            <div class="warning-icon">⚠️</div>
            <h2>${titleText}</h2>
            <p class="warning-backend-message">${message.replace(/\n/g, '<br>')}</p>
            <p class="warning-subtext">Please navigate to the <strong>Global Settings</strong> section to verify your Client ID and Client Secret in order to activate the game catalog and local sync operations.</p>
            <button class="nav-to-settings-btn" id="warn-redirect-settings">Go to Global Settings</button>
        </div>
    `;

    document.getElementById('warn-redirect-settings')?.addEventListener('click', () => {
        import('./router.js').then(m => m.navigateTo('settings'));
    });
}

function renderHomeStrip(elementId, games, currentSortOption = 'Name') {
    const container = document.getElementById(elementId);
    if (!container) {
        showError(`${elementId} could not be found`);
        return;
    }

    if (!games || games.length === 0) {
        container.innerHTML = '<p class="loading-text">No matching records returned from IGDB indices.</p>';
        return;
    }

    container.innerHTML = '';
    games.forEach(game => {
        // Create the card using the unified component with dashboard mode enabled.
        const card = createGameCard(
            game,
            currentSortOption,
            null,
            null,
            null,
            'none'
        );

        // For anticipated releases or latest releases, inject the full date when the component returned only the year.
        if (elementId === 'anticipated-strip' || elementId === 'releases-strip') {
            const metaContainer = card.querySelector('.game-card-meta');
            if (metaContainer) {
                metaContainer.innerText = game.ReleaseDate || 'TBD';
            }
        }

        container.appendChild(card);
    });
}

function showHomeError(message) {
    document.querySelectorAll('.game-strip').forEach(strip => {
        strip.innerHTML = `<p class="loading-text" style="color: #ff4a4a;">${message}</p>`;
    });
}