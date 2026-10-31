import { openGameDetails } from './gamedetails.js';

export async function loadDashboard() {
    try {
        const response = await fetch('/api/home/dashboard');
        if (!response.ok) {
            showHomeError("Failed to fetch dashboard data from server operations.");
            return;
        }
        const data = await response.json();

        renderHomeStrip('anticipated-strip', data.MostAnticipated);
        renderHomeStrip('releases-strip', data.LatestReleases);
        renderHomeStrip('rated-strip', data.Top10RatedGames);
    } catch (err) {
        showHomeError("Unable to securely connect to the backend core framework service.");
    }
}

function renderHomeStrip(elementId, games) {
    const container = document.getElementById(elementId);
    if (!container) return;

    if (!games || games.length === 0) {
        container.innerHTML = '<p class="loading-text">No matching records returned from IGDB indices.</p>';
        return;
    }

    container.innerHTML = '';
    games.forEach(game => {
        let cardHtml = `
            <div class="game-card" data-id="${game.Id}">
                <div class="game-cover" style="background-image: url('${game.CoverUrl}');"></div>
                <div class="game-title" title="${game.Name}">${game.Name}</div>`;

        if (elementId === 'anticipated-strip' || elementId === 'releases-strip') {
            cardHtml += `<div class="game-meta-date">${game.ReleaseDate || 'TBD'}</div>`;
        } else if (elementId === 'rated-strip') {
            const scoreOutOfFive = Math.round((game.Rating / 100) * 5);
            let stars = '★'.repeat(scoreOutOfFive) + '☆'.repeat(5 - scoreOutOfFive);
            cardHtml += `
                <div class="game-meta-rating">
                    <span class="game-rating-stars" title="${game.Rating.toFixed(1)}/100">${stars}</span>
                    <span class="game-rating-count">(${game.RatingCount})</span>
                </div>`;
        }
        cardHtml += `</div>`;

        const cardElement = document.createElement('div');
        cardElement.innerHTML = cardHtml.trim();
        const mainChild = cardElement.firstChild;

        mainChild.addEventListener('click', () => openGameDetails(game.Id, null));
        container.appendChild(mainChild);
    });
}

function showHomeError(message) {
    document.querySelectorAll('.game-strip').forEach(strip => {
        strip.innerHTML = `<p class="loading-text" style="color: #ff4a4a;">${message}</p>`;
    });
}
