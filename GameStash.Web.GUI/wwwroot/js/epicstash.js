/**
 * Epic Stash View Controller
 */

import { createGameCard } from './gamecard.js';
import { openGameDetails } from './gamedetails.js';
import { renderAlphabetBar } from './alphabetbar.js';

let epicGames = [];
let filteredGames = [];
let currentSortOption = 'Name';
let sortDirection = 'asc';
let currentFilterOption = 'All';

/**
 * Initializes the Epic Stash view lifecycle.
 * @param {HTMLElement} workspaceElement - Container element.
 */
export async function initEpicStashView(workspaceElement) {
    setupToolbarEvents();
    setupScrollTopEvent();
    await loadEpicStashData(false);
}

/**
 * Fetches Epic games from backend and renders the grid.
 * @param {boolean} forceRefresh - If true, bypasses in-memory cache and re-queries Epic API.
 */
async function loadEpicStashData(forceRefresh = false) {
    const loader = document.getElementById('epic-loading-indicator');
    const workspace = document.getElementById('epic-workspace');
    const countLabel = document.getElementById('epic-count-label');

    if (loader) loader.style.display = 'flex';
    if (workspace) workspace.style.display = 'none';
    if (countLabel) countLabel.innerText = 'Loading games...';

    document.querySelector('.stash-empty-warning-box')?.remove();

    try {
        const url = `/api/stash/epic${forceRefresh ? '?refresh=true' : ''}`;
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(`HTTP Error ${response.status}`);
        }

        epicGames = await response.json();

        if (!epicGames || epicGames.length === 0) {
            renderEpicWarning('NoGamesFound');
            return;
        }

        applyFilterAndSort();
    } catch (err) {
        console.error("Failed to load Epic stash data:", err);
        renderEpicWarning('Error', err.message);
    } finally {
        if (loader) loader.style.display = 'none';
        if (workspace && !document.querySelector('.stash-empty-warning-box')) {
            workspace.style.display = 'flex';
        }
    }
}

function setupToolbarEvents() {
    document.getElementById('epic-reload-btn')?.addEventListener('click', () => loadEpicStashData(false));
    document.getElementById('epic-sync-btn')?.addEventListener('click', () => loadEpicStashData(true));

    document.getElementById('epic-export-btn')?.addEventListener('click', async () => {
        const textLines = epicGames.map(g => {
            const release = g.ReleaseDate || g.releaseDate || 'TBD';
            const status = (g.IsInstalled ?? g.isInstalled) ? 'Installed' : 'Not Installed';
            return `${g.Name || g.name} (${status} | Released: ${release})`;
        });

        try {
            await navigator.clipboard.writeText(textLines.join('\n'));
            alert('Epic Games library list successfully copied to clipboard!');
        } catch {
            alert('Failed to copy list to clipboard.');
        }
    });

    const sortToggle = document.getElementById('epic-sort-toggle');
    const sortMenu = document.getElementById('epic-sort-menu');
    sortToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        sortMenu.style.display = sortMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#epic-sort-menu .dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            e.stopPropagation();
            const option = item.getAttribute('data-sort');
            if (currentSortOption === option) {
                sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
                currentSortOption = option;
                sortDirection = 'asc';
            }
            updateSortUI();
            applyFilterAndSort();
            sortMenu.style.display = 'none';
        });
    });

    const filterToggle = document.getElementById('epic-filter-toggle');
    const filterMenu = document.getElementById('epic-filter-menu');
    filterToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        filterMenu.style.display = filterMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#epic-filter-menu .dropdown-item').forEach(item => {
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
    const label = document.getElementById('epic-sort-label');
    const arrow = sortDirection === 'asc' ? '↑' : '↓';
    if (label) label.innerText = `Sort: ${currentSortOption} ${arrow}`;

    document.querySelectorAll('#epic-sort-menu .dropdown-item').forEach(item => {
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
    const label = document.getElementById('epic-filter-label');
    if (label) label.innerText = `Filter: ${currentFilterOption}`;

    document.querySelectorAll('#epic-filter-menu .dropdown-item').forEach(item => {
        item.classList.toggle('active', item.getAttribute('data-filter') === currentFilterOption);
    });
}

function applyFilterAndSort() {
    // 1. Filtering using IsInstalled
    filteredGames = epicGames.filter(game => {
        const isInstalled = !!(game.IsInstalled ?? game.isInstalled);

        switch (currentFilterOption) {
            case 'Installed': return isInstalled;
            case 'Not Installed': return !isInstalled;
            case 'All':
            default: return true;
        }
    });

    // 2. Sorting naturally on Name or Release Date
    const naturalCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

    filteredGames.sort((a, b) => {
        let valA, valB;
        switch (currentSortOption) {
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
    const countLabel = document.getElementById('epic-count-label');
    if (countLabel) {
        const total = epicGames.length;
        const filtered = filteredGames.length;
        if (currentFilterOption === 'All') {
            countLabel.innerText = `${total} ${total === 1 ? 'game' : 'games'} in Epic library`;
        } else {
            countLabel.innerText = `Showing ${filtered} of ${total} games (${currentFilterOption.toLowerCase()})`;
        }
    }

    renderGrid();
    renderAlphabetBar({
        barId: 'epic-alphabet-bar',
        workspaceId: 'epic-workspace',
        games: filteredGames.map(g => ({ ...g, Name: g.SortName || g.Name || g.name })),
        sortOption: currentSortOption
    });
}

function renderGrid() {
    const grid = document.getElementById('epic-games-grid');
    if (!grid) return;
    grid.innerHTML = '';

    filteredGames.forEach(game => {
        const igdbId = game.Id ?? game.id;
        const externalAppId = game.ExternalAppId ?? game.externalAppId ?? game.Uid ?? game.uid;
        const releaseYear = game.ReleaseYear || game.releaseYear || (game.ReleaseDate ? new Date(game.ReleaseDate).getFullYear() : 'Epic Games');

        const cardGame = {
            ...game,
            Id: igdbId,
            ExternalAppId: externalAppId,
            ReleaseYear: releaseYear
        };

        const card = createGameCard(
            cardGame,
            'Name',
            null,
            null,
            null,
            'epic'
        );

        card.addEventListener('click', (e) => {
            if (e.target.closest('.game-card-favorite-btn') ||
                e.target.closest('.game-card-menu-btn') ||
                e.target.closest('.game-card-context-menu') ||
                e.target.closest('.game-card-missing-delete-btn')) {
                return;
            }

            e.stopImmediatePropagation();
            if (igdbId && igdbId > 0) {
                openGameDetails(igdbId, 'epic');
            } else if (externalAppId) {
                openGameDetails(externalAppId, 'epic');
            }
        }, true);

        grid.appendChild(card);
    });
}

function setupScrollTopEvent() {
    const workspace = document.getElementById('epic-workspace');
    const scrollTopBtn = document.getElementById('epic-scroll-top-btn');
    const alphabetBar = document.getElementById('epic-alphabet-bar');
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

function renderEpicWarning(scenario, detailMessage = '') {
    const container = document.querySelector('.stash-container');
    const loader = document.getElementById('epic-loading-indicator');
    const workspace = document.getElementById('epic-workspace');
    const countLabel = document.getElementById('epic-count-label');
    if (loader) loader.style.display = 'none';
    if (workspace) workspace.style.display = 'none';
    document.querySelector('.stash-empty-warning-box')?.remove();
    let icon = '🎮';
    let title = 'No Epic Games Found';
    let text = 'Unable to resolve any games from your Epic Games account.';
    let subtext = 'Ensure your Epic Games session is authenticated in the Global Settings panel.';
    let btnText = 'Go to Settings';
    if (scenario === 'Error') {
        icon = '⚠️';
        title = 'Failed to Load Epic Stash';
        text = detailMessage || 'A network error occurred while communicating with the backend Epic service.';
    }
    if (countLabel) countLabel.innerText = title;
    const warningBox = document.createElement('div');
    warningBox.className = 'stash-empty-warning-box';
    warningBox.innerHTML = `
        <div class="warning-icon">${icon}</div>
        <h2>${title}</h2>
        <p>${text}</p>
        <p class="warning-subtext">${subtext}</p>
        <button class="nav-to-settings-btn" id="epic-warn-redirect-btn">${btnText}</button>
    `;
    container.appendChild(warningBox);
    document.getElementById('epic-warn-redirect-btn')?.addEventListener('click', () => {
        import('./router.js').then(m => m.navigateTo('settings'));
    });
}