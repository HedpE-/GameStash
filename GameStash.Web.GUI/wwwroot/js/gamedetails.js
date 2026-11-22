import { showSuccess, showError } from './snackbar.js';
import { createGameCard } from './gamecard.js';
import { renderStarRating } from './starrating.js';
import { openEditModal, openDeleteModal } from './stash.js';

let activeModalContext = null;

let currentScreenshots = [];
let currentScreenshotIndex = 0;

let currentVideos = [];
let currentVideoIndex = 0;

let currentViewerMode = 'screenshot'; // 'screenshot' | 'video'

let revealHiddenAchievements = false;

// Screenshot Zoom State
let currentZoom = 1;
const minZoom = 1;
const maxZoom = 4;
const zoomStep = 0.15;

// In-Memory Caches
const gameCache = new Map();
const imageCache = new Set();

// Navigation tracking state
let historyStack = [];
let historyPointer = -1;
let isInternalNavigation = false;

/**
 * Orchestrates async loading of target game record.
 *
 * @param {number} gameId - The game identity identifier.
 * @param {string|null} libraryType - Library provider ('local', 'steam', 'epic', 'gog', 'amazon', or null for external browse).
 * @param {object|null} initialData - Optional hint with pre-loaded state (e.g. { isInPlaylist: true }).
 */
export async function openGameDetails(gameId, libraryType = "none", initialData = null) {
    try {
        ensureModalGlobalListeners();
        let modalOverlay = document.getElementById('game-details-modal');

        // If the modal does not exist or is an outdated version missing new tabs, re-fetch it
        if (!modalOverlay ||
            !document.getElementById('tab-link-similar-games') ||
            !document.getElementById('tab-link-same-franchise') ||
            !document.getElementById('tab-link-achievements') ||
            !document.getElementById('md-franchise')) {

            if (modalOverlay) {
                modalOverlay.remove();
                document.getElementById('media-viewer')?.remove();
            }

            const templateResponse = await fetch(`/gamedetails.html?v=${Date.now()}`);
            if (!templateResponse.ok) return;

            const templateHtml = await templateResponse.text();
            document.body.insertAdjacentHTML('beforeend', templateHtml);
            modalOverlay = document.getElementById('game-details-modal');

            setupMediaViewerEvents();
            setupModalCloseEvents();
            setupHistoryTrackingEvents();
        }

        if (!isInternalNavigation) {
            if (historyPointer < historyStack.length - 1) {
                historyStack = historyStack.slice(0, historyPointer + 1);
            }

            historyStack.push({ gameId, libraryType });
            historyPointer++;
        }

        // 1. Dynamic Endpoint Selection supporting external stashes (Steam, etc.)
        const endpoint = (!libraryType || libraryType === 'none')
            ? `/api/games/${gameId}`
            : `/api/stash/${libraryType}/${gameId}/details`;

        const response = await fetch(endpoint);
        if (!response.ok) return;

        const game = await response.json();

        // Safe DOM assignment helper
        const setText = (id, text) => {
            const el = document.getElementById(id);
            if (el) el.innerText = text;
        };

        // Extract Common Base IGDB Properties
        const title = game.Name || game.name || 'Unknown Title';
        const platforms = game.Platforms || game.platforms || 'Unknown platforms';
        const summary = game.Summary || game.summary || 'No summary overview supplied.';
        const storyline = game.Storyline || game.storyline || 'No descriptive storyline details logged.';
        const releaseDate = game.ReleaseDate || game.releaseDate || 'TBD';
        const coverUrl = game.CoverUrl || game.coverUrl || '/css/placeholder.png';
        const developer = game.DeveloperCompany?.Name || game.developerCompany?.name || 'Unknown Developer';
        const developerLogoUrl = game.DeveloperLogoUrl || game.developerLogoUrl || '';
        const genres = game.Genres || game.genres || 'Not specified';
        const themes = game.Themes || game.themes || 'Not specified';
        const perspectives = game.Perspectives || game.perspectives || 'Not specified';
        const modes = game.Modes || game.modes || 'Not specified';
        const franchise = game.Franchise || game.franchise || '';
        const franchiseGamesIds = game.FranchiseGamesIds || game.franchiseGamesIds || [];
        const ratingScore = game.Rating || game.rating || 0;
        const ratingCount = game.RatingCount || game.ratingCount || 0;
        const screenshots = game.Screenshots || game.screenshots || [];
        const videos = game.Videos || game.videos || [];
        const similarGames = game.SimilarGames || game.similarGames || [];

        // Apply Common Data
        setText('md-title', title);
        setText('md-title-platforms', platforms);
        setText('md-summary', summary);
        setText('md-storyline', storyline);
        setText('md-release-date', releaseDate);
        setText('md-developer-company', developer);
        setText('md-platforms', platforms);
        setText('md-genres', genres);
        setText('md-themes', themes);
        setText('md-perspectives', perspectives);
        setText('md-modes', modes);

        // Franchise field assignment
        setText('md-franchise', franchise ? franchise : 'No franchise');

        // Playtime field assignment (shown for managed stashes that track playtime)
        const playtimeRow = document.getElementById('md-playtime-row');
        const playtimeMinutes = game.PlaytimeMinutes ?? game.playtimeMinutes ?? null;
        const playtimeSupportedStores = ['steam', 'epic', 'gog'];

        if (playtimeRow) {
            if (libraryType && playtimeSupportedStores.includes(libraryType.toLowerCase()) && playtimeMinutes !== null) {
                setText('md-playtime', formatDetailedPlaytime(playtimeMinutes));
                playtimeRow.style.display = 'block';
            } else {
                playtimeRow.style.display = 'none';
            }
        }

        const devLogoEl = document.getElementById('md-developer-logo');
        if (devLogoEl) {
            devLogoEl.style.display = developerLogoUrl ? 'block' : 'none';
            devLogoEl.src = developerLogoUrl;
        }

        const coverEl = document.getElementById('md-cover');
        if (coverEl) coverEl.style.backgroundImage = `url('${coverUrl}')`;

        // Star Rating Component
        const ratingContainer = document.getElementById('md-rating-container');
        if (ratingContainer) {
            ratingContainer.innerHTML = '';
            if (ratingScore > 0) {
                const starsHtml = renderStarRating(ratingScore, { showValue: true, decimals: 1 });
                const countHtml = `<span class="meta-count">(${ratingCount} ratings)</span>`;
                ratingContainer.innerHTML = `${starsHtml} ${countHtml}`;
                ratingContainer.style.display = 'flex';
            } else {
                ratingContainer.innerHTML = '<span class="meta-count">No ratings available</span>';
            }
        }

        // Same Franchise Tab Management (Using franchise game IDs array)
        const franchiseTabLink = document.getElementById('tab-link-same-franchise');
        const franchiseGrid = document.getElementById('md-franchise-games');
        const franchiseCountEl = document.getElementById('md-franchise-count');

        const otherFranchiseGameIds = (franchiseGamesIds || [])
            .map(id => Number(id))
            .filter(id => id > 0 && String(id) !== String(gameId));

        if (otherFranchiseGameIds.length > 0) {
            if (franchiseTabLink) franchiseTabLink.style.display = 'block';
            if (franchiseCountEl) franchiseCountEl.innerText = `(${otherFranchiseGameIds.length})`;
            loadFranchiseGames(otherFranchiseGameIds, gameId);
        } else {
            if (franchiseTabLink) franchiseTabLink.style.display = 'none';
            if (franchiseCountEl) franchiseCountEl.innerText = '';
            if (franchiseGrid) franchiseGrid.innerHTML = '';
            const platformSelect = document.getElementById('md-franchise-platform-select');
            if (platformSelect) platformSelect.style.display = 'none';
        }

        // Storefront Achievements Tab Management (Steam, Epic, GOG, Amazon)
        const achievementsTabLink = document.getElementById('tab-link-achievements');
        const supportedAchievementStores = ['steam', 'epic', 'gog', 'amazon'];

        if (libraryType && supportedAchievementStores.includes(libraryType.toLowerCase())) {
            if (achievementsTabLink) achievementsTabLink.style.display = 'block';
            loadStoreAchievements(libraryType.toLowerCase(), gameId);
        } else {
            if (achievementsTabLink) achievementsTabLink.style.display = 'none';
            const achList = document.getElementById('md-achievements-list');
            const achSummary = document.getElementById('md-achievements-summary');
            const achTabCount = document.getElementById('md-achievements-tab-count');
            if (achList) achList.innerHTML = '';
            if (achSummary) achSummary.style.display = 'none';
            if (achTabCount) achTabCount.innerText = '';
        }

        // 2. Library Management Sidebar Elements Bindings
        const localSidebar = document.getElementById('md-local-sidebar');
        const localSpecs = document.getElementById('md-local-specs');
        const favoriteBtn = document.getElementById('md-favorite-btn');
        const playlistBtn = document.getElementById('md-floating-playlist-btn');
        const installBadge = document.getElementById('md-install-status');

        const playingBadge = document.getElementById('md-playing-status');
        const playedBadge = document.getElementById('md-played-status');
        const playedToEndBadge = document.getElementById('md-played-to-end-status');
        const playlistBadge = document.getElementById('md-playlist-status');

        const editSortNameBtn = document.getElementById('md-edit-sort-name-btn');
        const openFolderBtn = document.getElementById('md-open-folder-btn');
        const removeStashBtn = document.getElementById('md-remove-stash-btn');

        // Check if library is managed (Local Stash or Store Stashes)
        const isManagedLibrary = Boolean(libraryType) && libraryType !== 'none';

        if (isManagedLibrary) {
            if (localSidebar) localSidebar.style.display = 'flex';

            // Disk path & size specs are exclusive to Local Stash
            if (localSpecs) localSpecs.style.display = libraryType === 'local' ? 'flex' : 'none';

            // Playlist controls are supported across all managed stashes
            if (playlistBtn) playlistBtn.style.display = 'flex';
            if (playlistBadge) playlistBadge.style.display = 'flex';

            // Favorite button is supported across managed libraries
            if (favoriteBtn) favoriteBtn.style.display = 'flex';

            // Progress badges (Currently Playing, Have Played, Played to End)
            if (playingBadge) playingBadge.style.display = 'flex';
            if (playedBadge) playedBadge.style.display = 'flex';
            if (playedToEndBadge) playedToEndBadge.style.display = 'flex';

            const updateProgressUI = () => {
                const isPlaying = !!(game.IsPlaying ?? game.isPlaying);
                const isPlayed = !!(game.IsPlayed ?? game.isPlayed);
                let playedToEnd = !!(game.PlayedToEnd ?? game.playedToEnd);

                const canComplete = isPlaying || isPlayed;
                if (!canComplete && playedToEnd) {
                    playedToEnd = false;
                    game.PlayedToEnd = false;
                    game.playedToEnd = false;
                }

                if (playingBadge) {
                    playingBadge.classList.toggle('active', isPlaying);
                    playingBadge.classList.toggle('status-playing', isPlaying);
                }
                if (playedBadge) {
                    playedBadge.classList.toggle('active', isPlayed);
                    playedBadge.classList.toggle('status-played', isPlayed);
                }
                if (playedToEndBadge) {
                    const isFinishedActive = canComplete && playedToEnd;
                    playedToEndBadge.classList.toggle('disabled', !canComplete);
                    playedToEndBadge.classList.toggle('active', isFinishedActive);
                    playedToEndBadge.classList.toggle('status-finished', isFinishedActive);
                }
            };

            const persistProgressProperty = async (propertyName, value) => {
                const detailsUrl = `/api/stash/${libraryType}/${gameId}/details`;
                try {
                    const res = await fetch(detailsUrl, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ propertyName, value })
                    });
                    if (!res.ok) {
                        showError(`Failed to update ${propertyName}.`);
                    } else {
                        window.dispatchEvent(new CustomEvent('gameProgressChanged', {
                            detail: {
                                gameId,
                                isPlaying: !!game.IsPlaying,
                                isPlayed: !!game.IsPlayed,
                                playedToEnd: !!game.PlayedToEnd
                            }
                        }));
                    }
                } catch {
                    showError('Network error while updating game progress.');
                }
            };

            updateProgressUI();

            if (playingBadge) {
                playingBadge.onclick = async (e) => {
                    e.stopPropagation();
                    const newPlaying = !game.IsPlaying;
                    const wasPlayed = !!game.IsPlayed;
                    const hadPlayedToEnd = !!game.PlayedToEnd;

                    game.IsPlaying = newPlaying;
                    game.isPlaying = newPlaying;

                    // Mutual exclusivity: IsPlaying = true forces IsPlayed = false
                    if (newPlaying && wasPlayed) {
                        game.IsPlayed = false;
                        game.isPlayed = false;
                    }

                    updateProgressUI();

                    await persistProgressProperty('IsPlaying', newPlaying);
                    if (newPlaying && wasPlayed) {
                        await persistProgressProperty('IsPlayed', false);
                    }
                    if (hadPlayedToEnd && !game.PlayedToEnd) {
                        await persistProgressProperty('PlayedToEnd', false);
                    }
                };
            }

            if (playedBadge) {
                playedBadge.onclick = async (e) => {
                    e.stopPropagation();
                    const newPlayed = !game.IsPlayed;
                    const wasPlaying = !!game.IsPlaying;
                    const hadPlayedToEnd = !!game.PlayedToEnd;

                    game.IsPlayed = newPlayed;
                    game.isPlayed = newPlayed;

                    // Mutual exclusivity: IsPlayed = true forces IsPlaying = false
                    if (newPlayed && wasPlaying) {
                        game.IsPlaying = false;
                        game.isPlaying = false;
                    }

                    updateProgressUI();

                    await persistProgressProperty('IsPlayed', newPlayed);
                    if (newPlayed && wasPlaying) {
                        await persistProgressProperty('IsPlaying', false);
                    }
                    if (hadPlayedToEnd && !game.PlayedToEnd) {
                        await persistProgressProperty('PlayedToEnd', false);
                    }
                };
            }

            if (playedToEndBadge) {
                playedToEndBadge.onclick = async (e) => {
                    e.stopPropagation();
                    if (!game.IsPlaying && !game.IsPlayed) return;

                    const newVal = !game.PlayedToEnd;
                    game.PlayedToEnd = newVal;
                    game.playedToEnd = newVal;
                    updateProgressUI();

                    await persistProgressProperty('PlayedToEnd', newVal);
                };
            }

            // Local-only file management actions visibility
            // 1. Edit Sort Name (Supported across all managed stashes)
            if (editSortNameBtn) {
                editSortNameBtn.style.display = 'block';
                editSortNameBtn.onclick = (e) => {
                    e.stopPropagation();
                    openEditModal({ ...game, stashType: libraryType });
                };
            }

            // 2. Open Game Folder (Local Stash only)
            if (openFolderBtn) {
                openFolderBtn.style.display = libraryType === 'local' ? 'block' : 'none';
                openFolderBtn.onclick = async (e) => {
                    e.stopPropagation();
                    try {
                        const res = await fetch(`/api/stash/${libraryType}/${gameId}/open-folder`, { method: 'POST' });
                        if (!res.ok) {
                            const errData = await res.json().catch(() => ({}));
                            showError(errData.Message || errData.message || "Failed to open game folder.");
                        }
                    } catch {
                        showError("Network error while opening game folder.");
                    }
                };
            }

            // 3. Remove Game from Stash (Local Stash only)
            if (removeStashBtn) {
                removeStashBtn.style.display = libraryType === 'local' ? 'block' : 'none';
                removeStashBtn.onclick = (e) => {
                    e.stopPropagation();
                    openDeleteModal(game, () => closeGameDetails());
                };
            }

            if (libraryType === 'local') {
                setText('md-added-date', game.DateAddedFormatted || 'TBD');
                setText('md-local-type', game.Type || 'PC Game');
                setText('md-local-path', game.Folder || '');
                setText('md-local-size', game.FolderSizeFormatted || '');
            }

            favoriteBtn.classList.toggle('active', !!game.IsFavorite);

            // Installation Switch Helper
            const updateInstallUI = (isInstalled) => {
                if (installBadge) {
                    installBadge.classList.toggle('is-installed-active', isInstalled);
                    installBadge.classList.toggle('is-not-installed-active', !isInstalled);
                }
            };

            // Playlist UI Helper (Local Stash only)
            const updatePlaylistUI = (isInPlaylist) => {
                if (playlistBadge) {
                    playlistBadge.classList.toggle('active', isInPlaylist);
                    playlistBadge.classList.toggle('status-playlist', isInPlaylist);
                    playlistBadge.innerHTML = isInPlaylist
                        ? `<span>📋</span> In Playlist`
                        : `<span>➕</span> Add to Playlist`;
                }

                if (playlistBtn) {
                    playlistBtn.classList.toggle('active', isInPlaylist);
                    playlistBtn.title = isInPlaylist ? "In Playlist (Click to remove)" : "Add to Playlist";
                }
            };

            let resolvedPlaylistStatus = undefined;
            const playlistKeys = ['IsInPlaylist', 'isInPlaylist', 'InPlaylist', 'inPlaylist', 'IsPlaylist', 'isPlaylist'];
            for (const key of playlistKeys) {
                if (typeof game[key] === 'boolean') {
                    resolvedPlaylistStatus = game[key];
                    break;
                }
            }

            if (resolvedPlaylistStatus === undefined && initialData && typeof initialData.isInPlaylist === 'boolean') {
                resolvedPlaylistStatus = initialData.isInPlaylist;
            }

            const initialInPlaylist = !!resolvedPlaylistStatus;
            game.IsInPlaylist = initialInPlaylist;
            game.isInPlaylist = initialInPlaylist;
            updatePlaylistUI(initialInPlaylist);

            const togglePlaylistState = async () => {
                const currentStatus = !!(game.IsInPlaylist || game.isInPlaylist);
                const newStatus = !currentStatus;

                game.IsInPlaylist = newStatus;
                game.isInPlaylist = newStatus;
                updatePlaylistUI(newStatus);

                const method = newStatus ? 'PUT' : 'DELETE';
                const url = `/api/playlist/${libraryType}/${gameId}`;

                try {
                    const res = await fetch(url, { method: method });
                    if (res.ok) {
                        window.dispatchEvent(new CustomEvent('gamePlaylistStatusChanged', {
                            detail: { gameId: gameId, isInPlaylist: newStatus }
                        }));
                    } else {
                        game.IsInPlaylist = currentStatus;
                        game.isInPlaylist = currentStatus;
                        updatePlaylistUI(currentStatus);
                        showError("Failed to update playlist state.");
                    }
                } catch (err) {
                    console.error("Network communication exception during playlist toggle:", err);
                    game.IsInPlaylist = currentStatus;
                    game.isInPlaylist = currentStatus;
                    updatePlaylistUI(currentStatus);
                    showError("Error connecting to server.");
                }
            };

            if (playlistBtn) playlistBtn.onclick = (e) => { e.stopPropagation(); togglePlaylistState(); };
            if (playlistBadge) playlistBadge.onclick = (e) => { e.stopPropagation(); togglePlaylistState(); };

            updateInstallUI(!!game.IsInstalled);

            activeModalContext = {
                gameId,
                game,
                updateInstallUI,
                updatePlaylistUI
            };

            // Wire Install Status Toggle (All managed stashes)
            if (installBadge) {
                installBadge.onclick = async (e) => {
                    e.stopPropagation();
                    const newInstalledState = !game.IsInstalled;
                    game.IsInstalled = newInstalledState;
                    updateInstallUI(newInstalledState);

                    await fetch(`/api/stash/${libraryType}/${gameId}/details`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ propertyName: 'IsInstalled', value: newInstalledState })
                    });

                    window.dispatchEvent(new CustomEvent('gameInstalledChanged', {
                        detail: { gameId: game.Id ?? gameId, isInstalled: newInstalledState }
                    }));
                };
            }

            // Wire Favorite Toggle Button (All managed stashes)
            if (favoriteBtn) {
                favoriteBtn.onclick = async (e) => {
                    e.stopPropagation();
                    const newFavoriteState = !game.IsFavorite;
                    game.IsFavorite = newFavoriteState;

                    favoriteBtn.classList.toggle('active', newFavoriteState);

                    await fetch(`/api/stash/${libraryType}/${gameId}/details`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ propertyName: 'IsFavorite', value: newFavoriteState })
                    });

                    window.dispatchEvent(new CustomEvent('gameFavoriteChanged', {
                        detail: { gameId: game.Id ?? gameId, isFavorite: newFavoriteState }
                    }));
                };
            }
        } else {
            if (localSidebar) localSidebar.style.display = 'none';
            if (localSpecs) localSpecs.style.display = 'none';
            if (playlistBtn) playlistBtn.style.display = 'none';
            if (playlistBadge) playlistBadge.style.display = 'none';
            if (favoriteBtn) favoriteBtn.style.display = 'none';
            activeModalContext = null;
        }

        // Render Media Grids and Similar Games
        preloadMedia(screenshots, videos);
        renderMediaGrids(screenshots, videos, similarGames);

        updateHistoryButtonsUI();
        switchTab('tab-summary');

        modalOverlay.classList.add('active');
    } catch (err) {
        console.error("Critical error rendering game details view:", err);
    } finally {
        isInternalNavigation = false;
    }
}

/**
 * Preloads media assets into the browser cache for seamless transitions.
 */
function preloadMedia(screenshots, videos) {
    if (Array.isArray(screenshots)) {
        screenshots.forEach(shot => {
            const url = shot.HugeRetinaUrl ||
                shot.hugeRetinaUrl ||
                shot.MediumUrl ||
                shot.mediumUrl;

            if (url && !imageCache.has(url)) {
                const img = new Image();
                img.src = url;
                imageCache.add(url);
            }
        });
    }

    if (Array.isArray(videos)) {
        videos.forEach(vid => {
            const thumb = vid.ThumbnailUrl || vid.thumbnailUrl;

            if (thumb && !imageCache.has(thumb)) {
                const img = new Image();
                img.src = thumb;
                imageCache.add(thumb);
            }
        });
    }
}

function formatDetailedPlaytime(totalMinutes) {
    let remaining = Math.floor(Number(totalMinutes) || 0);
    if (remaining <= 0) return 'Unplayed';

    const MINS_PER_HOUR = 60;
    const MINS_PER_DAY = 24 * MINS_PER_HOUR;     // 1440
    const MINS_PER_MONTH = 30 * MINS_PER_DAY;    // 43200
    const MINS_PER_YEAR = 365 * MINS_PER_DAY;    // 525600

    const years = Math.floor(remaining / MINS_PER_YEAR);
    remaining %= MINS_PER_YEAR;

    const months = Math.floor(remaining / MINS_PER_MONTH);
    remaining %= MINS_PER_MONTH;

    const days = Math.floor(remaining / MINS_PER_DAY);
    remaining %= MINS_PER_DAY;

    const hours = Math.floor(remaining / MINS_PER_HOUR);
    const minutes = remaining % MINS_PER_HOUR;

    const parts = [];
    if (years > 0) parts.push(`${years} ${years === 1 ? 'year' : 'years'}`);
    if (months > 0) parts.push(`${months} ${months === 1 ? 'month' : 'months'}`);
    if (days > 0) parts.push(`${days} ${days === 1 ? 'day' : 'days'}`);
    if (hours > 0) parts.push(`${hours} ${hours === 1 ? 'hour' : 'hours'}`);
    if (minutes > 0) parts.push(`${minutes} ${minutes === 1 ? 'minute' : 'minutes'}`);

    return parts.join(' ');
}

/**
 * Fetches and renders normalized achievements for Steam, Epic, GOG, or Amazon.
 *
 * @param {string} store - The library provider ('steam', 'epic', 'gog', 'amazon').
 * @param {number|string} gameId - The game identifier.
 */
async function loadStoreAchievements(store, gameId) {
    const listEl = document.getElementById('md-achievements-list');
    const summaryEl = document.getElementById('md-achievements-summary');
    const tabCountEl = document.getElementById('md-achievements-tab-count');
    const tabLinkEl = document.getElementById('tab-link-achievements');
    const filterSelect = document.getElementById('md-achievements-filter-select');

    if (!listEl) return;

    listEl.innerHTML = '<p class="loading-text">Loading achievements...</p>';
    if (summaryEl) summaryEl.style.display = 'none';
    if (tabCountEl) tabCountEl.innerText = '';

    try {
        const res = await fetch(`/api/games/${encodeURIComponent(store)}/${encodeURIComponent(gameId)}/achievements`);
        if (!res.ok) {
            if (tabLinkEl) tabLinkEl.style.display = 'none';
            listEl.innerHTML = '';
            return;
        }

        const data = await res.json();
        const total = data.TotalAchievements ?? data.totalAchievements ?? 0;
        const unlocked = data.UnlockedAchievements ?? data.unlockedAchievements ?? 0;
        const totalXp = data.TotalXp ?? data.totalXp ?? null;
        const earnedXp = data.EarnedXp ?? data.earnedXp ?? null;
        const items = data.Achievements ?? data.achievements ?? [];

        if (total === 0 || items.length === 0) {
            if (tabLinkEl) tabLinkEl.style.display = 'none';
            listEl.innerHTML = '';
            return;
        }

        if (tabCountEl) tabCountEl.innerText = `(${unlocked}/${total})`;

        const pct = Math.min(100, Math.round((unlocked / total) * 100));
        const progressTextEl = document.getElementById('md-achievements-progress-text');
        const progressFillEl = document.getElementById('md-achievements-progress-fill');
        const xpBadgeEl = document.getElementById('md-achievements-xp-badge');

        if (progressTextEl) progressTextEl.innerText = `${unlocked} / ${total} Unlocked (${pct}%)`;
        if (progressFillEl) progressFillEl.style.width = `${pct}%`;

        if (xpBadgeEl) {
            if (totalXp && totalXp > 0) {
                xpBadgeEl.innerText = `${earnedXp ?? 0} / ${totalXp} XP`;
                xpBadgeEl.style.display = 'inline-flex';
            } else {
                xpBadgeEl.style.display = 'none';
            }
        }

        if (summaryEl) summaryEl.style.display = 'flex';
        revealHiddenAchievements = false;
        const revealBtn = document.getElementById('md-achievements-reveal-btn');
        const hasLockedSecrets = items.some(a => !(a.IsUnlocked ?? a.isUnlocked) && !!(a.IsHidden ?? a.isHidden));

        const getFilteredAchievements = () => {
            const mode = filterSelect ? filterSelect.value : 'all';
            if (mode === 'unlocked') return items.filter(a => !!(a.IsUnlocked ?? a.isUnlocked));
            if (mode === 'locked') return items.filter(a => !(a.IsUnlocked ?? a.isUnlocked));
            return items;
        };

        if (revealBtn) {
            revealBtn.style.display = hasLockedSecrets ? 'inline-flex' : 'none';
            revealBtn.innerText = '👁 Reveal Hidden Achievements';
            revealBtn.onclick = () => {
                revealHiddenAchievements = !revealHiddenAchievements;
                revealBtn.innerText = revealHiddenAchievements ? '🙈 Mask Hidden Achievements' : '👁 Reveal Hidden Achievements';
                renderAchievementsList(listEl, getFilteredAchievements());
            };
        }

        if (filterSelect) {
            filterSelect.value = 'all';
            filterSelect.onchange = () => renderAchievementsList(listEl, getFilteredAchievements());
        }


        renderAchievementsList(listEl, items);
    } catch (err) {
        console.error('Failed to load store achievements:', err);
        if (tabLinkEl) tabLinkEl.style.display = 'none';
        listEl.innerHTML = '';
    }
}

/**
 * Helper function to render normalized achievement items inside the container.
 */
function renderAchievementsList(container, items) {
    container.innerHTML = '';

    if (!items || items.length === 0) {
        container.innerHTML = '<p class="loading-text">No achievements match the selected filter.</p>';
        return;
    }

    items.forEach(item => {
        const isUnlocked = !!(item.IsUnlocked ?? item.isUnlocked);
        const isHidden = !!(item.IsHidden ?? item.isHidden);
        const shouldMask = isHidden && !isUnlocked && !revealHiddenAchievements;

        const name = shouldMask ? 'Hidden Achievement' : ((item.Name ?? item.name ?? 'Achievement') + (isHidden && !isUnlocked ? ' (Hidden)' : ''));
        const desc = shouldMask
            ? 'Details for this achievement will be revealed once unlocked.'
            : (item.Description ?? item.description ?? '');

        const unlockedAt = item.UnlockedAt ?? item.unlockedAt ?? null;
        const rarity = item.RarityPercent ?? item.rarityPercent ?? 0;
        const xp = item.Xp ?? item.xp ?? null;
        const iconUrl = isUnlocked
            ? (item.IconUrl ?? item.iconUrl ?? '/css/placeholder.png')
            : (item.LockedIconUrl ?? item.lockedIconUrl ?? item.IconUrl ?? item.iconUrl ?? '/css/placeholder.png');

        let unlockDateText = '';
        if (isUnlocked && unlockedAt) {
            const parsed = new Date(unlockedAt);
            if (!isNaN(parsed.getTime())) {
                unlockDateText = 'Unlocked ' + parsed.toLocaleDateString();
            }
        }

        const rarityText = rarity > 0 ? Number(rarity).toFixed(1) + '% of players' : '';
        const xpText = xp && xp > 0 ? '+' + xp + ' XP' : '';
        const metaParts = [unlockDateText, rarityText, xpText].filter(Boolean);

        const row = document.createElement('div');
        row.className = 'achievement-row ' + (isUnlocked ? 'unlocked' : 'locked');
        row.innerHTML =
            '<img class="achievement-icon" src="' + escapeHtml(iconUrl) + '" alt="' + escapeHtml(name) + '" loading="lazy" />' +
            '<div class="achievement-info">' +
            '<div class="achievement-header-line">' +
            '<span class="achievement-name">' + escapeHtml(name) + '</span>' +
            '<span class="achievement-status-pill ' + (isUnlocked ? 'is-unlocked' : 'is-locked') + '">' +
            (isUnlocked ? '✓ Unlocked' : '🔒 Locked') +
            '</span>' +
            '</div>' +
            (desc ? '<p class="achievement-desc">' + escapeHtml(desc) + '</p>' : '') +
            (metaParts.length > 0 ? '<div class="achievement-meta">' + escapeHtml(metaParts.join(' • ')) + '</div>' : '') +
            '</div>';

        container.appendChild(row);
    });
}


/**
 * Loads same-franchise games by their IDs using the batch endpoint.
 *
 * @param {number[]} gameIds - Array of game IDs belonging to the franchise.
 * @param {number} currentGameId - The ID of the currently displayed game to exclude.
 */
async function loadFranchiseGames(gameIds, currentGameId) {
    const franchiseGrid = document.getElementById('md-franchise-games');
    const franchiseCountEl = document.getElementById('md-franchise-count');
    const platformSelect = document.getElementById('md-franchise-platform-select');
    if (!franchiseGrid) return;

    const targetIds = (gameIds || [])
        .map(id => Number(id))
        .filter(id => id > 0 && String(id) !== String(currentGameId));

    if (targetIds.length === 0) {
        franchiseGrid.innerHTML = '<p class="loading-text">No other games found in this franchise.</p>';
        if (platformSelect) platformSelect.style.display = 'none';
        return;
    }

    franchiseGrid.innerHTML = '<p class="loading-text">Loading franchise games...</p>';

    try {
        const res = await fetch('/api/games', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(targetIds)
        });

        if (!res.ok) {
            franchiseGrid.innerHTML = 'Failed to load franchise games.';
            return;
        }

        const games = await res.json();
        const otherGames = games.filter(g => String(g.Id || g.id) !== String(currentGameId));

        if (franchiseCountEl) franchiseCountEl.innerText = `(${otherGames.length})`;

        // Extract unique platforms across all franchise games
        const platformMap = new Map();
        otherGames.forEach(item => {
            const platforms = item.Platforms || item.platforms || [];
            platforms.forEach(p => {
                const id = p.Id ?? p.id;
                const name = p.Name ?? p.name;
                if (id && name && !platformMap.has(String(id))) {
                    platformMap.set(String(id), name);
                }
            });
        });

        // Setup the platforms combobox
        if (platformSelect) {
            platformSelect.innerHTML = '<option value="all">All Platforms</option>';

            // Sort platforms alphabetically
            const sortedPlatforms = Array.from(platformMap.entries()).sort((a, b) =>
                a[1].localeCompare(b[1])
            );

            sortedPlatforms.forEach(([platId, platName]) => {
                const option = document.createElement('option');
                option.value = platId;
                option.textContent = platName;
                platformSelect.appendChild(option);
            });

            // Show or hide dropdown depending on platform diversity
            platformSelect.style.display = sortedPlatforms.length > 0 ? 'inline-block' : 'none';

            // Filter on change handler
            platformSelect.onchange = (e) => {
                const selectedVal = e.target.value;
                let filtered = otherGames;

                if (selectedVal !== 'all') {
                    filtered = otherGames.filter(item => {
                        const plats = item.Platforms || item.platforms || [];
                        return plats.some(p => String(p.Id ?? p.id) === selectedVal);
                    });
                }

                if (franchiseCountEl) franchiseCountEl.innerText = `(${filtered.length})`;
                renderFranchiseGameCards(franchiseGrid, filtered);
            };
        }

        // Render initial view (all platforms)
        renderFranchiseGameCards(franchiseGrid, otherGames);

    } catch (err) {
        console.error("Error fetching franchise games:", err);
        franchiseGrid.innerHTML = 'Error loading franchise games.';
    }
}

/**
 * Helper function to render game cards inside the franchise grid.
 */
function renderFranchiseGameCards(container, gamesList) {
    container.innerHTML = '';

    if (!gamesList || gamesList.length === 0) {
        container.innerHTML = '<p class="loading-text">No games found for the selected platform.</p>';
        return;
    }

    gamesList.forEach(item => {
        const releaseRaw = item.ReleaseDate || item.releaseDate || item.ReleaseYear || item.releaseYear || '';
        const releaseYear = releaseRaw.includes(',') ? releaseRaw.split(',').pop().trim() : releaseRaw;

        const mappedGame = {
            Id: item.Id ?? item.id,
            Name: item.Name ?? item.name ?? 'Unknown',
            CoverUrl: item.CoverUrl ?? item.coverUrl ?? '/css/placeholder.png',
            ReleaseYear: releaseYear
        };

        const card = createGameCard(
            mappedGame,
            'Name',
            null,
            null,
            null,
            'none'
        );

        container.appendChild(card);
    });
}

/**
 * Renders screenshots, videos, and similar games grids inside the modal.
 */
function renderMediaGrids(screenshots, videos, similarGames) {
    // Render Videos Grid
    currentVideos = videos;

    const videoGrid = document.getElementById('md-videos');

    if (videoGrid) {
        videoGrid.innerHTML = '';

        if (currentVideos.length > 0) {
            currentVideos.forEach((vid, index) => {
                const videoId = vid.VideoId || vid.videoId || '';
                const vidTitle = vid.Name || vid.name || 'Trailer / Gameplay';

                const thumbUrl = vid.ThumbnailUrl ||
                    vid.thumbnailUrl ||
                    (videoId ? `https://img.youtube.com/vi/${videoId}/hqdefault.jpg` : '/css/placeholder.png');

                const item = document.createElement('div');

                item.className = 'video-item';
                item.style.backgroundImage = `url('${thumbUrl}')`;
                item.title = vidTitle;
                item.innerHTML = `▶ ${vidTitle}`;

                item.addEventListener('click', () => openVideoViewer(index));

                videoGrid.appendChild(item);
            });
        } else {
            videoGrid.innerHTML = 'No video media records logged.';
        }
    }

    // Render Screenshots Grid
    currentScreenshots = screenshots;

    const screenshotGrid = document.getElementById('md-screenshots');

    if (screenshotGrid) {
        screenshotGrid.innerHTML = '';

        if (currentScreenshots.length > 0) {
            currentScreenshots.forEach((shot, index) => {
                const mediumUrl = shot.MediumUrl || shot.mediumUrl || '';

                if (mediumUrl) {
                    const shotItem = document.createElement('div');

                    shotItem.className = 'screenshot-item';
                    shotItem.style.backgroundImage = `url('${mediumUrl}')`;

                    shotItem.addEventListener('click', () => openMediaViewer(index));

                    screenshotGrid.appendChild(shotItem);
                }
            });
        } else {
            screenshotGrid.innerHTML = 'No active graphical screenshots found.';
        }
    }

    // Render Similar Games Grid
    // Render Similar Games Grid
    const similarTabLink = document.getElementById('tab-link-similar-games');
    const similarGrid = document.getElementById('md-similar');

    if (similarGrid) {
        similarGrid.innerHTML = '';

        if (Array.isArray(similarGames) && similarGames.length > 0) {
            if (similarTabLink) similarTabLink.style.display = 'block';

            similarGames.forEach(sim => {
                const mappedGame = {
                    Id: sim.Id || sim.id,
                    Name: sim.Name || sim.name || 'Unknown',
                    CoverUrl: sim.CoverUrl || sim.coverUrl,
                    ReleaseYear: sim.ReleaseYear || sim.releaseYear || ''
                };

                const simCard = createGameCard(
                    mappedGame,
                    'Name',
                    null,
                    null,
                    null,
                    'none'
                );

                similarGrid.appendChild(simCard);
            });
        } else {
            if (similarTabLink) similarTabLink.style.display = 'none';
            similarGrid.innerHTML = 'No similar catalog items mapped.';
        }
    }
}

/**
 * Closes the primary dashboard modal overlay layer.
 */
export function closeGameDetails() {
    document.getElementById('gd-edit-modal')?.classList.remove('active');
    document.getElementById('gd-delete-modal')?.classList.remove('active');

    const modal = document.getElementById('game-details-modal');
    if (modal) {
        modal.classList.remove('active');
    }

    activeModalContext = null;
    historyStack = [];
    historyPointer = -1;
}

export function navigateHistoryBack() {
    if (historyPointer > 0) {
        isInternalNavigation = true;
        historyPointer--;

        const item = historyStack[historyPointer];
        const id = typeof item === 'object' ? item.gameId : item;
        const libraryType = typeof item === 'object' ? item.libraryType : null;

        openGameDetails(id, libraryType);
    }
}

export function navigateHistoryForward() {
    if (historyPointer < historyStack.length - 1) {
        isInternalNavigation = true;
        historyPointer++;

        const item = historyStack[historyPointer];
        const id = typeof item === 'object' ? item.gameId : item;
        const libraryType = typeof item === 'object' ? item.libraryType : null;

        openGameDetails(id, libraryType);
    }
}

function updateHistoryButtonsUI() {
    const backBtn = document.getElementById('md-nav-back');
    const forwardBtn = document.getElementById('md-nav-forward');

    if (backBtn) {
        backBtn.classList.toggle('is-visible', historyPointer > 0);
    }
    if (forwardBtn) {
        forwardBtn.classList.toggle('is-visible', historyPointer < historyStack.length - 1);
    }
}

function setupHistoryTrackingEvents() {
    const backBtn = document.getElementById('md-nav-back');
    const forwardBtn = document.getElementById('md-nav-forward');

    if (backBtn) backBtn.addEventListener('click', navigateHistoryBack);
    if (forwardBtn) forwardBtn.addEventListener('click', navigateHistoryForward);
}

export function switchTab(tabId) {
    document.querySelectorAll('.modal-tab-content').forEach(tab =>
        tab.classList.remove('active')
    );

    document.querySelectorAll('.modal-tab-link').forEach(link =>
        link.classList.remove('active')
    );

    const activeContent = document.getElementById(tabId);

    if (activeContent) {
        activeContent.classList.add('active');
        activeContent.scrollTop = 0;
        if (tabId === 'tab-achievements') {
            const achList = document.getElementById('md-achievements-list');
            if (achList) achList.scrollTop = 0;
        }
    }

    const tabName = tabId.replace('tab-', '').replace(/-/g, ' ').trim();

    const activeLink = Array.from(
        document.querySelectorAll('.modal-tab-link')
    ).find(link => {
        const cleanLabel = link.innerText.replace(/\(\d+\/\d+\)/g, '').toLowerCase().trim();
        return cleanLabel === tabName;
    });

    if (activeLink) {
        activeLink.classList.add('active');
    }
}

/* Resets the zoom transform of the lightbox screenshot. */
function resetZoom() {
    currentZoom = 1;

    const imgElement = document.getElementById('viewer-img');

    if (imgElement) {
        imgElement.style.transform = 'scale(1)';
    }
}

function escapeHtml(str) {
    return String(str || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

/* Updates the top caption header for current screenshot or video. */
function updateViewerTitle() {
    const titleEl = document.getElementById('viewer-title');

    if (!titleEl) return;

    if (currentViewerMode === 'screenshot') {
        const shot = currentScreenshots[currentScreenshotIndex];

        const imageId = shot?.ImageId ||
            shot?.imageId ||
            `Screenshot ${currentScreenshotIndex + 1}`;

        titleEl.innerText = imageId;
        titleEl.style.display = 'block';

    } else if (currentViewerMode === 'video') {
        const vid = currentVideos[currentVideoIndex];

        const vidName = vid?.Name ||
            vid?.name ||
            vid?.VideoId ||
            vid?.videoId ||
            `Video ${currentVideoIndex + 1}`;

        titleEl.innerText = vidName;
        titleEl.style.display = 'block';

    } else {
        titleEl.style.display = 'none';
    }
}

/**
 * Opens full-size lightbox viewer for screenshots.
 */
export function openMediaViewer(index) {
    if (currentScreenshots.length === 0) return;

    currentViewerMode = 'screenshot';
    currentScreenshotIndex = index;

    const viewer = document.getElementById('media-viewer');
    const imgElement = document.getElementById('viewer-img');
    const videoElement = document.getElementById('viewer-video');

    if (!viewer || !imgElement) return;

    if (videoElement) {
        videoElement.src = '';
        videoElement.style.display = 'none';
    }

    imgElement.style.display = 'block';

    resetZoom();
    updateViewerNavButtonsVisibility(currentScreenshots.length);
    updateViewerImage();
    updateViewerTitle();

    viewer.style.display = 'flex';
    viewer.focus();
}

/**
 * Opens full-size lightbox viewer for videos.
 */
export function openVideoViewer(index) {
    if (currentVideos.length === 0) return;

    currentViewerMode = 'video';
    currentVideoIndex = index;

    const viewer = document.getElementById('media-viewer');
    const imgEl = document.getElementById('viewer-img');
    const videoEl = document.getElementById('viewer-video');

    if (!viewer || !videoEl) return;

    if (imgEl) {
        imgEl.style.display = 'none';
        imgEl.src = '';
    }

    videoEl.style.display = 'block';

    resetZoom();
    updateViewerNavButtonsVisibility(currentVideos.length);
    updateViewerVideo();
    updateViewerTitle();

    viewer.style.display = 'flex';
    viewer.focus();
}

/**
 * Closes the media viewer and stops audio/video playback.
 */
export function closeMediaViewer() {
    const viewer = document.getElementById('media-viewer');
    const videoEl = document.getElementById('viewer-video');
    const imgEl = document.getElementById('viewer-img');

    resetZoom();

    if (videoEl) {
        videoEl.src = '';
        videoEl.style.display = 'none';
    }

    if (imgEl) {
        imgEl.style.display = 'none';
    }

    if (viewer) {
        viewer.style.display = 'none';
    }
}

/**
 * Dispatches navigation to the active media collection (Screenshots or Videos).
 */
export function navigateMedia(direction) {
    if (currentViewerMode === 'video') {
        navigateVideo(direction);
    } else {
        navigateScreenshot(direction);
    }
}

export function navigateScreenshot(direction) {
    if (currentScreenshots.length === 0) return;

    currentScreenshotIndex = (
        currentScreenshotIndex + direction + currentScreenshots.length
    ) % currentScreenshots.length;

    resetZoom();
    updateViewerImage();
    updateViewerTitle();
}

export function navigateVideo(direction) {
    if (currentVideos.length === 0) return;

    currentVideoIndex = (
        currentVideoIndex + direction + currentVideos.length
    ) % currentVideos.length;

    updateViewerVideo();
    updateViewerTitle();
}

function updateViewerImage() {
    const imgElement = document.getElementById('viewer-img');

    if (imgElement && currentScreenshots[currentScreenshotIndex]) {
        const shot = currentScreenshots[currentScreenshotIndex];

        const hugeUrl = shot.HugeRetinaUrl ||
            shot.hugeRetinaUrl ||
            shot.MediumUrl ||
            shot.mediumUrl;

        if (hugeUrl) imgElement.src = hugeUrl;
    }
}

function updateViewerVideo() {
    const videoEl = document.getElementById('viewer-video');
    const video = currentVideos[currentVideoIndex];

    if (videoEl && video) {
        const videoId = video.VideoId || video.videoId;

        const embedUrl = video.YouTubeEmbedUrl ||
            video.youTubeEmbedUrl ||
            video.EmbedUrl ||
            video.embedUrl ||
            (videoId ? `https://www.youtube.com/embed/${videoId}?autoplay=1&rel=0` : '');

        videoEl.src = embedUrl;
    }
}

function updateViewerNavButtonsVisibility(count) {
    const prevBtn = document.querySelector('.viewer-nav-btn.prev');
    const nextBtn = document.querySelector('.viewer-nav-btn.next');

    const displayStyle = count > 1 ? 'flex' : 'none';

    if (prevBtn) prevBtn.style.display = displayStyle;
    if (nextBtn) nextBtn.style.display = displayStyle;
}

function setupMediaViewerEvents() {
    window.addEventListener('keydown', (e) => {
        const viewer = document.getElementById('media-viewer');

        if (!viewer || viewer.style.display !== 'flex') return;

        if (e.key === 'ArrowRight') navigateMedia(1);
        if (e.key === 'ArrowLeft') navigateMedia(-1);
        if (e.key === 'Escape') closeMediaViewer();
    });

    const closeBtn = document.querySelector('.viewer-close-btn');

    if (closeBtn) {
        closeBtn.addEventListener('click', closeMediaViewer);
    }

    const prevBtn = document.querySelector('.viewer-nav-btn.prev');

    if (prevBtn) {
        prevBtn.addEventListener('click', () => navigateMedia(-1));
    }

    const nextBtn = document.querySelector('.viewer-nav-btn.next');

    if (nextBtn) {
        nextBtn.addEventListener('click', () => navigateMedia(1));
    }

    const stage = document.getElementById('viewer-stage');

    if (stage) {
        stage.addEventListener('wheel', (e) => {
            if (currentViewerMode !== 'screenshot') return;

            e.preventDefault();

            const imgElement = document.getElementById('viewer-img');

            if (!imgElement || imgElement.style.display === 'none') return;

            if (e.deltaY < 0) {
                currentZoom = Math.min(maxZoom, currentZoom + zoomStep);
            } else {
                currentZoom = Math.max(minZoom, currentZoom - zoomStep);
            }

            imgElement.style.transform = `scale(${currentZoom})`;

        }, { passive: false });
    }
}

function setupModalCloseEvents() {
    const modalOverlay = document.getElementById('game-details-modal');
    const modalCloseBtn = document.querySelector('.modal-header-close');

    if (modalCloseBtn) {
        modalCloseBtn.addEventListener('click', closeGameDetails);
    }

    if (modalOverlay) {
        modalOverlay.addEventListener('click', (e) => {
            if (e.target === modalOverlay) {
                closeGameDetails();
            }
        });
    }

    const tabLinks = document.querySelectorAll('.modal-tab-link');
    tabLinks.forEach(link => {
        link.addEventListener('click', () => {
            const normalizedTabName = link.innerText
                .replace(/\(\d+\/\d+\)/g, '')
                .toLowerCase()
                .trim()
                .replace(/\s+/g, '-');

            const targetId = `tab-${normalizedTabName}`;
            switchTab(targetId);
        });
    });
}

function ensureModalGlobalListeners() {
    if (window._hasGameDetailsGlobalListeners) return;

    window.addEventListener('gameFavoriteChanged', (e) => {
        const { gameId: evGameId, isFavorite } = e.detail;
        if (activeModalContext && String(activeModalContext.gameId) === String(evGameId)) {
            activeModalContext.game.IsFavorite = isFavorite;
            const favBtn = document.getElementById('md-favorite-btn');
            if (favBtn) favBtn.classList.toggle('active', isFavorite);
        }
    });

    window.addEventListener('gameInstalledChanged', (e) => {
        const { gameId: evGameId, isInstalled } = e.detail;
        if (activeModalContext && String(activeModalContext.gameId) === String(evGameId)) {
            activeModalContext.game.IsInstalled = isInstalled;
            activeModalContext.updateInstallUI(isInstalled);
        }
    });

    window.addEventListener('gamePlaylistStatusChanged', (e) => {
        const { gameId: evGameId, isInPlaylist } = e.detail;
        if (activeModalContext && String(activeModalContext.gameId) === String(evGameId)) {
            activeModalContext.game.IsInPlaylist = isInPlaylist;
            activeModalContext.game.isInPlaylist = isInPlaylist;
            activeModalContext.updatePlaylistUI(isInPlaylist);
        }
    });

    window._hasGameDetailsGlobalListeners = true;
}

window.switchTab = switchTab;
window.closeGameDetails = closeGameDetails;