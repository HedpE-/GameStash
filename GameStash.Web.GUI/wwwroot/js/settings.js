// Active polling interval reference
let scanPollingTimer = null;

/**
 * Loads and renders application configurations and monitored folders.
 */
export async function loadSettings() {
    try {
        const clientIdInput = document.getElementById('client-id-input');
        const secretInput = document.getElementById('secret-input');

        // 1. Fetch IGDB credentials
        let response = await fetch('/api/settings');
        if (response.ok) {
            const data = await response.json();
            if (clientIdInput) clientIdInput.value = data.igdbClientId || data.IgdbClientId || '';
            if (secretInput) secretInput.value = data.igdbSecret || data.IgdbSecret || '';
        }

        // 2. Fetch Root Folders from new RootFoldersController
        response = await fetch('/api/folders');
        if (!response.ok) return;
        const folders = await response.json();

        // Render root folders with disk storage statistics and scanning indicators
        renderFoldersList(folders);

        // Update global rescan button state
        toggleGlobalRescanButton(folders.length > 0);

        // Check if any folder is currently being imported/scanned
        const isAnyScanning = folders.some(f => f.IsImportingFolders === true || f.isImportingFolders === true);
        manageScanPolling(isAnyScanning);

        // Bind input dynamic events
        bindFolderInputEvents();
    } catch (err) {
        console.error("Failed to load settings:", err);
    }
}

/**
 * Automatically polls the settings endpoint while any folder scan is ongoing.
 */
function manageScanPolling(isScanning) {
    if (isScanning) {
        if (!scanPollingTimer) {
            scanPollingTimer = setInterval(() => {
                loadSettings();
            }, 2000);
        }
    } else {
        if (scanPollingTimer) {
            clearInterval(scanPollingTimer);
            scanPollingTimer = null;
        }
    }
}

/**
 * Attaches the typing event listener to toggle the Add button state.
 */
function bindFolderInputEvents() {
    const input = document.getElementById('new-folder-input');
    if (input) {
        input.removeEventListener('input', toggleAddButtonState);
        input.addEventListener('input', toggleAddButtonState);
        toggleAddButtonState();
    }
}

/**
 * Dynamically toggles the disabled status of the Add Folder button based on text input.
 */
function toggleAddButtonState() {
    const input = document.getElementById('new-folder-input');
    const addFolderBtn = document.getElementById('add-folder-btn');
    if (input && addFolderBtn) {
        const isEmpty = !input.value.trim();
        addFolderBtn.disabled = isEmpty;
        addFolderBtn.style.opacity = isEmpty ? '0.5' : '1';
        addFolderBtn.style.cursor = isEmpty ? 'not-allowed' : 'pointer';
    }
}

/**
 * Toggles the disabled status of the Rescan All button based on folder count.
 */
function toggleGlobalRescanButton(hasFolders) {
    const rescanAllBtn = document.getElementById('rescan-all-btn');
    if (rescanAllBtn) {
        rescanAllBtn.disabled = !hasFolders;
        rescanAllBtn.style.opacity = !hasFolders ? '0.5' : '1';
        rescanAllBtn.style.cursor = !hasFolders ? 'not-allowed' : 'pointer';
    }
}

/**
 * Immediately updates card UI to reflect scanning state by folder ID or across all cards.
 */
function updateFolderUIState(folderId, isScanning) {
    const cards = document.querySelectorAll('.root-folder-card');
    cards.forEach(card => {
        const cardId = card.getAttribute('data-id');
        // Match specific card by ID, or update all if folderId is null
        if (!folderId || cardId === String(folderId)) {
            const badgeContainer = card.querySelector('.root-folder-badges');
            const rescanBtn = card.querySelector('.btn-rescan');

            if (isScanning) {
                if (badgeContainer) {
                    badgeContainer.innerHTML = `<span class="badge-tag scanning"><span class="spinner-small"></span> Scanning folder...</span>`;
                }
                if (rescanBtn) {
                    rescanBtn.disabled = true;
                    rescanBtn.textContent = 'Scanning...';
                    rescanBtn.style.opacity = '0.5';
                    rescanBtn.style.cursor = 'not-allowed';
                }
            }
        }
    });
}

/**
 * Triggers a folder rescan via the new controller endpoints:
 * - If id is provided: POST /api/folders/rescan/{id}
 * - If id is null (global): PUT /api/folders/rescan
 */
export async function rescanFolder(id = null) {
    // 1. Instant feedback in DOM
    updateFolderUIState(id, true);

    try {
        const url = id
            ? `/api/folders/rescan/${encodeURIComponent(id)}`
            : '/api/folders/rescan';

        const method = id ? 'POST' : 'PUT';

        const response = await fetch(url, { method: method });

        if (response.status === 202 || response.ok) {
            manageScanPolling(true);
            setTimeout(loadSettings, 500);
        } else {
            alert("Failed to initiate folder rescan.");
            loadSettings();
        }
    } catch (err) {
        console.error("Rescan request failed:", err);
        alert("Failed to initiate folder rescan.");
        loadSettings();
    }
}

/**
 * Renders the list of root folders including disk metrics, badges, and scan indicators.
 */
function renderFoldersList(folders) {
    const container = document.getElementById('folders-list');
    if (!container) return;
    container.innerHTML = '';

    if (!folders || folders.length === 0) {
        container.innerHTML = '<div class="empty-state">No monitored root folders configured. Add one above to begin indexing.</div>';
        return;
    }

    folders.forEach(folder => {
        const id = folder.Id ?? folder.id ?? 0;
        const path = folder.Path || folder.path || '';
        const count = folder.FoundGamesCount ?? folder.foundGamesCount ?? 0;
        const usedSpace = folder.UsedSpace ?? folder.usedSpace ?? 0;
        const freeSpace = folder.FreeSpace ?? folder.freeSpace ?? 0;
        const isImporting = folder.IsImportingFolders ?? folder.isImportingFolders ?? false;
        const totalSpace = folder.TotalSpace ?? folder.totalSpace ?? (usedSpace + freeSpace);

        const statusBadgeHtml = isImporting
            ? `<span class="badge-tag scanning"><span class="spinner-small"></span> Scanning folder...</span>`
            : `<span class="badge-tag highlight">${count} game folder(s) found</span>`;

        let diskSectionHtml = '';

        if (totalSpace > 0) {
            const usedPercent = Math.min(100, Math.round((usedSpace / totalSpace) * 100));
            const formattedUsed = formatBytes(usedSpace);
            const formattedFree = formatBytes(freeSpace);
            const formattedTotal = formatBytes(totalSpace);

            let fillClass = '';
            if (usedPercent >= 90) fillClass = 'fill-danger';
            else if (usedPercent >= 75) fillClass = 'fill-warning';

            diskSectionHtml = `
                <div class="disk-storage-section">
                    <div class="storage-labels">
                        <span class="storage-usage-text">
                            <strong>${formattedFree} free</strong> of ${formattedTotal} (${formattedUsed} used)
                        </span>
                        <span class="storage-percent">${usedPercent}%</span>
                    </div>
                    <div class="storage-progress-track">
                        <div class="storage-progress-fill ${fillClass}" style="width: ${usedPercent}%;"></div>
                    </div>
                </div>
            `;
        }

        const card = document.createElement('div');
        card.className = 'root-folder-card';
        card.setAttribute('data-id', id); // Stored for targeted DOM manipulation
        card.innerHTML = `
            <div class="root-folder-main">
                <div class="root-folder-left">
                    <span class="root-folder-path">${escapeHtml(path)}</span>
                    <div class="root-folder-badges">
                        ${statusBadgeHtml}
                    </div>
                </div>
                <div class="folder-actions">
                    <button class="btn-icon btn-rescan" data-action="rescan" data-id="${id}" ${isImporting ? 'disabled style="opacity:0.5;cursor:not-allowed;"' : ''}>
                        ${isImporting ? 'Scanning...' : '↻ Rescan'}
                    </button>
                    <button class="btn-icon btn-danger" data-action="delete" data-id="${id}" data-path="${encodeURIComponent(path)}">Remove</button>
                </div>
            </div>
            ${diskSectionHtml}
        `;

        container.appendChild(card);
    });
}

/**
 * Saves IGDB API credentials.
 */
export async function saveCredentials() {
    const clientId = document.getElementById('client-id-input')?.value.trim() || '';
    const secret = document.getElementById('secret-input')?.value.trim() || '';

    try {
        const response = await fetch('/api/settings/credentials', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IgdbClientId: clientId, IgdbSecret: secret })
        });

        if (response.ok) {
            alert("IGDB credentials successfully synchronized.");
            loadSettings();
        } else {
            alert("Failed to commit IGDB credentials.");
        }
    } catch (err) {
        alert("Network error while persisting settings.");
    }
}

/**
 * Adds a new root folder for background monitoring and indexing.
 */
export async function addFolder() {
    const input = document.getElementById('new-folder-input');
    const path = input?.value.trim();
    if (!path) return;

    try {
        // Sends raw string in body as expected by [FromBody] string path
        const response = await fetch('/api/folders', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(path)
        });

        if (response.ok) {
            input.value = '';
            toggleAddButtonState();
            await loadSettings();
        } else {
            const err = await response.text();
            alert(`Failed to add folder: ${err}`);
        }
    } catch (err) {
        alert("Network error while registering folder path.");
    }
}

/**
 * Removes a root folder from tracking using DELETE /api/folders/{id}.
 */
export async function removeFolder(id, pathName = '') {
    const displayName = pathName ? `:\n${pathName}` : '';
    if (!confirm(`Are you sure you want to stop monitoring${displayName}?`)) return;

    try {
        const response = await fetch(`/api/folders/${encodeURIComponent(id)}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            await loadSettings();
        } else {
            alert("Failed to remove root folder.");
        }
    } catch (err) {
        alert("Network error while removing root folder.");
    }
}

function formatBytes(bytes, decimals = 1) {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// Global Single Click Event Delegator
document.addEventListener('click', (e) => {
    if (e.target.id === 'save-credentials-btn') {
        saveCredentials();
    } else if (e.target.id === 'add-folder-btn') {
        if (!e.target.disabled) addFolder();
    } else if (e.target.id === 'rescan-all-btn') {
        if (!e.target.disabled) rescanFolder(null); // Rescan all
    } else {
        const action = e.target.dataset.action;
        const id = e.target.dataset.id;
        const rawPath = e.target.dataset.path;

        if (action && id) {
            if (action === 'delete') {
                const path = rawPath ? decodeURIComponent(rawPath) : '';
                removeFolder(id, path);
            }
            if (action === 'rescan') {
                rescanFolder(id);
            }
        }
    }
});

// Enter key shortcut inside the path input
document.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && document.activeElement && document.activeElement.id === 'new-folder-input') {
        const addFolderBtn = document.getElementById('add-folder-btn');
        if (addFolderBtn && !addFolderBtn.disabled) {
            addFolder();
        }
    }
});