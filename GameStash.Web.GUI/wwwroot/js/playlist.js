/**
 * Unified Playlist & Algorithmic Recommendations Controller (Across All Stashes)
 */

import { createGameCard } from './gamecard.js';
import { openEditModal, openDeleteModal } from './stash.js';
import { showError } from './snackbar.js';

let isSyncing = false;

/**
 * Initializes the Playlist View lifecycle.
 * Called automatically by the SPA router when navigating to 'playlist'.
 */
export async function initPlaylistView() {
    setupGlobalSyncEvents();

    const refreshBtn = document.getElementById('playlist-refresh-btn');
    if (refreshBtn) {
        refreshBtn.onclick = () => loadAllPlaylistStrips(true);
    }

    await loadAllPlaylistStrips(false);
}

/**
 * Orchestrates data loading across all three strips in parallel.
 */
async function loadAllPlaylistStrips(forceRefresh = false) {
    if (isSyncing) return;
    isSyncing = true;

    const cacheBuster = forceRefresh ? `?v=${Date.now()}` : '';

    try {
        const [playlistRes, playlistRecsRes, stashRecsRes] = await Promise.all([
            fetch(`/api/playlist${cacheBuster}`),
            fetch(`/api/playlist/recommendations/playlist?limit=30${forceRefresh ? '&v=' + Date.now() : ''}`),
            fetch(`/api/playlist/recommendations/stash?limit=30${forceRefresh ? '&v=' + Date.now() : ''}`)
        ]);

        // 1. Render Active Playlist Queue
        if (playlistRes.ok) {
            const playlistGames = await playlistRes.json();
            renderPlaylistQueueStrip(playlistGames);
        } else {
            renderStripError('playlist-games-strip', 'Failed to load active playlist queue.');
        }

        // 2. Render Suggestions based on Current Playlist
        if (playlistRecsRes.ok) {
            const playlistRecs = await playlistRecsRes.json();
            renderRecommendationsStrip(
                'playlist-suggestions-strip',
                playlistRecs,
                '🎯',
                'Add games to your playlist to generate queue-aligned recommendations.'
            );
        } else {
            renderStripError('playlist-suggestions-strip', 'Failed to calculate playlist recommendations.');
        }

        // 3. Render Suggestions based on Stash Profile (Favorites / Ratings / Playing)
        if (stashRecsRes.ok) {
            const stashRecs = await stashRecsRes.json();
            renderRecommendationsStrip(
                'stash-suggestions-strip',
                stashRecs,
                '⭐',
                'Mark games as Favorites, play them, or add ratings to unlock personalized stash suggestions.'
            );
        } else {
            renderStripError('stash-suggestions-strip', 'Failed to calculate stash recommendations.');
        }

    } catch (err) {
        console.error('Critical error in playlist view orchestrator:', err);
        showError('Unable to connect to the recommendation engine.');
    } finally {
        isSyncing = false;
    }
}

/**
 * Helper to create a storefront-aware card for any stash game.
 */
function buildPlaylistCard(game) {
    const storeType = (game.ExternalSystem || game.externalSystem || 'local').toLowerCase();
    const cardGame = {
        ...game,
        showStoreBadge: true
    };

    return createGameCard(
        cardGame,
        'Name',
        storeType === 'local' ? openDeleteModal : null,
        openEditModal,
        () => loadAllPlaylistStrips(false),
        storeType
    );
}

/**
 * Strip 1: Renders games currently in the playlist queue.
 */
function renderPlaylistQueueStrip(games) {
    const container = document.getElementById('playlist-games-strip');
    const badge = document.getElementById('playlist-count-badge');

    const totalCount = Array.isArray(games) ? games.length : 0;
    if (badge) badge.innerText = `${totalCount} ${totalCount === 1 ? 'game' : 'games'}`;

    if (!container) return;

    if (!games || games.length === 0) {
        container.innerHTML = `
            <div class="empty-strip-box">
                <span class="empty-icon">📋</span>
                <strong>Your playlist queue is currently empty.</strong>
                <span>Click the bookmark icon or context menu on any game in your stashes to add it here.</span>
            </div>
        `;
        return;
    }

    container.innerHTML = '';
    games.forEach(game => {
        container.appendChild(buildPlaylistCard(game));
    });
}

/**
 * Strip 2 & 3: Renders recommendation strips with match scores and top explanation reasons.
 */
function renderRecommendationsStrip(containerId, recommendations, emptyIcon, emptyMessage) {
    const container = document.getElementById(containerId);
    if (!container) return;

    if (!recommendations || recommendations.length === 0) {
        container.innerHTML = `
            <div class="empty-strip-box">
                <span class="empty-icon">${emptyIcon}</span>
                <span>${emptyMessage}</span>
            </div>
        `;
        return;
    }

    container.innerHTML = '';
    recommendations.forEach(rec => {
        const game = rec.Game || rec.game;
        if (!game) return;

        const card = buildPlaylistCard(game);

        // Inject match percentage and top matching reason into the card metadata area
        const metaEl = card.querySelector('.game-card-meta');
        if (metaEl) {
            const percentage = rec.MatchPercentage ?? rec.matchPercentage ?? 0;
            const reasons = rec.MatchReasons || rec.matchReasons || [];
            const primaryReason = reasons.length > 0 ? reasons : '';

            metaEl.innerHTML = `
                <div style="display:flex; flex-direction:column; gap:2px;">
                    <span style="color:#03dac6; font-weight:700; letter-spacing:0.3px;">★ ${percentage}% Match</span>
                    ${primaryReason ? `<span style="font-size:11px; color:#9ca3af; text-overflow:ellipsis; overflow:hidden; white-space:nowrap;" title="${primaryReason}">${primaryReason}</span>` : ''}
                </div>
            `;
        }

        container.appendChild(card);
    });
}

/**
 * Displays an inline error message inside a strip container if an endpoint fails.
 */
function renderStripError(containerId, message) {
    const container = document.getElementById(containerId);
    if (container) {
        container.innerHTML = `<p class="loading-text" style="color:#ff5252;">${message}</p>`;
    }
}

/**
 * Sets up global event listeners so strips automatically recalibrate when games change state.
 */
function setupGlobalSyncEvents() {
    if (window._hasPlaylistViewSync) return;

    window.addEventListener('gamePlaylistStatusChanged', () => {
        if (document.querySelector('.playlist-view-container')) {
            loadAllPlaylistStrips(false);
        }
    });

    window.addEventListener('gameFavoriteChanged', () => {
        if (document.querySelector('.playlist-view-container')) {
            loadAllPlaylistStrips(false);
        }
    });

    window._hasPlaylistViewSync = true;
}
