/**
 * Steam Stash View Controller
 */

import { createGameCard } from './gamecard.js';
import { openGameDetails } from './gamedetails.js';
import { renderAlphabetBar } from './alphabetbar.js';

let steamGames = [];
let filteredGames = [];
let currentSortOption = 'Name';
let sortDirection = 'asc';
let currentFilterOption = 'All';

/**
 * Initializes the Steam Stash view lifecycle.
 * @param {HTMLElement} workspaceElement - Container element.
 */
export async function initSteamStashView(workspaceElement) {
    setupToolbarEvents();
    setupScrollTopEvent();
    await loadSteamStashData(false);
}

/**
 * Fetches Steam games from backend and renders the grid.
 * @param {boolean} forceRefresh - If true, bypasses in-memory cache and re-queries Steam Web API.
 */
async function loadSteamStashData(forceRefresh = false) {
    const loader = document.getElementById('steam-loading-indicator');
    const workspace = document.getElementById('steam-workspace');
    const countLabel = document.getElementById('steam-count-label');

    if (loader) loader.style.display = 'flex';
    if (workspace) workspace.style.display = 'none';
    if (countLabel) countLabel.innerText = 'Loading games...';

    document.querySelector('.stash-empty-warning-box')?.remove();

    try {
        const url = `/api/stash/steam${forceRefresh ? '?refresh=true' : ''}`;
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(`HTTP Error ${response.status}`);
        }

        steamGames = await response.json();

        if (!steamGames || steamGames.length === 0) {
            renderSteamWarning('NoGamesFound');
            return;
        }

        applyFilterAndSort();
    } catch (err) {
        console.error("Failed to load Steam stash data:", err);
        renderSteamWarning('Error', err.message);
    } finally {
        if (loader) loader.style.display = 'none';
        if (workspace && !document.querySelector('.stash-empty-warning-box')) {
            workspace.style.display = 'flex';
        }
    }
}

function getPlaytimeMinutes(game) {
    return game.PlaytimeMinutes ?? game.playtimeMinutes ?? 0;
}

function formatPlaytimeHours(minutes) {
    if (!minutes || minutes <= 0) return 'Unplayed';
    const hours = (minutes / 60).toFixed(1);
    return `${hours} hrs played`;
}

function setupToolbarEvents() {
    document.getElementById('steam-reload-btn')?.addEventListener('click', () => loadSteamStashData(false));
    document.getElementById('steam-sync-btn')?.addEventListener('click', () => loadSteamStashData(true));

    document.getElementById('steam-export-btn')?.addEventListener('click', async () => {
        const textLines = steamGames.map(g => {
            const playtime = formatPlaytimeHours(getPlaytimeMinutes(g));
            const release = g.ReleaseDate || g.releaseDate || 'TBD';
            return `${g.Name || g.name} (${playtime} | Released: ${release})`;
        });

        try {
            await navigator.clipboard.writeText(textLines.join('\n'));
            alert('Steam library list successfully copied to clipboard!');
        } catch {
            alert('Failed to copy list to clipboard.');
        }
    });

    const sortToggle = document.getElementById('steam-sort-toggle');
    const sortMenu = document.getElementById('steam-sort-menu');
    sortToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        sortMenu.style.display = sortMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#steam-sort-menu .dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            e.stopPropagation();
            const option = item.getAttribute('data-sort');
            if (currentSortOption === option) {
                sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
                currentSortOption = option;
                sortDirection = option === 'Playtime' ? 'desc' : 'asc';
            }
            updateSortUI();
            applyFilterAndSort();
            sortMenu.style.display = 'none';
        });
    });

    const filterToggle = document.getElementById('steam-filter-toggle');
    const filterMenu = document.getElementById('steam-filter-menu');
    filterToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        filterMenu.style.display = filterMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#steam-filter-menu .dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            e.stopPropagation();
            currentFilterOption = item.getAttribute('data-filter');
            updateFilterUI();
            applyFilterAndSort();
            filterMenu.style.display = 'none';
        });
    });

    window.addEventListener('click', () => {
        if (sortMenu) sortMenu.style.display = 'none';
        if (filterMenu) filterMenu.style.display = 'none';
    });
}

function updateSortUI() {
    const label = document.getElementById('steam-sort-label');
    const arrow = sortDirection === 'asc' ? '↑' : '↓';
    if (label) label.innerText = `Sort: ${currentSortOption} ${arrow}`;

    document.querySelectorAll('#steam-sort-menu .dropdown-item').forEach(item => {
        const opt = item.getAttribute('data-sort');
        const indicator = item.querySelector('.sort-indicator');
        if (opt === currentSortOption) {
            item.classList.add('active');
            if (indicator) indicator.innerText = arrow;
        } else {
            item.classList.remove('active');
            if (indicator) indicator.innerText = '';
        }
    });
}

function updateFilterUI() {
    const label = document.getElementById('steam-filter-label');
    if (label) label.innerText = `Filter: ${currentFilterOption}`;

    document.querySelectorAll('#steam-filter-menu .dropdown-item').forEach(item => {
        item.classList.toggle('active', item.getAttribute('data-filter') === currentFilterOption);
    });
}

function applyFilterAndSort() {
    // 1. Filtering using PlaytimeMinutes and IsInstalled
    filteredGames = steamGames.filter(game => {
        const isInstalled = !!(game.IsInstalled ?? game.isInstalled);
        const minutes = getPlaytimeMinutes(game);

        switch (currentFilterOption) {
            case 'Installed': return isInstalled;
            case 'Not Installed': return !isInstalled;
            case 'Played': return minutes > 0;
            case 'Unplayed': return minutes === 0;
            case 'All':
            default: return true;
        }
    });

    // 2. Sorting numerically on PlaytimeMinutes or naturally on Name
    const naturalCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

    filteredGames.sort((a, b) => {
        let valA, valB;
        switch (currentSortOption) {
            case 'Playtime':
                valA = getPlaytimeMinutes(a);
                valB = getPlaytimeMinutes(b);
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;

            case 'Release Date':
                valA = a.ReleaseDate ? new Date(a.ReleaseDate).getTime() : 0;
                valB = b.ReleaseDate ? new Date(b.ReleaseDate).getTime() : 0;
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;

            case 'Name':
            default:
                valA = (a.SortName || a.Name || a.name || '').toLowerCase();
                valB = (b.SortName || b.Name || b.name || '').toLowerCase();
                return sortDirection === 'asc'
                    ? naturalCollator.compare(valA, valB)
                    : naturalCollator.compare(valB, valA);
        }
    });

    // 3. Counter display
    const countLabel = document.getElementById('steam-count-label');
    if (countLabel) {
        const total = steamGames.length;
        const filtered = filteredGames.length;
        if (currentFilterOption === 'All') {
            countLabel.innerText = `${total} ${total === 1 ? 'game' : 'games'} in Steam library`;
        } else {
            countLabel.innerText = `Showing ${filtered} of ${total} games (${currentFilterOption.toLowerCase()})`;
        }
    }

    renderGrid();
    renderAlphabetBar({
        barId: 'steam-alphabet-bar',
        workspaceId: 'steam-workspace',
        games: filteredGames.map(g => ({ ...g, Name: g.SortName || g.Name || g.name })),
        sortOption: currentSortOption
    });
}

function renderGrid() {
    const grid = document.getElementById('steam-games-grid');
    if (!grid) return;
    grid.innerHTML = '';

    filteredGames.forEach(game => {
        const igdbId = game.Id ?? game.id;
        const externalAppId = game.ExternalAppId ?? game.externalAppId;
        const minutes = getPlaytimeMinutes(game);
        const playtimeText = formatPlaytimeHours(minutes);

        const cardGame = {
            ...game,
            Id: igdbId,
            ExternalAppId: externalAppId,
            // Subtitle on card shows playtime formatted cleanly
            ReleaseYear: playtimeText
        };

        const card = createGameCard(
            cardGame,
            'Name', // displays ReleaseYear (playtimeText)
            null,
            null,
            null,
            'steam'
        );

        // Click opens full IGDB details modal directly using the IGDB ID
        card.addEventListener('click', (e) => {
            if (e.target.closest('.game-card-favorite-btn') ||
                e.target.closest('.game-card-menu-btn') ||
                e.target.closest('.game-card-context-menu') ||
                e.target.closest('.game-card-missing-delete-btn')) {
                return; // Let the context menu or favorite button handle the event normally
            }

            e.stopImmediatePropagation();
            if (igdbId && igdbId > 0) {
                openGameDetails(igdbId, 'steam');
            } else if (externalAppId) {
                openGameDetails(externalAppId, 'steam');
            }
        }, true);

        grid.appendChild(card);
    });
}

function setupScrollTopEvent() {
    const workspace = document.getElementById('steam-workspace');
    const scrollTopBtn = document.getElementById('steam-scroll-top-btn');
    const alphabetBar = document.getElementById('steam-alphabet-bar');

    if (!workspace || !scrollTopBtn) return;

    const handleWorkspaceScroll = () => {
        if (workspace.scrollTop > 300) {
            scrollTopBtn.classList.add('visible');
        } else {
            scrollTopBtn.classList.remove('visible');
            scrollTopBtn.classList.remove('shifted-left');
            return;
        }

        if (alphabetBar && window.getComputedStyle(alphabetBar).display !== 'none') {
            const btnRect = scrollTopBtn.getBoundingClientRect();
            const barRect = alphabetBar.getBoundingClientRect();

            if (btnRect.top <= barRect.bottom && btnRect.bottom >= barRect.top) {
                scrollTopBtn.classList.add('shifted-left');
            } else {
                scrollTopBtn.classList.remove('shifted-left');
            }
        } else {
            scrollTopBtn.classList.remove('shifted-left');
        }
    };

    workspace.addEventListener('scroll', handleWorkspaceScroll);
    window.addEventListener('resize', handleWorkspaceScroll);

    scrollTopBtn.addEventListener('click', () => {
        workspace.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
    });
}

function renderSteamWarning(scenario, detailMessage = '') {
    const container = document.querySelector('.stash-container');
    const loader = document.getElementById('steam-loading-indicator');
    const workspace = document.getElementById('steam-workspace');
    const countLabel = document.getElementById('steam-count-label');

    if (loader) loader.style.display = 'none';
    if (workspace) workspace.style.display = 'none';

    document.querySelector('.stash-empty-warning-box')?.remove();

    let icon = '⚛️';
    let title = 'No Steam Games Found';
    let text = 'Unable to resolve any games from your Steam account.';
    let subtext = 'Ensure your SteamID64 and Steam Web API Key are configured in the <strong>Global Settings</strong> panel.';
    let btnText = 'Go to Settings';

    if (scenario === 'Error') {
        icon = '⚠️';
        title = 'Failed to Load Steam Stash';
        text = detailMessage || 'A network error occurred while communicating with the backend Steam service.';
    }

    if (countLabel) countLabel.innerText = title;

    const warningBox = document.createElement('div');
    warningBox.className = 'stash-empty-warning-box';
    warningBox.innerHTML = `
        <div class="warning-icon">${icon}</div>
        <h2>${title}</h2>
        <p>${text}</p>
        <p class="warning-subtext">${subtext}</p>
        <button class="nav-to-settings-btn" id="steam-warn-redirect-btn">${btnText}</button>
    `;

    container.appendChild(warningBox);

    document.getElementById('steam-warn-redirect-btn')?.addEventListener('click', () => {
        import('./router.js').then(m => m.navigateTo('settings'));
    });
}