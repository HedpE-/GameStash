let currentScreenshots = [];
let currentScreenshotIndex = 0;

let currentVideos = [];
let currentVideoIndex = 0;

let currentViewerMode = 'screenshot'; // 'screenshot' | 'video'

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
 * Preloads media assets into the browser cache for seamless transitions.
 */
function preloadMedia(screenshots, videos) {
    if (Array.isArray(screenshots)) {
        screenshots.forEach(shot => {
            const url = shot.HugeRetinaUrl || shot.hugeRetinaUrl || shot.MediumUrl || shot.mediumUrl;
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

/**
 * Orchestrates async loading of target game record.
 * @param {number} gameId - The game identity identifier.
 * @param {string|null} libraryType - Library provider ('local', 'steam', 'epic', or null for external browse).
 */
export async function openGameDetails(gameId, libraryType = "none") {
    try {
        let modalOverlay = document.getElementById('game-details-modal');

        if (!modalOverlay) {
            const templateResponse = await fetch('/gamedetails.html');
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

        // 1. Dynamic Endpoint Selection (JavaScript syntax fixed)
        let endpoint = "";
        switch (libraryType) {
            case "local":
                endpoint = `/api/stash/local/${gameId}/details`;
                break;
            default:
                endpoint = `/api/games/${gameId}`;
        }

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
        const summary = game.Summary || game.summary || 'No summary overview supplied.';
        const storyline = game.Storyline || game.storyline || 'No descriptive storyline details logged.';
        const releaseDate = game.ReleaseDate || game.releaseDate || 'TBD';
        const coverUrl = game.CoverUrl || game.coverUrl || '/css/placeholder.png';
        const developer = game.DeveloperCompany?.Name || game.developerCompany?.name || 'Unknown Developer';
        const developerLogoUrl = game.DeveloperLogoUrl || game.developerLogoUrl || '';
        const platforms = game.Platforms || game.platforms || 'No platforms available';
        const genres = game.Genres || game.genres || 'Not specified';
        const themes = game.Themes || game.themes || 'Not specified';
        const perspectives = game.Perspectives || game.perspectives || 'Not specified';
        const modes = game.Modes || game.modes || 'Not specified';
        const ratingScore = game.Rating || game.rating || 0;
        const ratingCount = game.RatingCount || game.ratingCount || 0;
        const screenshots = game.Screenshots || game.screenshots || [];
        const videos = game.Videos || game.videos || [];
        const similarGames = game.SimilarGames || game.similarGames || [];

        // Apply Common Data
        setText('md-title', title);
        setText('md-summary', summary);
        setText('md-storyline', storyline);
        setText('md-release-date', releaseDate);
        setText('md-developer-company', developer);
        setText('md-platforms', platforms);
        setText('md-genres', genres);
        setText('md-themes', themes);
        setText('md-perspectives', perspectives);
        setText('md-modes', modes);

        const devLogoEl = document.getElementById('md-developer-logo');
        if (devLogoEl) {
            devLogoEl.style.display = developerLogoUrl ? 'block' : 'none';
            devLogoEl.src = developerLogoUrl;
        }

        const coverEl = document.getElementById('md-cover');
        if (coverEl) coverEl.style.backgroundImage = `url('${coverUrl}')`;

        const starsFillElement = document.getElementById('md-stars-fill');
        if (starsFillElement) {
            starsFillElement.style.width = `${Math.max(0, Math.min(100, ratingScore))}%`;
        }

        const ratingMetaEl = document.getElementById('md-rating-meta');
        if (ratingMetaEl && ratingScore != null) {
            ratingMetaEl.innerText = `(${ratingScore.toFixed(1)} / ${ratingCount} ratings)`;
        }

        // 2. Local Library Specific Data Bindings
        const localSidebar = document.getElementById('md-local-sidebar');
        const localSpecs = document.getElementById('md-local-specs');
        const favoriteBtn = document.getElementById('md-favorite-btn');
        const openFolderBtn = document.getElementById('md-open-folder-btn');
        const installBadge = document.getElementById('md-install-status');
        const installText = document.getElementById('md-install-status-text');

        if (libraryType === 'local') {
            if (localSidebar) localSidebar.style.display = 'flex';
            if (localSpecs) localSpecs.style.display = 'flex';
            if (favoriteBtn) favoriteBtn.style.display = 'flex';

            setText('md-added-date', game.DateAddedFormatted || 'TBD');
            setText('md-local-type', game.Type || 'PC Game');
            setText('md-local-path', game.Folder || '');
            setText('md-local-size', game.FolderSizeFormatted || '');

            favoriteBtn.classList.toggle('active', !!game.IsFavorite);

            // Update installation status UI helper
            const updateInstallUI = (isInstalled) => {
                if (installBadge) {
                    installBadge.classList.toggle('installed', isInstalled);
                    installBadge.classList.toggle('not-installed', !isInstalled);
                }
                if (installText) {
                    installText.innerText = isInstalled ? 'Installed' : 'Not Installed';
                }
            };

            updateInstallUI(!!game.IsInstalled);

            // Wire Installation Status Toggle Click matching route /api/stash/{type}/{id}/installed
            if (installBadge) {
                installBadge.onclick = async (e) => {
                    e.stopPropagation();
                    const newInstalledState = !game.IsInstalled;
                    game.IsInstalled = newInstalledState;
                    updateInstallUI(newInstalledState);

                    await fetch(`/api/stash/${encodeURIComponent(libraryType)}/${gameId}/installed`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(newInstalledState)
                    });
                };
            }

            // Wire Favorite Toggle Button matching route /api/stash/{type}/{id}/favorite
            favoriteBtn.onclick = async (e) => {
                e.stopPropagation();
                const newFavoriteState = !game.IsFavorite;
                game.IsFavorite = newFavoriteState;
                favoriteBtn.classList.toggle('active', newFavoriteState);

                await fetch(`/api/stash/${encodeURIComponent(libraryType)}/${gameId}/favorite`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(newFavoriteState)
                });
            };

            // Wire Open Folder Button matching route /api/stash/{type}/{id}/open-folder
            if (openFolderBtn) {
                openFolderBtn.onclick = async () => {
                    await fetch(`/api/stash/${encodeURIComponent(libraryType)}/${gameId}/open-folder`, {
                        method: 'POST'
                    });
                };
            }
        } else {
            // Non-local or generic catalog view: hide local controls
            if (localSidebar) localSidebar.style.display = 'none';
            if (localSpecs) localSpecs.style.display = 'none';
            if (favoriteBtn) favoriteBtn.style.display = 'none';
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
                const thumbUrl = vid.ThumbnailUrl || vid.thumbnailUrl || (videoId ? `https://img.youtube.com/vi/${videoId}/hqdefault.jpg` : '/css/placeholder.png');

                const item = document.createElement('div');
                item.className = 'video-item';
                item.style.backgroundImage = `url('${thumbUrl}')`;
                item.title = vidTitle;
                item.innerHTML = `
                    <div class="video-play-badge">▶</div>
                    <div class="video-title-bar">${vidTitle}</div>
                `;
                item.addEventListener('click', () => openVideoViewer(index));
                videoGrid.appendChild(item);
            });
        } else {
            videoGrid.innerHTML = '<p class="loading-text">No video media records logged.</p>';
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
            screenshotGrid.innerHTML = '<p class="loading-text">No active graphical screenshots found.</p>';
        }
    }

    // Render Similar Games Grid
    const similarGrid = document.getElementById('md-similar');
    if (similarGrid) {
        similarGrid.innerHTML = '';
        if (similarGames.length > 0) {
            similarGames.forEach(sim => {
                const simId = sim.Id || sim.id;
                const simName = sim.Name || sim.name || 'Unknown';
                const simCover = sim.CoverUrl || sim.coverUrl || '/css/placeholder.png';

                const simCard = document.createElement('div');
                simCard.className = 'game-card';
                simCard.innerHTML = `
                    <div class="game-cover" style="background-image: url('${simCover}');"></div>
                    <div class="game-title" title="${simName}">${simName}</div>`;
                simCard.addEventListener('click', () => openGameDetails(simId, null));
                similarGrid.appendChild(simCard);
            });
        } else {
            similarGrid.innerHTML = '<p class="loading-text">No similar catalog items mapped.</p>';
        }
    }
}

/**
 * Closes the primary dashboard modal overlay layer.
 */
export function closeGameDetails() {
    const modal = document.getElementById('game-details-modal');
    if (modal) modal.classList.remove('active');

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

    if (backBtn && forwardBtn) {
        backBtn.style.display = historyPointer > 0 ? 'flex' : 'none';
        forwardBtn.style.display = historyPointer < historyStack.length - 1 ? 'flex' : 'none';
    }
}

function setupHistoryTrackingEvents() {
    const backBtn = document.getElementById('md-nav-back');
    const forwardBtn = document.getElementById('md-nav-forward');

    if (backBtn) backBtn.addEventListener('click', navigateHistoryBack);
    if (forwardBtn) forwardBtn.addEventListener('click', navigateHistoryForward);
}

export function switchTab(tabId) {
    document.querySelectorAll('.modal-tab-content').forEach(tab => tab.classList.remove('active'));
    document.querySelectorAll('.modal-tab-link').forEach(link => link.classList.remove('active'));

    const activeContent = document.getElementById(tabId);
    if (activeContent) activeContent.classList.add('active');

    const tabName = tabId.replace('tab-', '').replace('-', ' ').trim();
    const activeLink = Array.from(document.querySelectorAll('.modal-tab-link'))
        .find(link => link.innerText.toLowerCase().trim() === tabName);
    if (activeLink) activeLink.classList.add('active');
}

/**
 * Resets the zoom transform of the lightbox screenshot.
 */
function resetZoom() {
    currentZoom = 1;
    const imgElement = document.getElementById('viewer-img');
    if (imgElement) {
        imgElement.style.transform = `scale(1)`;
    }
}

/**
 * Updates the top caption header for current screenshot or video.
 */
function updateViewerTitle() {
    const titleEl = document.getElementById('viewer-title');
    if (!titleEl) return;

    if (currentViewerMode === 'screenshot') {
        const shot = currentScreenshots[currentScreenshotIndex];
        const imageId = shot?.ImageId || shot?.imageId || `Screenshot ${currentScreenshotIndex + 1}`;
        titleEl.innerText = imageId;
        titleEl.style.display = 'block';
    } else if (currentViewerMode === 'video') {
        const vid = currentVideos[currentVideoIndex];
        const vidName = vid?.Name || vid?.name || vid?.VideoId || vid?.videoId || `Video ${currentVideoIndex + 1}`;
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
    currentScreenshotIndex = (currentScreenshotIndex + direction + currentScreenshots.length) % currentScreenshots.length;
    resetZoom();
    updateViewerImage();
    updateViewerTitle();
}

export function navigateVideo(direction) {
    if (currentVideos.length === 0) return;
    currentVideoIndex = (currentVideoIndex + direction + currentVideos.length) % currentVideos.length;
    updateViewerVideo();
    updateViewerTitle();
}

function updateViewerImage() {
    const imgElement = document.getElementById('viewer-img');
    if (imgElement && currentScreenshots[currentScreenshotIndex]) {
        const shot = currentScreenshots[currentScreenshotIndex];
        const hugeUrl = shot.HugeRetinaUrl || shot.hugeRetinaUrl || shot.MediumUrl || shot.mediumUrl;
        if (hugeUrl) imgElement.src = hugeUrl;
    }
}

function updateViewerVideo() {
    const videoEl = document.getElementById('viewer-video');
    const video = currentVideos[currentVideoIndex];
    if (videoEl && video) {
        const videoId = video.VideoId || video.videoId;
        const embedUrl = video.YouTubeEmbedUrl || video.youTubeEmbedUrl || video.EmbedUrl || video.embedUrl || (videoId ? `https://www.youtube.com/embed/${videoId}?autoplay=1&rel=0` : '');
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
    if (closeBtn) closeBtn.addEventListener('click', closeMediaViewer);
    const prevBtn = document.querySelector('.viewer-nav-btn.prev');
    if (prevBtn) prevBtn.addEventListener('click', () => navigateMedia(-1));
    const nextBtn = document.querySelector('.viewer-nav-btn.next');
    if (nextBtn) nextBtn.addEventListener('click', () => navigateMedia(1));

    // Mouse wheel zoom for screenshots
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
    const modalCloseBtn = document.querySelector('.modal-header-close');
    if (modalCloseBtn) modalCloseBtn.addEventListener('click', closeGameDetails);
    const tabLinks = document.querySelectorAll('.modal-tab-link');
    tabLinks.forEach(link => {
        link.addEventListener('click', () => {
            const normalizedTabName = link.innerText.toLowerCase().trim().replace(/\s+/g, '-');
            const targetId = `tab-${normalizedTabName}`;
            switchTab(targetId);
        });
    });
}

// Window attachments for template backwards compatibility
window.switchTab = switchTab;
window.closeGameDetails = closeGameDetails;