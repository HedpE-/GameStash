import { showSuccess, showError, showWarning } from './snackbar.js';

// Active polling interval reference
let scanPollingTimer = null;

const API_HELP_GUIDES = {
    igdb: {
        title: 'How to Obtain Twitch / IGDB Credentials',
        html: `
            <p>IGDB requires a Twitch Developer Application to authenticate requests.</p>
            <ol>
                <li>Visit the <a href="https://dev.twitch.tv/console" target="_blank" rel="noopener noreferrer">Twitch Developer Console</a> and log in with your Twitch account.</li>
                <li>Click <strong>Register Your Application</strong>.</li>
                <li>Set <strong>Name</strong> to e.g. <code>GameStash</code>.</li>
                <li>Set <strong>OAuth Redirect URLs</strong> to <code>http://localhost</code>.</li>
                <li>Set <strong>Category</strong> to <strong>Application Integration</strong>.</li>
                <li>Click <strong>Create</strong>, then click <strong>Manage</strong> on your new application.</li>
                <li>Copy the <strong>Client ID</strong> and paste it into the settings.</li>
                <li>Click <strong>New Secret</strong> to generate your <strong>Client Secret</strong>, copy it, and paste it into the settings.</li>
                <li>Click <strong>Save Credentials</strong> to establish connection.</li>
            </ol>
        `
    },
    steam: {
        title: 'How to Obtain Steam Credentials',
        html: `
            <h4>1. Steam Web API Key</h4>
            <ol>
                <li>Visit the <a href="https://steamcommunity.com/dev/apikey" target="_blank" rel="noopener noreferrer">Steam Community Developer API Key</a> page and log in.</li>
                <li>In the <strong>Domain Name</strong> field, enter <code>localhost</code> (or <code>gamestash.local</code>).</li>
                <li>Agree to the terms and click <strong>Register</strong>.</li>
                <li>Copy the generated <strong>32-character hexadecimal key</strong>.</li>
            </ol>

            <h4>2. SteamID64</h4>
            <ol>
                <li>In your Steam client or browser, open your Steam profile.</li>
                <li>If your profile URL is in the format <code>steamcommunity.com/profiles/76561198...</code>, that 17-digit number is your <strong>SteamID64</strong>.</li>
                <li>If you use a custom URL, visit <a href="https://steamid.io" target="_blank" rel="noopener noreferrer">steamid.io</a>, paste your profile link, and copy the <strong>steamID64</strong> value.</li>
                <li>Paste both values into the settings and click <strong>Save Steam Credentials</strong>.</li>
            </ol>
        `
    },
    epic: {
        title: 'How to Connect Your Epic Games Account',
        html: `
            <ol>
                <li>Click <strong>1. Open Epic Login</strong> to open the official Epic Games authentication page.</li>
                <li>Sign in with your standard Epic Games account credentials.</li>
                <li>Upon login, the browser will display a raw JSON response containing an <code>authorizationCode</code> field.</li>
                <li>Copy the entire JSON text (or just the 32-character code inside <code>authorizationCode</code>).</li>
                <li>Paste it into the <strong>Epic Authorization Code or JSON</strong> field in GameStash.</li>
                <li>Click <strong>2. Authenticate Epic</strong> to exchange the code and link your library.</li>
            </ol>
        `
    },
    gog: {
        title: 'How to Connect Your GOG Account',
        html: `
            <ol>
                <li>Click <strong>1. Open GOG Login</strong> (or open the official GOG login page).</li>
                <li>Sign in with your standard GOG.com account credentials.</li>
                <li>Upon successful login, you will be redirected to a blank page starting with: 
                    <br><code>https://embed.gog.com/on_login_success?origin=client&code=...</code></li>
                <li>Copy the entire URL from your browser's address bar (or just the code value).</li>
                <li>Paste it into the <strong>GOG Login Code</strong> input field in GameStash.</li>
                <li>Click <strong>2. Authenticate GOG</strong> to finish connection.</li>
            </ol>
        `
    }
};

/**
 * Loads and renders application configurations and monitored folders.
 */
export async function loadSettings() {
    try {
        const igdbClientIdInput = document.getElementById('client-id-input');
        const igdbSecretInput = document.getElementById('secret-input');
        const steamIdInput = document.getElementById('steam-id-input');
        const steamKeyInput = document.getElementById('steam-api-key-input');
        const epicAuthCodeInput = document.getElementById('epic-auth-code-input');
        const gogAuthCodeInput = document.getElementById('gog-auth-code-input');

        let response = await fetch('/api/settings');
        if (response.ok) {
            const data = await response.json();

            if (igdbClientIdInput) {
                igdbClientIdInput.value = data.igdb_Client_ID || data.IGDB_Client_ID || data.igdB_Client_ID || '';
            }
            if (igdbSecretInput) {
                igdbSecretInput.value = data.igdb_Secret || data.IGDB_Secret || data.igdB_Secret || '';
            }

            if (steamIdInput) {
                steamIdInput.value = data.steamId64 || data.SteamId64 || '';
            }
            if (steamKeyInput) {
                steamKeyInput.value = data.steamApiKey || data.SteamApiKey || '';
            }

            if (epicAuthCodeInput) {
                epicAuthCodeInput.value = '';
            }

            if (gogAuthCodeInput) {
                gogAuthCodeInput.value = '';
            }
        }

        await updateIgdbApiStatusLabel();
        await updateSteamApiStatusLabel();
        await updateEpicApiStatusLabel();
        try {
            const epicConfigRes = await fetch('/api/settings/epic/clientid');
            if (epicConfigRes.ok) {
                const epicConfig = await epicConfigRes.json();
                const clientId = epicConfig.ClientId || epicConfig.clientId;
                const loginUrl = epicConfig.LoginUrl || epicConfig.loginUrl;

                const openEpicLoginBtn = document.getElementById('open-epic-login-btn');
                if (openEpicLoginBtn) {
                    openEpicLoginBtn.href = loginUrl;
                }
            }
        } catch (err) {
            console.error("Failed to fetch Epic client configuration:", err);
        }
        await updateGogApiStatusLabel();

        response = await fetch('/api/folders');
        if (!response.ok) return;
        const folders = await response.json();

        renderFoldersList(folders);
        toggleGlobalRescanButton(folders.length > 0);

        const isAnyScanning = folders.some(f => f.IsImportingFolders === true || f.isImportingFolders === true);
        manageScanPolling(isAnyScanning);

        bindFolderInputEvents();
    } catch (err) {
        console.error("Failed to load settings:", err);
    }
}

export function openHelpModal(provider) {
    const modal = document.getElementById('settings-help-modal');
    const titleEl = document.getElementById('help-modal-title');
    const bodyEl = document.getElementById('help-modal-body');

    const guide = API_HELP_GUIDES[provider];
    if (!guide || !modal) return;

    if (titleEl) titleEl.textContent = guide.title;
    if (bodyEl) bodyEl.innerHTML = guide.html;

    modal.classList.add('active');
}

export function closeHelpModal() {
    const modal = document.getElementById('settings-help-modal');
    if (modal) modal.classList.remove('active');
}

async function updateSteamApiStatusLabel() {
    const statusLabel = document.getElementById('settings-steam-api-status');
    if (!statusLabel) return;

    try {
        const response = await fetch('/api/settings/steam/status', { cache: 'no-store' });

        if (response.ok) {
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = '🟢 Steam connection active. Account integration is operational.';
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';

            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Enter your Steam Web API Key and SteamID64 above.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = `🔴 Steam authentication failed.<br><span class="api-raw-error">${escapeHtml(message)}</span>`;
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with the Steam verification service.';
    }
}

export async function saveSteamCredentials() {
    const apiKey = document.getElementById('steam-api-key-input')?.value.trim() || '';
    const steamId = document.getElementById('steam-id-input')?.value.trim() || '';

    if (!apiKey || !steamId) {
        showError("Both Steam Web API Key and SteamID64 fields are required.");
        return;
    }

    try {
        const response = await fetch('/api/settings/steam/credentials', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                UserClientId: steamId,
                Secret: apiKey,
                SteamId: steamId,
                SteamApiKey: apiKey
            })
        });

        if (response.ok) {
            showSuccess("Steam credentials successfully saved.");
            await loadSettings();

            if (typeof window.refreshNavbarStatus === 'function') {
                await window.refreshNavbarStatus();
            }
        } else {
            const resData = await response.json().catch(() => ({}));
            showError(`Failed to save Steam credentials. ${resData.Message || resData.message || ''}`);
            await updateSteamApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while persisting Steam credentials.");
        await updateSteamApiStatusLabel();
    }
}

async function updateIgdbApiStatusLabel() {
    const statusLabel = document.getElementById('settings-api-status');
    if (!statusLabel) return;

    try {
        const response = await fetch('/api/settings/igdb/status', { cache: 'no-store' });

        if (response.ok) {
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = '🟢 Connection active. IGDB integration is operational.';
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';

            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Please fill in your credentials above and save to activate system features.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = `🔴 Authentication failed.<br><span class="api-raw-error">${escapeHtml(message)}</span>`;
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with the core authentication subsystem.';
    }
}

export async function saveIgdbCredentials() {
    const clientId = document.getElementById('client-id-input')?.value.trim() || '';
    const secret = document.getElementById('secret-input')?.value.trim() || '';

    if (!clientId || !secret) {
        showError("Both Client ID and Client Secret fields are mandatory.");
        return;
    }

    try {
        const response = await fetch('/api/settings/igdb/credentials', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ UserClientId: clientId, Secret: secret })
        });

        if (response.ok) {
            showSuccess("IGDB credentials successfully stored.");
            await loadSettings();

            if (typeof window.refreshNavbarFoldersVisibility === 'function') {
                window.location.reload();
            }
        } else {
            let resData = await response.json().catch(() => ({}));
            showError(`Failed to commit IGDB credentials. ${resData.Message || resData.message || ''}`);
            await updateApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while persisting settings.");
        await updateApiStatusLabel();
    }
}

/**
 * Evaluates Epic Games connection status.
 */
async function updateEpicApiStatusLabel() {
    const statusLabel = document.getElementById('settings-epic-api-status');
    if (!statusLabel) return;

    try {
        const response = await fetch('/api/settings/epic/status', { cache: 'no-store' });

        if (response.ok) {
            const data = await response.json();
            const displayName = data.DisplayName || data.displayName || data.Username || 'User';
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = `🟢 Epic Games connection active. Logged in as <strong>${escapeHtml(displayName)}</strong>.`;
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';

            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Click "1. Open Epic Login" and paste the authorization code above.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = `🔴 Epic authentication failed.<br><span class="api-raw-error">${escapeHtml(message)}</span>`;
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with Epic validation service.';
    }
}

/**
 * Saves Epic authorization code and exchanges it for tokens.
 */
export async function saveEpicCredentials() {
    const codeInput = document.getElementById('epic-auth-code-input');
    const rawCode = codeInput?.value.trim() || '';

    if (!rawCode) {
        showError("Please paste the Epic authorization code or JSON response.");
        return;
    }

    try {
        const response = await fetch('/api/settings/epic/credentials', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Secret: rawCode, UserClientId: "epic" })
        });

        if (response.ok) {
            showSuccess("Epic Games account successfully authenticated.");
            if (codeInput) codeInput.value = '';
            await updateEpicApiStatusLabel();

            if (typeof window.refreshNavbarStatus === 'function') {
                await window.refreshNavbarStatus();
            }
        } else {
            const resData = await response.json().catch(() => ({}));
            showError(`Failed to authenticate Epic: ${resData.Message || resData.message || 'Invalid code'}`);
            await updateEpicApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while authenticating Epic credentials.");
        await updateEpicApiStatusLabel();
    }
}

/**
 * Evaluates GOG connection status.
 */
async function updateGogApiStatusLabel() {
    const statusLabel = document.getElementById('settings-gog-api-status');
    if (!statusLabel) return;

    try {
        const response = await fetch('/api/settings/gog/status', { cache: 'no-store' });

        if (response.ok) {
            const data = await response.json();
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = `🟢 GOG connection active. Logged in as <strong>${escapeHtml(data.Username || 'User')}</strong>.`;
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';

            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Click "Open GOG Login" and paste the authorization code above.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = `🔴 GOG authentication failed.<br><span class="api-raw-error">${escapeHtml(message)}</span>`;
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with GOG validation service.';
    }
}

/**
 * Saves GOG login code and exchanges it for tokens.
 */
export async function saveGogCredentials() {
    const codeInput = document.getElementById('gog-auth-code-input');
    const rawCode = codeInput?.value.trim() || '';

    if (!rawCode) {
        showError("Please paste the GOG login code or redirect URL.");
        return;
    }

    try {
        const response = await fetch('/api/settings/gog/credentials', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Secret: rawCode, UserClientId: "gog" })
        });

        if (response.ok) {
            showSuccess("GOG account successfully authenticated.");
            if (codeInput) codeInput.value = '';
            await updateGogApiStatusLabel();

            if (typeof window.refreshNavbarStatus === 'function') {
                await window.refreshNavbarStatus();
            }
        } else {
            const resData = await response.json().catch(() => ({}));
            showError(`Failed to authenticate GOG: ${resData.Message || 'Invalid code'}`);
            await updateGogApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while authenticating GOG credentials.");
        await updateGogApiStatusLabel();
    }
}

/**
 * Opens native directory picker dialog to select root folder path.
 */
async function browseFolder() {
    try {
        const response = await fetch('/api/folders/browse');
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
            await loadSettings();
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
    const helpBtn = e.target.closest('[data-help]');
    if (helpBtn) {
        openHelpModal(helpBtn.dataset.help);
        return;
    }

    if (e.target.id === 'save-igdb-credentials-btn') {
        saveIgdbCredentials();
    } else if (e.target.id === 'save-steam-credentials-btn') {
        saveSteamCredentials();
    } else if (e.target.id === 'save-epic-credentials-btn') {
        saveEpicCredentials();
    } else if (e.target.id === 'save-gog-credentials-btn') {
        saveGogCredentials();
    } else if (e.target.id === 'browse-folder-btn') {
        browseFolder();
    } else if (e.target.id === 'help-modal-close-btn' || e.target.id === 'help-modal-close-icon') {
        closeHelpModal();
    } else if (e.target.id === 'settings-help-modal') {
        closeHelpModal();
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

// Close help modal on Escape key press
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeHelpModal();
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