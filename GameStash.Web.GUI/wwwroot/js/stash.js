/**
 * Unified Stash View Controller (Handles Local, Steam, Epic, GOG, Amazon, and Unified Stashes)
 */

import { createGameCard } from './gamecard.js';
import { openGameDetails } from './gamedetails.js';
import { renderAlphabetBar } from './alphabetbar.js';

let activeDeleteCallback = null;

const STASH_CONFIGS = {
    local: {
        title: 'Local Games Stash',
        apiUrl: '/api/stash/local',
        loadingText: 'Loading stash items...',
        countSuffix: (total) => `${total} ${total === 1 ? 'game' : 'games'} cataloged`,
        syncLabel: null,
        hasFlush: true,
        sortOptions: ['Name', 'Release Date', 'Rating', 'Date Added', 'Folder Size'],
        filterOptions: [
            { value: 'Favorites', label: 'Favorites' },
            { value: 'Not Favorites', label: 'Not Favorites' },
            { value: 'Installed', label: 'Installed' },
            { value: 'Not Installed', label: 'Not Installed' },
            { value: 'Played', label: 'Played' },
            { value: 'Not Played', label: 'Not Played' },
            { value: 'Missing', label: 'Missing Folder' }
        ],
        emptyIcon: '📂',
        emptyTitle: 'No Monitored Folders Found',
        emptyText: 'Unable to resolve any local games.',
        emptySubtext: 'Configure your root folders in <strong>Root Folders</strong> settings to begin indexing.',
        redirectView: 'rootfolderssettings',
        redirectBtnText: 'Go to Root Folders'
    },
    steam: {
        title: 'Steam Games Stash',
        apiUrl: '/api/stash/steam',
        loadingText: 'Loading Steam library...',
        countSuffix: (total) => `${total} ${total === 1 ? 'game' : 'games'} in Steam library`,
        syncLabel: 'Sync from Steam',
        hasFlush: false,
        sortOptions: ['Name', 'Playtime', 'Release Date'],
        filterOptions: [
            { value: 'Favorites', label: 'Favorites' },
            { value: 'Not Favorites', label: 'Not Favorites' },
            { value: 'Installed', label: 'Installed' },
            { value: 'Not Installed', label: 'Not Installed' },
            { value: 'Played', label: 'Played' },
            { value: 'Not Played', label: 'Not Played' }
        ],
        emptyIcon: '⚛️',
        emptyTitle: 'No Steam Games Found',
        emptyText: 'Unable to resolve any games from your Steam account.',
        emptySubtext: 'Ensure your SteamID64 and Steam Web API Key are configured in <strong>External Services</strong>.',
        redirectView: 'externalservicessettings',
        redirectBtnText: 'Go to External Services'
    },
    epic: {
        title: 'Epic Games Stash',
        apiUrl: '/api/stash/epic',
        loadingText: 'Loading Epic Games library...',
        countSuffix: (total) => `${total} ${total === 1 ? 'game' : 'games'} in Epic library`,
        syncLabel: 'Sync from Epic',
        hasFlush: false,
        sortOptions: ['Name', 'Playtime', 'Release Date'],
        filterOptions: [
            { value: 'Favorites', label: 'Favorites' },
            { value: 'Not Favorites', label: 'Not Favorites' },
            { value: 'Installed', label: 'Installed' },
            { value: 'Not Installed', label: 'Not Installed' },
            { value: 'Played', label: 'Played' },
            { value: 'Not Played', label: 'Not Played' }
        ],
        emptyIcon: '🎮',
        emptyTitle: 'No Epic Games Found',
        emptyText: 'Unable to resolve any games from your Epic Games account.',
        emptySubtext: 'Ensure your Epic Games session is authenticated in <strong>External Services</strong>.',
        redirectView: 'externalservicessettings',
        redirectBtnText: 'Go to External Services'
    },
    gog: {
        title: 'GOG Stash',
        apiUrl: '/api/stash/gog',
        loadingText: 'Loading GOG library...',
        countSuffix: (total) => `${total} ${total === 1 ? 'game' : 'games'} in GOG library`,
        syncLabel: 'Sync from GOG',
        hasFlush: false,
        sortOptions: ['Name', 'Playtime', 'Release Date'],
        filterOptions: [
            { value: 'Favorites', label: 'Favorites' },
            { value: 'Not Favorites', label: 'Not Favorites' },
            { value: 'Installed', label: 'Installed' },
            { value: 'Not Installed', label: 'Not Installed' },
            { value: 'Played', label: 'Played' },
            { value: 'Not Played', label: 'Not Played' }
        ],
        emptyIcon: '🌌',
        emptyTitle: 'No GOG Games Found',
        emptyText: 'Unable to resolve any games from your GOG account.',
        emptySubtext: 'Ensure your GOG session is authenticated in <strong>External Services</strong>.',
        redirectView: 'externalservicessettings',
        redirectBtnText: 'Go to External Services'
    },
    amazon: {
        title: 'Amazon Games Stash',
        apiUrl: '/api/stash/amazon',
        loadingText: 'Loading Amazon Games library...',
        countSuffix: (total) => `${total} ${total === 1 ? 'game' : 'games'} in Amazon library`,
        syncLabel: 'Sync from Amazon',
        hasFlush: false,
        sortOptions: ['Name', 'Release Date'],
        filterOptions: [
            { value: 'Favorites', label: 'Favorites' },
            { value: 'Not Favorites', label: 'Not Favorites' },
            { value: 'Installed', label: 'Installed' },
            { value: 'Not Installed', label: 'Not Installed' }
        ],
        emptyIcon: '👑',
        emptyTitle: 'No Amazon Games Found',
        emptyText: 'Unable to resolve any games from your Amazon Games account.',
        emptySubtext: 'Ensure your Amazon Games session is authenticated in <strong>External Services</strong>.',
        redirectView: 'externalservicessettings',
        redirectBtnText: 'Go to External Services'
    },
    unified: {
        title: 'Unified Stash',
        apiUrl: '/api/stash/unified',
        loadingText: 'Loading unified game library...',
        countSuffix: (total) => `${total} ${total === 1 ? 'entry' : 'entries'} across all stashes`,
        syncLabel: 'Sync All Stashes',
        hasFlush: false,
        sortOptions: ['Name', 'Playtime', 'Release Date'],
        filterOptions: [
            { value: 'Favorites', label: 'Favorites' },
            { value: 'Not Favorites', label: 'Not Favorites' },
            { value: 'Installed', label: 'Installed' },
            { value: 'Not Installed', label: 'Not Installed' },
            { value: 'Played', label: 'Played' },
            { value: 'Not Played', label: 'Not Played' },
            { value: 'Duplicates', label: 'Duplicates' },
            { value: 'Missing', label: 'Missing Folder' }
        ],
        emptyIcon: '📦',
        emptyTitle: 'No Games Found in Unified Stash',
        emptyText: 'None of your configured stashes currently have indexed games.',
        emptySubtext: 'Configure your storefronts or root folders in Settings to populate your library.',
        redirectView: 'externalservicessettings',
        redirectBtnText: 'Go to External Services'
    }
};

let activeType = 'local';
let activeConfig = STASH_CONFIGS.local;
let stashGames = [];
let filteredGames = [];
let currentSortOption = 'Name';
let sortDirection = 'asc';
let currentFilterOption = 'All';
let currentSearchQuery = '';

let activeGameForEdit = null;
let activeGameForDelete = null;

/**
 * Initializes the shared Stash view for a specific provider ('local' | 'steam' | 'epic' | 'gog' | 'amazon' | 'unified').
 */
export async function initStashView(workspaceElement, stashType = 'local') {
    activeType = stashType.toLowerCase();
    activeConfig = STASH_CONFIGS[activeType] || STASH_CONFIGS.local;

    // Reset per-view state
    stashGames = [];
    filteredGames = [];
    currentSortOption = 'Name';
    sortDirection = 'asc';
    currentFilterOption = 'All';
    currentSearchQuery = '';

    configureHeaderAndToolbar();
    setupToolbarEvents();
    setupDialogEvents();
    setupExportModalEvents();
    setupScrollTopEvent();
    setupGlobalEventListeners();

    await loadStashData(false);
}

function configureHeaderAndToolbar() {
    const titleEl = document.getElementById('stash-title');
    if (titleEl) titleEl.innerText = activeConfig.title;

    const loadingTextEl = document.getElementById('stash-loading-text');
    if (loadingTextEl) loadingTextEl.innerText = activeConfig.loadingText;

    const syncBtn = document.getElementById('stash-sync-btn');
    const syncLabel = document.getElementById('stash-sync-label');
    if (syncBtn) {
        if (activeConfig.syncLabel) {
            syncBtn.style.display = 'flex';
            if (syncLabel) syncLabel.innerText = activeConfig.syncLabel;
        } else {
            syncBtn.style.display = 'none';
        }
    }

    const flushBtn = document.getElementById('stash-flush-btn');
    if (flushBtn) {
        flushBtn.style.display = activeConfig.hasFlush ? 'flex' : 'none';
    }

    const storeDropdown = document.getElementById('stash-store-dropdown');
    if (storeDropdown) {
        storeDropdown.style.display = activeConfig.hasStoreFilter ? 'block' : 'none';
    }

    // Build dynamic Sort menu
    const sortMenu = document.getElementById('stash-sort-menu');
    if (sortMenu) {
        sortMenu.innerHTML = activeConfig.sortOptions.map((opt, idx) => `
            <div class="dropdown-item ${idx === 0 ? 'active' : ''}" data-sort="${opt}">
                <span>${opt}</span>
                <span class="sort-indicator">${idx === 0 ? '↑' : ''}</span>
            </div>
        `).join('');
    }

    // Build dynamic Filter menu with separator + Clear Filter action
    const filterMenu = document.getElementById('stash-filter-menu');
    if (filterMenu) {
        const itemsHtml = activeConfig.filterOptions.map((opt) =>
            '<div class="dropdown-item" data-filter="' + opt.value + '">' + opt.label + '</div>'
        ).join('');

        filterMenu.innerHTML =
            itemsHtml +
            '<div class="dropdown-separator"></div>' +
            '<div class="dropdown-item dropdown-clear-item disabled" id="stash-filter-clear-btn" data-filter="All">Clear Filter</div>';
    }

    const filterLabel = document.getElementById('stash-filter-label');
    if (filterLabel) filterLabel.innerText = 'Filter: All';
}

async function loadStashData(forceRefresh = false) {
    const loader = document.getElementById('stash-loading-indicator');
    const workspace = document.getElementById('stash-workspace');
    const countLabel = document.getElementById('stash-count-label');

    if (loader) loader.style.display = 'flex';
    if (workspace) workspace.style.display = 'none';
    if (countLabel) countLabel.innerText = 'Loading games...';

    document.querySelector('.stash-empty-warning-box')?.remove();

    try {
        // Local Stash pre-checks: root folders & unimported backlog detection
        let folders = [];
        if (activeType === 'local') {
            const foldersRes = await fetch('/api/folders');
            if (foldersRes.ok) {
                folders = await foldersRes.json();
                if (!folders || folders.length === 0) {
                    renderWarning('MissingRootFolders');
                    return;
                }
            }
        }
        const url = activeConfig.apiUrl + (forceRefresh && activeConfig.syncLabel ? '?refresh=true' : '');
    const response = await fetch(url);
    if (!response.ok) throw new Error('HTTP Error ' + response.status);
    stashGames = await response.json();
    if (!stashGames || stashGames.length === 0) {
        if (activeType === 'local') {
            const isAnyScanning = folders.some(f => f.IsImportingFolders === true || f.isImportingFolders === true);
            if (isAnyScanning) {
                renderWarning('FoldersAreScanning');
                return;
            }
            const foundRes = await fetch('/api/folders/found');
            if (foundRes.ok) {
                const foundFolders = await foundRes.json();
                renderWarning(foundFolders && foundFolders.length > 0 ? 'HasUnimportedGames' : 'NoGamesFoundInRoots');
                return;
            }
        }
        renderWarning('NoGamesFound');
        return;
    }
    applyFilterAndSort();
} catch (err) {
    console.error('Failed to load ' + activeType + ' stash data:', err);
    renderWarning('Error', err.message);
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
    return (minutes / 60).toFixed(1) + ' hrs played';
}
function getUnifiedGameKey(game) {
    const id = game.Id ?? game.id;
    if (id !== null && id !== undefined && Number(id) > 0) {
        return 'igdb:' + id;
    }
    const rawName = (game.Name || game.name || '').toLowerCase().replace(/[^a-z0-9]/g, '');
    return 'title:' + rawName;
}

function setupToolbarEvents() {
    document.getElementById('stash-reload-btn')?.addEventListener('click', () => loadStashData(false));
    document.getElementById('stash-sync-btn')?.addEventListener('click', () => loadStashData(true));
    document.getElementById('stash-flush-btn')?.addEventListener('click', async () => {
        if (!confirm("CRITICAL WARNING:\nAre you sure you want to permanently flush the entire PostgreSQL database table?\nThis action cannot be undone.")) {
            return;
        }
        try {
            const response = await fetch('/api/stash/local/', { method: 'DELETE' });
            if (response.ok) {
                alert('Database successfully flushed.');
                await loadStashData(false);
            } else {
                alert('Failed to clear database records.');
            }
        } catch (err) {
            console.error("Flush transaction encountered a network exception:", err);
        }
    });
    const storeToggle = document.getElementById('stash-store-toggle');
    const storeMenu = document.getElementById('stash-store-menu');
    const sortToggle = document.getElementById('stash-sort-toggle');
    const sortMenu = document.getElementById('stash-sort-menu');
    const filterToggle = document.getElementById('stash-filter-toggle');
    const filterMenu = document.getElementById('stash-filter-menu');
    const closeAllDropdowns = () => {
        if (storeMenu) storeMenu.style.display = 'none';
        if (sortMenu) sortMenu.style.display = 'none';
        if (filterMenu) filterMenu.style.display = 'none';
    };
    storeToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        const wasOpen = storeMenu.style.display === 'flex';
        closeAllDropdowns();
        if (!wasOpen) storeMenu.style.display = 'flex';
    });
    const searchInput = document.getElementById('stash-search-input');
    const searchClear = document.getElementById('stash-search-clear');

    searchInput?.addEventListener('input', () => {
        currentSearchQuery = searchInput.value.trim().toLowerCase();
        if (searchClear) searchClear.style.display = currentSearchQuery ? 'flex' : 'none';
        applyFilterAndSort();
    });

    searchClear?.addEventListener('click', () => {
        if (searchInput) searchInput.value = '';
        currentSearchQuery = '';
        if (searchClear) searchClear.style.display = 'none';
        applyFilterAndSort();
        searchInput?.focus();
    });

    sortToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        const wasOpen = sortMenu.style.display === 'flex';
        closeAllDropdowns();
        if (!wasOpen) sortMenu.style.display = 'flex';
    });
    document.querySelectorAll('#stash-sort-menu .dropdown-item').forEach(item => {
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
            closeAllDropdowns();
        });
    });
    filterToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        const wasOpen = filterMenu.style.display === 'flex';
        closeAllDropdowns();
        if (!wasOpen) filterMenu.style.display = 'flex';
    });
    document.querySelectorAll('#stash-filter-menu .dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            e.stopPropagation();
            if (item.classList.contains('disabled')) return;

            currentFilterOption = item.getAttribute('data-filter') || 'All';
            updateFilterUI();
            applyFilterAndSort();
            closeAllDropdowns();
        });
    });
    if (!window._hasStashDropdownCloser) {
        window.addEventListener('click', () => {
            document.getElementById('stash-store-menu')?.style.setProperty('display', 'none');
            document.getElementById('stash-sort-menu')?.style.setProperty('display', 'none');
            document.getElementById('stash-filter-menu')?.style.setProperty('display', 'none');
        });
        window._hasStashDropdownCloser = true;
    }
}

function updateSortUI() {
    const label = document.getElementById('stash-sort-label');
    const arrow = sortDirection === 'asc' ? '↑' : '↓';
    if (label) label.innerText = 'Sort: ' + currentSortOption + ' ' + arrow;
    document.querySelectorAll('#stash-sort-menu .dropdown-item').forEach(item => {
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
    const label = document.getElementById('stash-filter-label');
    if (label) label.innerText = 'Filter: ' + currentFilterOption;
    document.querySelectorAll('#stash-filter-menu .dropdown-item[data-filter]').forEach(item => {
        const val = item.getAttribute('data-filter');
        if (val === 'All') return;
        item.classList.toggle('active', val === currentFilterOption);
    });
    const clearBtn = document.getElementById('stash-filter-clear-btn');
    if (clearBtn) {
        clearBtn.classList.toggle('disabled', currentFilterOption === 'All');
    }
}

function applyFilterAndSort() {
    let duplicateKeys = null;
    if (activeType === 'unified' && currentFilterOption === 'Duplicates') {
        const counts = new Map();
        stashGames.forEach(g => {
            const key = getUnifiedGameKey(g);
            counts.set(key, (counts.get(key) || 0) + 1);
        });
        duplicateKeys = new Set(
            Array.from(counts.entries())
                .filter(([, count]) => count > 1)
                .map(([key]) => key)
        );
    }
    filteredGames = stashGames.filter(game => {
        const isInstalled = !!(game.IsInstalled ?? game.isInstalled);
        const isFavorite = !!(game.IsFavorite ?? game.isFavorite);
        const minutes = getPlaytimeMinutes(game);
        const played = minutes > 0 ||
            !!(game.IsPlayed ?? game.isPlayed) ||
            !!(game.IsPlaying ?? game.isPlaying);

        if (currentSearchQuery) {
            const name = (game.Name || game.name || '').toLowerCase();
            const sortName = (game.SortName || game.sortName || '').toLowerCase();
            if (!name.includes(currentSearchQuery) && !sortName.includes(currentSearchQuery)) {
                return false;
            }
        }

        switch (currentFilterOption) {
            case 'Favorites': return isFavorite;
            case 'Not Favorites': return !isFavorite;
            case 'Installed': return isInstalled;
            case 'Not Installed': return !isInstalled;
            case 'Missing': {
                const source = (game.ExternalSystem || game.externalSystem || activeType || '').toLowerCase();
                const isLocalGame = source === 'local';
                const isFolderValid = game.IsFolderValid ?? game.isFolderValid;
                return isLocalGame && isFolderValid === false;
            }
            case 'Played': return played;
            case 'Not Played': return !played;
            case 'Duplicates': return duplicateKeys ? duplicateKeys.has(getUnifiedGameKey(game)) : true;
            case 'All':
            default: return true;
        }
    });

    const naturalCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
    filteredGames.sort((a, b) => {
        let valA, valB;
        switch (currentSortOption) {
            case 'Release Date':
                valA = (a.FirstReleaseDate || a.ReleaseDate) ? new Date(a.FirstReleaseDate || a.ReleaseDate).getTime() : 0;
                valB = (b.FirstReleaseDate || b.ReleaseDate) ? new Date(b.FirstReleaseDate || b.ReleaseDate).getTime() : 0;
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;
            case 'Playtime':
                valA = getPlaytimeMinutes(a);
                valB = getPlaytimeMinutes(b);
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;
            case 'Rating':
                valA = a.Rating || 0;
                valB = b.Rating || 0;
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;
            case 'Date Added':
                valA = a.DateAdded ? new Date(a.DateAdded).getTime() : 0;
                valB = b.DateAdded ? new Date(b.DateAdded).getTime() : 0;
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;
            case 'Folder Size':
                valA = a.FolderDiskSpaceBytes || 0;
                valB = b.FolderDiskSpaceBytes || 0;
                if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
                if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
                return 0;
            case 'Name':
            default:
                valA = (a.SortName || a.Name || a.name || '').toLowerCase();
                valB = (b.SortName || b.Name || b.name || '').toLowerCase();
                const nameCmp = sortDirection === 'asc'
                    ? naturalCollator.compare(valA, valB)
                    : naturalCollator.compare(valB, valA);
                if (nameCmp !== 0 || activeType !== 'unified') return nameCmp;
                const storeA = (a.ExternalSystem || a.externalSystem || '').toLowerCase();
                const storeB = (b.ExternalSystem || b.externalSystem || '').toLowerCase();
                return naturalCollator.compare(storeA, storeB);
        }
    });
    const countLabel = document.getElementById('stash-count-label');
    if (countLabel) {
        const total = stashGames.length;
        const filtered = filteredGames.length;
        if (currentFilterOption === 'All' && !currentSearchQuery) {
            countLabel.innerText = activeConfig.countSuffix(total);
        } else {
            countLabel.innerText = 'Showing ' + filtered + ' of ' + total + ' games';
        }
    }
    renderGrid();
    renderAlphabetBar({
        barId: 'stash-alphabet-bar',
        workspaceId: 'stash-workspace',
        games: filteredGames.map(g => ({ ...g, Name: g.SortName || g.Name || g.name })),
        sortOption: currentSortOption
    });
}

function renderGrid() {
    const grid = document.getElementById('stash-games-grid');
    if (!grid) return;
    grid.innerHTML = '';
    filteredGames.forEach(game => {
        const igdbId = game.Id ?? game.id;
        const externalAppId = game.ExternalAppId ?? game.externalAppId ?? game.ExtId ?? game.extId ?? game.Uid ?? game.uid;
        const cardStashType = activeType === 'unified'
            ? (game.ExternalSystem || game.externalSystem || 'local').toLowerCase()
            : activeType;
        const cardGame = {
            ...game,
            Id: igdbId,
            ExternalAppId: externalAppId,
            showStoreBadge: activeType === 'unified',
            ReleaseYear: game.ReleaseYear || game.releaseYear || ''
        };
        const card = createGameCard(
            cardGame,
            currentSortOption,
            activeType === 'local' ? openDeleteModal : null,
            openEditModal,
            () => {
                if (currentFilterOption === 'Favorites' || currentFilterOption === 'Not Favorites') {
                    applyFilterAndSort();
                }
            },
            cardStashType
        );
        if (activeType !== 'local') {
            card.addEventListener('click', (e) => {
                if (e.target.closest('.game-card-favorite-btn') ||
                    e.target.closest('.game-card-menu-btn') ||
                    e.target.closest('.game-card-context-menu') ||
                    e.target.closest('.game-card-missing-delete-btn')) {
                    return;
                }
                e.stopImmediatePropagation();
                if (igdbId !== null && igdbId !== undefined) {
                    openGameDetails(igdbId, cardStashType);
                } else if (externalAppId) {
                    openGameDetails(externalAppId, cardStashType);
                }
            }, true);
        }
        grid.appendChild(card);
    });
}
function setupScrollTopEvent() {
    const workspace = document.getElementById('stash-workspace');
    const scrollTopBtn = document.getElementById('stash-scroll-top-btn');
    const alphabetBar = document.getElementById('stash-alphabet-bar');
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
        workspace.scrollTo({ top: 0, behavior: 'smooth' });
    });
}
function setupDialogEvents() {
    const editModal = document.getElementById('stash-edit-modal');
    const editInput = document.getElementById('stash-edit-sortname-input');
    const editSaveBtn = document.getElementById('stash-edit-save-btn');
    document.getElementById('stash-edit-close-btn')?.addEventListener('click', () => {
        if (editModal) editModal.classList.remove('active');
    });
    editSaveBtn?.addEventListener('click', async () => {
        if (!activeGameForEdit || !editInput) return;
        const newSortName = editInput.value.trim();
        if (!newSortName) return;

        activeGameForEdit.SortName = newSortName;
        const targetType = activeGameForEdit.stashType || activeType || 'local';

        await fetch(`/api/stash/${targetType}/${activeGameForEdit.Id}/details`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ propertyName: 'SortName', value: newSortName })
        });

        if (editModal) editModal.classList.remove('active');

        window.dispatchEvent(new CustomEvent('gameSortNameChanged', {
            detail: { gameId: activeGameForEdit.Id, sortName: newSortName }
        }));

        if (document.getElementById('stash-games-grid')) {
            applyFilterAndSort();
        }
    });
    editInput?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            editSaveBtn?.click();
        }
    });
    document.getElementById('stash-delete-cancel-btn')?.addEventListener('click', () => {
        document.getElementById('stash-delete-modal')?.classList.remove('active');
    });
    document.getElementById('stash-delete-confirm-btn')?.addEventListener('click', async () => {
        if (!activeGameForDelete) return;

        await fetch(`/api/stash/local/${activeGameForDelete.Id}`, { method: 'DELETE' });
        stashGames = stashGames.filter(g => String(g.Id) !== String(activeGameForDelete.Id));

        document.getElementById('stash-delete-modal')?.classList.remove('active');

        window.dispatchEvent(new CustomEvent('gameRemovedFromStash', {
            detail: { gameId: activeGameForDelete.Id }
        }));

        if (document.getElementById('stash-games-grid')) {
            applyFilterAndSort();
        }

        if (typeof activeDeleteCallback === 'function') {
            activeDeleteCallback();
            activeDeleteCallback = null;
        }
    });
}

function setupGlobalEventListeners() {
    if (window._hasSharedStashListeners) return;
    window.addEventListener('gameProgressChanged', (e) => {
        const { gameId, isPlaying, isPlayed, playedToEnd } = e.detail;
        const target = stashGames.find(g => String(g.Id ?? g.id) === String(gameId));
        if (target) {
            target.IsPlaying = isPlaying;
            target.isPlaying = isPlaying;
            target.IsPlayed = isPlayed;
            target.isPlayed = isPlayed;
            target.PlayedToEnd = playedToEnd;
            target.playedToEnd = playedToEnd;

            if (document.getElementById('stash-games-grid') &&
                (currentFilterOption === 'Played' || currentFilterOption === 'Not Played')) {
                applyFilterAndSort();
            }
        }
    });

    window.addEventListener('gameFavoriteChanged', (e) => {
        const { gameId, isFavorite } = e.detail;
        const target = stashGames.find(g => String(g.Id ?? g.id) === String(gameId));
        if (target) {
            target.IsFavorite = isFavorite;
            target.isFavorite = isFavorite;

            if (document.getElementById('stash-games-grid') &&
                (currentFilterOption === 'Favorites' || currentFilterOption === 'Not Favorites')) {
                applyFilterAndSort();
            }
        }
    });
    window.addEventListener('gameSortNameChanged', (e) => {
        const { gameId, sortName } = e.detail;
        const target = stashGames.find(g => String(g.Id ?? g.id) === String(gameId));
        if (target && document.getElementById('stash-games-grid')) {
            target.SortName = sortName;
            target.sortName = sortName;
            applyFilterAndSort();
        }
    });
    window.addEventListener('gameRemovedFromStash', (e) => {
        const { gameId } = e.detail;
        if (document.getElementById('stash-games-grid')) {
            stashGames = stashGames.filter(g => String(g.Id ?? g.id) !== String(gameId));
            applyFilterAndSort();
        }
    });
    window.addEventListener('gameInstalledChanged', (e) => {
        const { gameId, isInstalled } = e.detail;
        const target = stashGames.find(g => String(g.Id ?? g.id) === String(gameId));
        if (target) {
            target.IsInstalled = isInstalled;
            target.isInstalled = isInstalled;

            if (document.getElementById('stash-games-grid') &&
                (currentFilterOption === 'Installed' || currentFilterOption === 'Not Installed')) {
                applyFilterAndSort();
            }
        }
    });

    window.addEventListener('gamePlaylistStatusChanged', (e) => {
        const { gameId, isInPlaylist } = e.detail;
        const target = stashGames.find(g => String(g.Id ?? g.id) === String(gameId));
        if (target) {
            target.IsInPlaylist = isInPlaylist;
            target.isInPlaylist = isInPlaylist;
        }
    });
    window._hasSharedStashListeners = true;
}

export function openEditModal(game, stashType = activeType) {
    activeGameForEdit = game;
    const input = document.getElementById('stash-edit-sortname-input');
    if (input) {
        input.value = game.SortName || game.Name;
        document.getElementById('stash-edit-modal')?.classList.add('active');
        input.focus();
    }
}

export function openDeleteModal(game, onDeletedSuccess = null) {
    activeGameForDelete = game;
    activeDeleteCallback = onDeletedSuccess;
    const msgEl = document.getElementById('stash-delete-message');
    if (msgEl) {
        msgEl.innerHTML =
            'This action will permanently remove <strong>\'' + game.Name + '\'</strong> from your stash. ' +
            'If the folder still exists, it will return to the \'Import from Root Folders\' section.<br/><br/>Are you sure?';
    }
    document.getElementById('stash-delete-modal')?.classList.add('active');
}

function setupExportModalEvents() {
    const exportModal = document.getElementById('stash-export-modal');
    const exportBtn = document.getElementById('stash-export-btn');
    const closeBtn = document.getElementById('stash-export-close-btn');
    const copyBtn = document.getElementById('stash-export-copy-btn');
    const downloadBtn = document.getElementById('stash-export-download-btn');
    const countInfo = document.getElementById('stash-export-count-info');

    if (!exportModal || !exportBtn) return;

    const closeModal = () => exportModal.classList.remove('active');

    exportBtn.onclick = () => {
        const sourceList = filteredGames.length > 0 ? filteredGames : stashGames;
        if (countInfo) countInfo.innerText = String(sourceList.length);
        exportModal.classList.add('active');
    };

    if (closeBtn) closeBtn.onclick = closeModal;

    exportModal.onclick = (e) => {
        if (e.target === exportModal) closeModal();
    };

    const buildExportPayload = () => {
        const formatInput = document.querySelector('input[name="stash-export-format"]:checked');
        const format = formatInput ? formatInput.value : 'csv';

        const selectedFields = Array.from(
            document.querySelectorAll('#stash-export-fields input[type="checkbox"]:checked')
        ).map(cb => cb.value);

        const sourceList = filteredGames.length > 0 ? filteredGames : stashGames;

        const fieldDefinitions = {
            igdbId: {
                header: 'IGDB ID',
                getValue: (g) => {
                    const id = Number(g.Id ?? g.id ?? 0);
                    return id > 0 ? id : '';
                }
            },
            store: {
                header: 'Game Store',
                getValue: (g) => {
                    const raw = g.ExternalSystem || g.externalSystem || activeType || '';
                    if (!raw) return '';
                    return raw.toLowerCase() === 'gog'
                        ? 'GOG'
                        : raw.charAt(0).toUpperCase() + raw.slice(1).toLowerCase();
                }
            },
            name: {
                header: 'Game Name',
                getValue: (g) => g.Name || g.name || ''
            },
            installed: {
                header: 'Installed',
                getValue: (g) => !!(g.IsInstalled ?? g.isInstalled)
            },
            isPlayed: {
                header: 'Played',
                getValue: (g) => !!(g.IsPlayed ?? g.isPlayed) || getPlaytimeMinutes(g) > 0
            },
            isPlaying: {
                header: 'Currently Playing',
                getValue: (g) => !!(g.IsPlaying ?? g.isPlaying)
            },
            playedToEnd: {
                header: 'Completed',
                getValue: (g) => !!(g.PlayedToEnd ?? g.playedToEnd)
            },
            playtime: {
                header: 'Playtime',
                getValue: (g) => {
                    const mins = getPlaytimeMinutes(g);
                    if (mins <= 0) return 'Unplayed';
                    const hrs = Math.floor(mins / 60);
                    const rem = mins % 60;
                    const parts = [];
                    if (hrs > 0) parts.push(hrs + (hrs === 1 ? ' hr' : ' hrs'));
                    if (rem > 0) parts.push(rem + (rem === 1 ? ' min' : ' mins'));
                    return parts.join(' ');
                }
            }
        };

        const activeDefs = selectedFields
            .filter(key => fieldDefinitions[key])
            .map(key => ({ key, ...fieldDefinitions[key] }));

        if (format === 'json') {
            const rows = sourceList.map(game => {
                const obj = {};
                activeDefs.forEach(def => {
                    obj[def.header] = def.getValue(game);
                });
                return obj;
            });

            return {
                format: 'json',
                mimeType: 'application/json;charset=utf-8;',
                fileName: 'gamestash-' + activeType + '-export.json',
                content: JSON.stringify(rows, null, 2)
            };
        }

        // CSV format with RFC-4180 escaping
        const escapeCsvCell = (val) => {
            if (val === null || val === undefined) return '';
            const str = String(val);
            if (str.includes('"') || str.includes(',') || str.includes('\n') || str.includes('\r')) {
                return '"' + str.replace(/"/g, '""') + '"';
            }
            return str;
        };

        const headerLine = activeDefs.map(d => escapeCsvCell(d.header)).join(',');
        const dataLines = sourceList.map(game =>
            activeDefs.map(d => escapeCsvCell(d.getValue(game))).join(',')
        );

        return {
            format: 'csv',
            mimeType: 'text/csv;charset=utf-8;',
            fileName: 'gamestash-' + activeType + '-export.csv',
            content: [headerLine, ...dataLines].join('\r\n')
        };
    };

    if (copyBtn) {
        copyBtn.onclick = async () => {
            const payload = buildExportPayload();
            try {
                await navigator.clipboard.writeText(payload.content);
                closeModal();
                alert(payload.format.toUpperCase() + ' export copied to clipboard!');
            } catch {
                alert('Failed to copy export to clipboard.');
            }
        };
    }

    if (downloadBtn) {
        downloadBtn.onclick = () => {
            const payload = buildExportPayload();
            const blob = new Blob([payload.content], { type: payload.mimeType });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = payload.fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
            closeModal();
        };
    }
}

function renderWarning(scenario, detailMessage = '') {
    const container = document.querySelector('.stash-container');
    const loader = document.getElementById('stash-loading-indicator');
    const workspace = document.getElementById('stash-workspace');
    const countLabel = document.getElementById('stash-count-label');
    if (loader) loader.style.display = 'none';
    if (workspace) workspace.style.display = 'none';
    document.querySelector('.stash-empty-warning-box')?.remove();
    let icon = activeConfig.emptyIcon;
    let title = activeConfig.emptyTitle;
    let text = activeConfig.emptyText;
    let subtext = activeConfig.emptySubtext;
    let btnText = activeConfig.redirectBtnText;
    let targetView = activeConfig.redirectView;
    let hideButton = false;
    if (scenario === 'Error') {
        icon = '⚠️';
        title = 'Failed to Load ' + activeConfig.title;
        text = detailMessage || 'A network error occurred while communicating with the backend stash service.';
    } else if (scenario === 'MissingRootFolders') {
        title = 'No Monitored Folders Found';
        text = 'You have not configured any root directories to monitor for local game installations.';
    } else if (scenario === 'HasUnimportedGames') {
        icon = '🎮';
        title = 'Empty Stash';
        text = 'Unimported local game folders detected within your root directories.';
        subtext = 'Please navigate to the <strong>Import from Root Folders</strong> view to match metadata and add new games to your stash.';
        btnText = 'Import Games';
        targetView = 'importfolders';
    } else if (scenario === 'NoGamesFoundInRoots') {
        icon = '🔍';
        title = 'No Matches Discovered';
        text = 'No matching game installations or folders were found across your configured Root Folder(s).';
        subtext = 'Make sure your subdirectories contain executable game files or verify directory layouts inside your local storage.';
        btnText = 'Verify Settings';
    } else if (scenario === 'FoldersAreScanning') {
        icon = '<span class="spinner" style="width:44px; height:44px; display:inline-block;"></span>';
        title = 'Scanning Operations Ongoing';
        text = 'A monitored root directory is currently being indexed and parsed in the background.';
        subtext = 'The system is building the initial cache maps. Please wait a few moments for the operations to finalize.';
        hideButton = true;
    }
    if (countLabel) countLabel.innerText = title;
    const warningBox = document.createElement('div');
    warningBox.className = 'stash-empty-warning-box';
    warningBox.innerHTML =
        '<div class="warning-icon">' + icon + '</div>' +
        '<h2>' + title + '</h2>' +
        '<p>' + text + '</p>' +
        '<p class="warning-subtext">' + subtext + '</p>' +
        (hideButton ? '' : '<button class="nav-to-settings-btn" id="stash-warn-redirect-btn">' + btnText + '</button>');
    container.appendChild(warningBox);
    document.getElementById('stash-warn-redirect-btn')?.addEventListener('click', () => {
        import('./router.js').then(m => m.navigateTo(targetView));
    });
}