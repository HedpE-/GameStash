import { showWarning } from './snackbar.js';

/**
 * Loads and renders monitored root storage directories.
 */
export async function initRootFoldersSettingsView() {
    try {
        const response = await fetch('/api/folders', { cache: 'no-store' });
        if (!response.ok) return;
        const folders = await response.json();

        renderFoldersList(folders);
        toggleGlobalRescanButton(folders.length > 0);
        bindFolderInputEvents();
    } catch (err) {
        console.error("Failed to load root folders settings:", err);
    }
}

/**
 * Opens native directory picker dialog to select root folder path.
 */
async function browseFolder() {
    try {
        const response = await fetch('/api/folders/browse', { cache: 'no-store' });
        if (response.ok) {
            const data = await response.json();
            const selectedPath = data.Path || data.path || '';

            if (selectedPath) {
                const input = document.getElementById('new-folder-input');
                if (input) {
                    input.value = selectedPath;
                    toggleAddButtonState();
                }
            }
        } else {
            const errData = await response.json().catch(() => ({}));
            showWarning(errData.Message || "Native folder picker is unavailable in this environment. Please type the path manually.");
        }
    } catch (err) {
        console.error("Browse folder request error:", err);
        showWarning("Network error while trying to open folder picker. Please type the path manually.");
    }
}

function bindFolderInputEvents() {
    const input = document.getElementById('new-folder-input');
    if (input) {
        input.removeEventListener('input', toggleAddButtonState);
        input.addEventListener('input', toggleAddButtonState);
        toggleAddButtonState();
    }
}

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

function toggleGlobalRescanButton(hasFolders) {
    const rescanAllBtn = document.getElementById('rescan-all-btn');
    if (rescanAllBtn) {
        rescanAllBtn.style.display = hasFolders ? 'inline-flex' : 'none';
    }
}

function updateFolderUIState(folderId, isScanning) {
    const cards = document.querySelectorAll('.root-folder-card');
    cards.forEach(card => {
        const cardId = card.getAttribute('data-id');
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

export async function rescanFolder(id = null) {
    updateFolderUIState(id, true);

    try {
        const url = id
            ? `/api/folders/rescan/${encodeURIComponent(id)}`
            : '/api/folders/rescan';

        const method = id ? 'POST' : 'PUT';
        const response = await fetch(url, { method: method });

        if (!response.ok) {
            alert("Failed to initiate folder rescan.");
        }
    } catch (err) {
        console.error("Rescan request failed:", err);
        alert("Failed to initiate folder rescan.");
    } finally {
        await initRootFoldersSettingsView();
    }
}


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
        card.setAttribute('data-id', id);
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

export async function addFolder() {
    const input = document.getElementById('new-folder-input');
    const path = input?.value.trim();
    if (!path) return;

    try {
        const response = await fetch('/api/folders', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(path)
        });

        if (response.ok) {
            input.value = '';
            toggleAddButtonState();
            await initRootFoldersSettingsView();

            if (typeof window.refreshNavbarFoldersVisibility === 'function') {
                await window.refreshNavbarFoldersVisibility();
            }
        } else {
            const err = await response.text();
            alert(`Failed to add folder: ${err}`);
        }
    } catch (err) {
        alert("Network error while registering folder path.");
    }
}

export async function removeFolder(id, pathName = '') {
    const displayName = pathName ? `:\n${pathName}` : '';
    if (!confirm(`Are you sure you want to stop monitoring${displayName}?`)) return;

    try {
        const response = await fetch(`/api/folders/${encodeURIComponent(id)}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            await initRootFoldersSettingsView();
            if (typeof window.refreshNavbarFoldersVisibility === 'function') {
                await window.refreshNavbarFoldersVisibility();
            }
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

// Click Event Delegator for Root Folders Settings
document.addEventListener('click', (e) => {
    if (e.target.id === 'browse-folder-btn') {
        browseFolder();
    } else if (e.target.id === 'add-folder-btn') {
        if (!e.target.disabled) addFolder();
    } else if (e.target.id === 'rescan-all-btn') {
        if (!e.target.disabled) rescanFolder(null);
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