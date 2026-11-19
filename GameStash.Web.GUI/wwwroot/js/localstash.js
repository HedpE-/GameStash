import { createGameCard } from './gamecard.js';
import { openGameDetails } from './gamedetails.js';
import { renderAlphabetBar } from './alphabetbar.js';

let libraryGames = [];
let filteredGames = [];
let currentSortOption = 'Name';
let sortDirection = 'asc';
let currentFilterOption = 'All';

let activeGameForEdit = null;
let activeGameForDelete = null;

export async function initLocalStashView(workspaceElement) {
    setupToolbarEvents();
    setupDialogEvents();
    setupScrollTopEvent();
    await loadStashData();
}

async function loadStashData() {
    const loader = document.getElementById('stash-loading-indicator');
    const workspace = document.getElementById('stash-workspace');
    const countLabel = document.getElementById('stash-count-label');

    if (loader) loader.style.display = 'flex';
    if (workspace) workspace.style.display = 'none';
    if (countLabel) countLabel.innerText = 'Loading games...';

    try {
        // 1. First validate if any Root Folders are registered in the system bounds
        const foldersResponse = await fetch('/api/folders');
        const folders = await foldersResponse.json();
        if (foldersResponse.ok) {
            if (!folders || folders.length === 0) {
                renderFoldersWarning('MissingRootFolders');
                return; // Abort loading further game data indices
            }
        }

        // 2. Fetch local library stash records
        const response = await fetch('/api/stash/local');
        if (!response.ok) throw new Error(`HTTP Error ${response.status}`);

        libraryGames = await response.json();

        // 3. EVALUATE EMPTY STATED DATA: If library has 0 games, check the unimported backlog state
        if (!libraryGames || libraryGames.length === 0) {
            // NEW SCENARIO INTERCEPTOR: Check if any folder is actively scanning right now
            const isAnyScanning = folders.some(f => f.IsImportingFolders === true || f.isImportingFolders === true);
            if (isAnyScanning) {
                renderFoldersWarning('FoldersAreScanning');
                return; // Abort loading library until scanning pipeline completes
            }

            const foundResponse = await fetch('/api/folders/found');

            if (foundResponse.ok) {
                const foundFolders = await foundResponse.json();

                // If there are unimported folders waiting, prompt the user to import them
                if (foundFolders && foundFolders.length > 0) {
                    renderFoldersWarning('HasUnimportedGames');
                } else {
                    // If everything is completely empty, prompt the user that no matches exist
                    renderFoldersWarning('NoGamesFoundInRoots');
                }
                return;
            }
        }

        // Remove any old stale warning boxes if games are now present
        document.querySelector('.stash-empty-warning-box')?.remove();

        // 4. Proceed with standard sorting and rendering pipeline
        applyFilterAndSort();
    } catch (err) {
        console.error("Failed to load local stash data:", err);
        renderFoldersWarning('Error', err.message);
    } finally {
        if (loader) loader.style.display = 'none';
        // Only restore workspace viewport bounds if a warning screen didn't intercept the DOM
        if (workspace && !document.querySelector('.stash-empty-warning-box')) {
            workspace.style.display = 'flex';
        }
    }
}

function setupToolbarEvents() {
    document.getElementById('stash-reload-btn')?.addEventListener('click', () => loadStashData());

    document.getElementById('stash-flush-btn')?.addEventListener('click', async () => {
        if (!confirm("CRITICAL WARNING:\nAre you sure you want to permanently flush the entire PostgreSQL database table?\nThis action cannot be undone.")) {
            return;
        }

        try {
            const response = await fetch('/api/stash/local/', { method: 'DELETE' });
            if (response.ok) {
                alert('Database successfully flushed.');
                await loadStashData();
            } else {
                alert('Failed to clear database records.');
            }
        } catch (err) {
            console.error("Flush transaction encountered a network exception:", err);
        }
    });

    document.getElementById('stash-export-btn')?.addEventListener('click', async () => {
        const textLines = libraryGames.map(g => `${g.Name} (${g.IsFavorite ? 'Favorite | ' : ''}${g.IsInstalled ? 'Installed' : 'Not Installed'})`);
        try {
            await navigator.clipboard.writeText(textLines.join('\n'));
            alert('Games list successfully copied to clipboard!');
        } catch {
            alert('Failed to copy list to clipboard.');
        }
    });

    const sortToggle = document.getElementById('stash-sort-toggle');
    const sortMenu = document.getElementById('stash-sort-menu');
    sortToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        sortMenu.style.display = sortMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#stash-sort-menu .dropdown-item').forEach(item => {
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

    const filterToggle = document.getElementById('stash-filter-toggle');
    const filterMenu = document.getElementById('stash-filter-menu');
    filterToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        filterMenu.style.display = filterMenu.style.display === 'flex' ? 'none' : 'flex';
    });

    document.querySelectorAll('#stash-filter-menu .dropdown-item').forEach(item => {
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
    const label = document.getElementById('stash-sort-label');
    const arrow = sortDirection === 'asc' ? '↑' : '↓';
    if (label) label.innerText = `Sort: ${currentSortOption} ${arrow}`;

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
    if (label) label.innerText = `Filter: ${currentFilterOption}`;

    document.querySelectorAll('#stash-filter-menu .dropdown-item').forEach(item => {
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

    const naturalCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

    filteredGames.sort((a, b) => {
        let valA, valB;
        switch (currentSortOption) {
            case 'Release Date':
                valA = a.FirstReleaseDate ? new Date(a.FirstReleaseDate).getTime() : 0;
                valB = b.FirstReleaseDate ? new Date(b.FirstReleaseDate).getTime() : 0;
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
                return sortDirection === 'asc'
                    ? naturalCollator.compare(valA, valB)
                    : naturalCollator.compare(valB, valA);
        }
    });

    const countLabel = document.getElementById('stash-count-label');
    if (countLabel) {
        const total = libraryGames.length;
        const filtered = filteredGames.length;
        if (currentFilterOption === 'All') {
            countLabel.innerText = `${total} ${total === 1 ? 'game' : 'games'} cataloged`;
        } else {
            countLabel.innerText = `Showing ${filtered} of ${total} games (${currentFilterOption.toLowerCase()})`;
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
        const card = createGameCard(
            game,
            currentSortOption,
            openDeleteModal,
            openEditModal,
            () => {
                if (currentFilterOption === 'Favorites' || currentFilterOption === 'Not Favorites') {
                    applyFilterAndSort();
                }
            },
            'local'
        );
        grid.appendChild(card);
    });
}

/**
 * Renders a full-screen warning layout when no root folders are monitored.
 */
function renderFoldersWarning(scenario, detailMessage = '') {
    const container = document.querySelector('.stash-container');
    const loader = document.getElementById('stash-loading-indicator');
    const workspace = document.getElementById('stash-workspace');
    const countLabel = document.getElementById('stash-count-label');

    if (loader) loader.style.display = 'none';
    if (workspace) workspace.style.display = 'none';

    // Cleanup previous warning instances to block duplications
    document.querySelector('.stash-empty-warning-box')?.remove();

    let icon = '📂';
    let title = 'No Monitored Folders Found';
    let text = 'You have not configured any root directories to monitor for local game installations.';
    let subtext = 'Please navigate to the <strong>Global Settings</strong> panel to add your first folder path and begin indexing your library.';
    let btnText = 'Go to Settings';
    let targetView = 'settings';
    let hideButton = false;

    if (scenario === 'Error') {
        icon = '⚠️';
        title = 'Failed to Load Local Stash';
        text = detailMessage || 'A network error occurred while communicating with the backend local stash service.';
        subtext = 'Check the application logs or verify your database connection in Global Settings.';
        btnText = 'Go to Settings';
        targetView = 'settings';
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
        targetView = 'settings';
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
    warningBox.innerHTML = `
        <div class="warning-icon">${icon}</div>
        <h2>${title}</h2>
        <p>${text}</p>
        <p class="warning-subtext">${subtext}</p>
        ${hideButton ? '' : `<button class="nav-to-settings-btn" id="stash-warn-redirect-btn">${btnText}</button>`}
    `;

    container.appendChild(warningBox);

    document.getElementById('stash-warn-redirect-btn')?.addEventListener('click', () => {
        import('./router.js').then(m => m.navigateTo(targetView));
    });
}

function setupScrollTopEvent() {
    const workspace = document.getElementById('stash-workspace');
    const scrollTopBtn = document.getElementById('stash-scroll-top-btn');
    const alphabetBar = document.getElementById('stash-alphabet-bar');

    if (!workspace || !scrollTopBtn) return;

    // Função centralizada para avaliar visibilidade e colisões geométricas
    const handleWorkspaceScroll = () => {
        // 1. Controlo de Visibilidade Base (Surgir após 300px)
        if (workspace.scrollTop > 300) {
            scrollTopBtn.classList.add('visible');
        } else {
            scrollTopBtn.classList.remove('visible');
            scrollTopBtn.classList.remove('shifted-left'); // Limpa se voltar ao topo
            return;
        }

        // 2. Motor de Colisão Dinâmico com a Alphabet Bar
        if (alphabetBar && window.getComputedStyle(alphabetBar).display !== 'none') {
            const btnRect = scrollTopBtn.getBoundingClientRect();
            const barRect = alphabetBar.getBoundingClientRect();

            // Se o topo do botão ultrapassar o limite inferior da barra do alfabeto,
            // ou se as áreas verticais se sobrepuserem de alguma forma:
            if (btnRect.top <= barRect.bottom && btnRect.bottom >= barRect.top) {
                scrollTopBtn.classList.add('shifted-left');
            } else {
                scrollTopBtn.classList.remove('shifted-left');
            }
        } else {
            scrollTopBtn.classList.remove('shifted-left');
        }
    };

    // Ouve tanto o Scroll como o Resize da janela para recalcular posições instantaneamente
    workspace.addEventListener('scroll', handleWorkspaceScroll);
    window.addEventListener('resize', handleWorkspaceScroll);

    // Suporta também o clique suave original
    scrollTopBtn.addEventListener('click', () => {
        workspace.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
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
        activeGameForEdit.SortName = newSortName;

        await fetch(`/api/stash/local/${activeGameForEdit.Id}/details`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ propertyName: 'SortName', value: newSortName })
        });

        if (editModal) editModal.classList.remove('active');
        applyFilterAndSort();
    });

    editInput?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            editSaveBtn?.click();
        }
    });

    document.getElementById('stash-delete-cancel-btn')?.addEventListener('click', () => {
        document.getElementById('stash-delete-modal').classList.remove('active');
    });

    document.getElementById('stash-delete-confirm-btn')?.addEventListener('click', async () => {
        if (!activeGameForDelete) return;

        await fetch(`/api/stash/local/${activeGameForDelete.Id}`, { method: 'DELETE' });
        libraryGames = libraryGames.filter(g => g.Id !== activeGameForDelete.Id);

        document.getElementById('stash-delete-modal').classList.remove('active');
        applyFilterAndSort();
    });
}

function openEditModal(game) {
    activeGameForEdit = game;
    const input = document.getElementById('stash-edit-sortname-input');
    if (input) {
        input.value = game.SortName || game.Name;
        document.getElementById('stash-edit-modal').classList.add('active');
        input.focus();
    }
}

function openDeleteModal(game) {
    activeGameForDelete = game;
    document.getElementById('stash-delete-message').innerHTML = `
        This action will permanently remove <strong>'${game.Name}'</strong> from your stash.
        If the folder still exists, it will return to the 'Import from Root Folders' section.<br/><br/>
        Are you sure?
    `;
    document.getElementById('stash-delete-modal').classList.add('active');
}
