import { openGameDetails } from './gamedetails.js';

let libraryGames = [];
let filteredGames = [];
let currentSortOption = 'Name';
let sortDirection = 'asc';
let currentFilterOption = 'All';

let activeGameForEdit = null;
let activeGameForDelete = null;

export async function initLocalLibraryView(workspaceElement) {
    setupToolbarEvents();
    setupDialogEvents();
    await loadLibraryData();
}

async function loadLibraryData() {
    const loader = document.getElementById('lib-loading-indicator');
    const workspace = document.getElementById('lib-workspace');

    if (libraryGames.length === 0 && loader) {
        loader.style.display = 'flex';
    }

    try {
        const response = await fetch('/api/library/local');
        if (!response.ok) throw new Error(`HTTP Error ${response.status}`);

        libraryGames = await response.json();
        applyFilterAndSort();
    } catch (err) {
        console.error("Failed to load local library data:", err);
        const grid = document.getElementById('lib-games-grid');
        if (grid) grid.innerHTML = `<p style="color: #ff5252; padding: 20px;">Failed to load library items: ${err.message}</p>`;
    } finally {
        if (loader) loader.style.display = 'none';
        if (workspace) workspace.style.display = 'flex';
    }
}

function setupToolbarEvents() {
    document.getElementById('lib-reload-btn')?.addEventListener('click', () => loadLibraryData());

    document.getElementById('lib-export-btn')?.addEventListener('click', async () => {
        const textLines = libraryGames.map(g => `${g.Name} (${g.IsFavorite ? 'Favorite | ' : ''}${g.IsInstalled ? 'Installed' : 'Not Installed'})`);
        try {
            await navigator.clipboard.writeText(textLines.join('\n'));
            alert('Games list successfully copied to clipboard!');
        } catch {
            alert('Failed to copy list to clipboard.');
        }
    });

    const sortToggle = document.getElementById('lib-sort-toggle');
    const sortMenu = document.getElementById('lib-sort-menu');
    sortToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        sortMenu.style.display = sortMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#lib-sort-menu .dropdown-item').forEach(item => {
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

    const filterToggle = document.getElementById('lib-filter-toggle');
    const filterMenu = document.getElementById('lib-filter-menu');
    filterToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        filterMenu.style.display = filterMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#lib-filter-menu .dropdown-item').forEach(item => {
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
    const label = document.getElementById('lib-sort-label');
    const arrow = sortDirection === 'asc' ? '↑' : '↓';
    if (label) label.innerText = `Sort: ${currentSortOption} ${arrow}`;

    document.querySelectorAll('#lib-sort-menu .dropdown-item').forEach(item => {
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
    const label = document.getElementById('lib-filter-label');
    if (label) label.innerText = `Filter: ${currentFilterOption}`;

    document.querySelectorAll('#lib-filter-menu .dropdown-item').forEach(item => {
        item.classList.toggle('active', item.getAttribute('data-filter') === currentFilterOption);
    });
}

function applyFilterAndSort() {
    filteredGames = libraryGames.filter(game => {
        switch (currentFilterOption) {
            case 'Favorites': return game.IsFavorite;
            case 'Not Favorites': return !game.IsFavorite;
            case 'Installed': return game.IsInstalled;
            case 'Not Installed': return !game.IsInstalled;
            case 'Missing': return !game.IsFolderValid;
            default: return true;
        }
    });

    filteredGames.sort((a, b) => {
        let valA, valB;
        switch (currentSortOption) {
            case 'Release Date':
                valA = a.FirstReleaseDate ? new Date(a.FirstReleaseDate).getTime() : 0;
                valB = b.FirstReleaseDate ? new Date(b.FirstReleaseDate).getTime() : 0;
                break;
            case 'Rating':
                valA = a.Rating || 0;
                valB = b.Rating || 0;
                break;
            case 'Date Added':
                valA = a.DateAdded ? new Date(a.DateAdded).getTime() : 0;
                valB = b.DateAdded ? new Date(b.DateAdded).getTime() : 0;
                break;
            case 'Folder Size':
                valA = a.FolderDiskSpaceBytes || 0;
                valB = b.FolderDiskSpaceBytes || 0;
                break;
            case 'Name':
            default:
                valA = (a.SortName || a.Name || '').toLowerCase();
                valB = (b.SortName || b.Name || '').toLowerCase();
                break;
        }

        if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
        if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
        return 0;
    });

    const countBadge = document.getElementById('lib-count-badge');
    if (countBadge) countBadge.innerText = filteredGames.length;

    renderGrid();
    renderAlphabetBar();
}

function renderGrid() {
    const grid = document.getElementById('lib-games-grid');
    if (!grid) return;
    grid.innerHTML = '';

    filteredGames.forEach(game => {
        const card = document.createElement('div');
        card.className = 'lib-card-container';
        card.setAttribute('data-id', game.Id);

        const firstChar = (game.SortName || game.Name || '').trim().charAt(0).toUpperCase();
        card.setAttribute('data-letter', /\d/.test(firstChar) ? '#' : firstChar);

        let subtitleText = '';
        switch (currentSortOption) {
            case 'Release Date': subtitleText = game.ReleaseDateFormatted; break;
            case 'Rating': subtitleText = `★ ${game.Rating.toFixed(1)}`; break;
            case 'Date Added': subtitleText = game.DateAddedFormatted; break;
            case 'Folder Size': subtitleText = game.FolderSizeFormatted; break;
            case 'Name':
            default:
                subtitleText = game.ReleaseYear;
                break;
        }

        const isMissing = !game.IsFolderValid;

        card.innerHTML = `
            <div class="lib-card-frame" style="background-image: url('${game.CoverUrl}');">
                ${isMissing ? `
                    <div class="lib-missing-overlay">
                        <button type="button" class="menu-action-delete danger" title="Remove Missing Entry">
                            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                            </svg>
                        </button>
                    </div>
                ` : `
                    <div class="lib-card-overlay">
                        <button type="button" class="lib-favorite-btn ${game.IsFavorite ? 'active' : ''}" title="Favorite">
                            ${game.IsFavorite ? '♥' : '♡'}
                        </button>
                        <div style="position: relative; align-self: flex-end;">
                            <button type="button" class="lib-menu-btn" title="Options">⋮</button>
                            <div class="lib-card-context-menu">
                                <button type="button" class="menu-action-refresh">Refresh</button>
                                <button type="button" class="menu-action-edit">Edit</button>
                                <button type="button" class="menu-action-delete danger">Delete</button>
                            </div>
                        </div>
                    </div>
                `}
            </div>
            <div class="lib-card-text">
                <div class="lib-game-title" title="${game.Name}">${game.Name}</div>
                <div class="lib-game-meta">${subtitleText}</div>
            </div>
        `;

        if (isMissing) {
            card.querySelector('.menu-action-delete').addEventListener('click', (e) => {
                e.stopPropagation();
                openDeleteModal(game);
            });
        } else {
            // Normal game card click opens modal with 'local' provider
            card.addEventListener('click', (e) => {
                if (e.target.closest('.lib-favorite-btn') || e.target.closest('.lib-menu-btn') || e.target.closest('.lib-card-context-menu')) {
                    return;
                }
                // Pass gameId and library provider 'local'
                openGameDetails(game.Id, 'local');
            });

            // Favorite toggle
            const favBtn = card.querySelector('.lib-favorite-btn');
            favBtn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const newFav = !game.IsFavorite;
                game.IsFavorite = newFav;
                favBtn.classList.toggle('active', newFav);
                favBtn.innerText = newFav ? '♥' : '♡';

                await fetch(`/api/library/local/${game.Id}/favorite`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(newFav)
                });

                if (currentFilterOption === 'Favorites' || currentFilterOption === 'Not Favorites') {
                    applyFilterAndSort();
                }
            });

            // Context menu toggle
            const menuBtn = card.querySelector('.lib-menu-btn');
            const contextMenu = card.querySelector('.lib-card-context-menu');
            menuBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                document.querySelectorAll('.lib-card-context-menu').forEach(m => {
                    if (m !== contextMenu) m.style.display = 'none';
                });
                contextMenu.style.display = contextMenu.style.display === 'flex' ? 'none' : 'flex';
            });

            // Action: Refresh
            card.querySelector('.menu-action-refresh').addEventListener('click', async (e) => {
                e.stopPropagation();
                contextMenu.style.display = 'none';
                const res = await fetch(`/api/library/local/${game.Id}/refresh`, { method: 'POST' });
                if (res.ok) {
                    const updated = await res.json();
                    Object.assign(game, updated);
                    renderGrid();
                }
            });

            // Action: Edit
            card.querySelector('.menu-action-edit').addEventListener('click', (e) => {
                e.stopPropagation();
                contextMenu.style.display = 'none';
                openEditModal(game);
            });

            // Action: Delete
            card.querySelector('.menu-action-delete').addEventListener('click', (e) => {
                e.stopPropagation();
                contextMenu.style.display = 'none';
                openDeleteModal(game);
            });
        } // Closing the 'else' block correctly!

        grid.appendChild(card);
    });
}

function renderAlphabetBar() {
    const alphabetBar = document.getElementById('lib-alphabet-bar');
    if (!alphabetBar) return;

    if (currentSortOption !== 'Name') {
        alphabetBar.style.display = 'none';
        return;
    }

    alphabetBar.style.display = 'flex';
    alphabetBar.innerHTML = '';

    const letters = new Set();
    filteredGames.forEach(g => {
        const char = (g.SortName || g.Name || '').trim().charAt(0).toUpperCase();
        letters.add(/\d/.test(char) ? '#' : char);
    });

    const sortedLetters = Array.from(letters).sort();

    sortedLetters.forEach(letter => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'alphabet-letter-btn';
        btn.innerText = letter;

        btn.addEventListener('click', () => {
            const targetCard = document.querySelector(`.lib-card-container[data-letter="${letter}"]`);
            if (targetCard) {
                targetCard.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });

        alphabetBar.appendChild(btn);
    });
}

function setupDialogEvents() {
    document.getElementById('lib-edit-close-btn')?.addEventListener('click', () => {
        document.getElementById('lib-edit-modal').classList.remove('active');
    });

    document.getElementById('lib-edit-save-btn')?.addEventListener('click', async () => {
        if (!activeGameForEdit) return;
        const newSortName = document.getElementById('lib-edit-sortname-input').value.trim();
        activeGameForEdit.SortName = newSortName;

        await fetch(`/api/library/local/${activeGameForEdit.Id}/sortname`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(newSortName)
        });

        document.getElementById('lib-edit-modal').classList.remove('active');
        applyFilterAndSort();
    });

    document.getElementById('lib-delete-cancel-btn')?.addEventListener('click', () => {
        document.getElementById('lib-delete-modal').classList.remove('active');
    });

    document.getElementById('lib-delete-confirm-btn')?.addEventListener('click', async () => {
        if (!activeGameForDelete) return;

        await fetch(`/api/library/local/${activeGameForDelete.Id}`, { method: 'DELETE' });
        libraryGames = libraryGames.filter(g => g.Id !== activeGameForDelete.Id);

        document.getElementById('lib-delete-modal').classList.remove('active');
        applyFilterAndSort();
    });
}

function openEditModal(game) {
    activeGameForEdit = game;
    document.getElementById('lib-edit-sortname-input').value = game.SortName || game.Name;
    document.getElementById('lib-edit-modal').classList.add('active');
}

function openDeleteModal(game) {
    activeGameForDelete = game;
    document.getElementById('lib-delete-message').innerHTML = `
        This action will permanently remove <strong>'${game.Name}'</strong> from your library.
        If the folder still exists, it will return to the 'Import from Root Folders' section.<br/><br/>
        Are you sure?
    `;
    document.getElementById('lib-delete-modal').classList.add('active');
}