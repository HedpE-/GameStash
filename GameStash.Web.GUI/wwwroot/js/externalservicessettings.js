import { showSuccess, showError } from './snackbar.js';

const API_HELP_GUIDES = {
    igdb: {
        title: 'How to Obtain Twitch / IGDB Credentials',
        html: `
            <p>IGDB requires a Twitch Developer Application to authenticate requests.</p>
            <ol>
                <li>Visit the <a href="https://twitch.tv" target="_blank" rel="noopener noreferrer">Twitch Developer Console</a> and log in with your Twitch account.</li>
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
                <li>Visit the <a href="https://steamcommunity.com" target="_blank" rel="noopener noreferrer">Steam Community Developer API Key</a> page and log in.</li>
                <li>In the <strong>Domain Name</strong> field, enter <code>localhost</code> (or <code>gamestash.local</code>).</li>
                <li>Agree to the terms and click <strong>Register</strong>.</li>
                <li>Copy the generated <strong>32-character hexadecimal key</strong>.</li>
            </ol>

            <h4>2. SteamID64</h4>
            <ol>
                <li>In your Steam client or browser, open your Steam profile.</li>
                <li>If your profile URL is in the format <code>://steamcommunity.com...</code>, that 17-digit number is your <strong>SteamID64</strong>.</li>
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
                    <br><code>https://gog.com...</code></li>
                <li>Copy the entire URL from your browser's address bar (or just the code value).</li>
                <li>Paste it into the <strong>GOG Login Code</strong> input field in GameStash.</li>
                <li>Click <strong>2. Authenticate GOG</strong> to finish connection.</li>
            </ol>
        `
    },
    amazon: {
        title: 'How to Connect Your Amazon Games Account',
        html: `
            <ol>
                <li>Click <strong>1. Open Amazon Login</strong> to open the official Amazon Games OAuth sign-in page.</li>
                <li>Sign in with your Amazon account credentials (dismiss any Passkey prompt if shown).</li>
                <li>Upon successful login, the browser will stop on a blank page starting with:
                    <br><code>https://amazon.com...</code></li>
                <li>Copy the <strong>entire URL</strong> from your browser's address bar.</li>
                <li>Paste it into the <strong>Amazon Redirect URL or Authorization Code</strong> field in GameStash.</li>
                <li>Click <strong>2. Authenticate Amazon</strong> to finish linking your library.</li>
            </ol>
        `
    }
};

/**
 * Loads and renders external service credentials and validation statuses.
 */
export async function initExternalServicesSettingsView() {
    try {
        const igdbClientIdInput = document.getElementById('client-id-input');
        const igdbSecretInput = document.getElementById('secret-input');
        const steamIdInput = document.getElementById('steam-id-input');
        const steamKeyInput = document.getElementById('steam-api-key-input');
        const epicAuthCodeInput = document.getElementById('epic-auth-code-input');
        const gogAuthCodeInput = document.getElementById('gog-auth-code-input');
        const amazonAuthCodeInput = document.getElementById('amazon-auth-code-input');

        const response = await fetch('/api/settings', { cache: 'no-store' });
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

            if (epicAuthCodeInput) epicAuthCodeInput.value = '';
            if (gogAuthCodeInput) gogAuthCodeInput.value = '';
            if (amazonAuthCodeInput) amazonAuthCodeInput.value = '';
        }

        await updateIgdbApiStatusLabel();
        await updateSteamApiStatusLabel();
        await updateEpicApiStatusLabel();

        try {
            const epicConfigRes = await fetch('/api/settings/epic/clientid', { cache: 'no-store' });
            if (epicConfigRes.ok) {
                const epicConfig = await epicConfigRes.json();
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
        await updateAmazonApiStatusLabel();
        await refreshAmazonLoginUrl();
    } catch (err) {
        console.error("Failed to load external services settings:", err);
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
            await initExternalServicesSettingsView();
            if (typeof window.refreshNavbarStatus === 'function') {
                await window.refreshNavbarStatus();
            }
        } else {
            const resData = await response.json().catch(() => ({}));
            showError('Failed to commit IGDB credentials. ' + (resData.Message || resData.message || ''));
            await updateIgdbApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while persisting settings.");
        await updateIgdbApiStatusLabel();
    }
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
                statusLabel.innerHTML = '🔴 Steam authentication failed.<br><span class="api-raw-error">' + escapeHtml(message) + '</span>';
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
            await initExternalServicesSettingsView();
            if (typeof window.refreshNavbarStatus === 'function') {
                await window.refreshNavbarStatus();
            }
        } else {
            const resData = await response.json().catch(() => ({}));
            showError('Failed to save Steam credentials. ' + (resData.Message || resData.message || ''));
            await updateSteamApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while persisting Steam credentials.");
        await updateSteamApiStatusLabel();
    }
}
async function updateEpicApiStatusLabel() {
    const statusLabel = document.getElementById('settings-epic-api-status');
    if (!statusLabel) return;
    try {
        const response = await fetch('/api/settings/epic/status', { cache: 'no-store' });
        if (response.ok) {
            const data = await response.json();
            const displayName = data.DisplayName || data.displayName || data.Username || 'User';
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = '🟢 Epic Games connection active. Logged in as <strong>' + escapeHtml(displayName) + '</strong>.';
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';
            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Click "1. Open Epic Login" and paste the authorization code above.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = '🔴 Epic authentication failed.<br><span class="api-raw-error">' + escapeHtml(message) + '</span>';
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with Epic validation service.';
    }
}
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
            showError('Failed to authenticate Epic: ' + (resData.Message || resData.message || 'Invalid code'));
            await updateEpicApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while authenticating Epic credentials.");
        await updateEpicApiStatusLabel();
    }
}
async function updateGogApiStatusLabel() {
    const statusLabel = document.getElementById('settings-gog-api-status');
    if (!statusLabel) return;
    try {
        const response = await fetch('/api/settings/gog/status', { cache: 'no-store' });
        if (response.ok) {
            const data = await response.json();
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = '🟢 GOG connection active. Logged in as <strong>' + escapeHtml(data.Username || 'User') + '</strong>.';
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';
            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Click "Open GOG Login" and paste the authorization code above.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = '🔴 GOG authentication failed.<br><span class="api-raw-error">' + escapeHtml(message) + '</span>';
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with GOG validation service.';
    }
}
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
            showError('Failed to authenticate GOG: ' + (resData.Message || 'Invalid code'));
            await updateGogApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while authenticating GOG credentials.");
        await updateGogApiStatusLabel();
    }
}
async function refreshAmazonLoginUrl() {
    try {
        const res = await fetch('/api/settings/amazon/login-url', { cache: 'no-store' });
        if (res.ok) {
            const data = await res.json();
            const openAmazonLoginBtn = document.getElementById('open-amazon-login-btn');
            if (openAmazonLoginBtn) {
                openAmazonLoginBtn.href = data.LoginUrl || data.loginUrl || '#';
            }
        }
    } catch (err) {
        console.error("Failed to fetch Amazon login URL:", err);
    }
}
async function updateAmazonApiStatusLabel() {
    const statusLabel = document.getElementById('settings-amazon-api-status');
    if (!statusLabel) return;
    try {
        const response = await fetch('/api/settings/amazon/status', { cache: 'no-store' });
        if (response.ok) {
            const data = await response.json();
            const displayName = data.Username || data.username || data.DisplayName || data.displayName || 'User';
            statusLabel.className = 'api-status-label status-success';
            statusLabel.innerHTML = '🟢 Amazon Games connection active. Logged in as <strong>' + escapeHtml(displayName) + '</strong>.';
        } else {
            const errorData = await response.json().catch(() => ({}));
            const message = errorData.Message || errorData.message || '';
            if (message.includes("not configured")) {
                statusLabel.className = 'api-status-label status-missing';
                statusLabel.innerHTML = '⚪ Configuration required. Click "1. Open Amazon Login" and paste the redirect URL above.';
            } else {
                statusLabel.className = 'api-status-label status-error';
                statusLabel.innerHTML = '🔴 Amazon authentication failed.<br><span class="api-raw-error">' + escapeHtml(message) + '</span>';
            }
        }
    } catch (err) {
        statusLabel.className = 'api-status-label status-error';
        statusLabel.innerHTML = '🔴 Failed to communicate with Amazon validation service.';
    }
}

export async function saveAmazonCredentials() {
    const codeInput = document.getElementById('amazon-auth-code-input');
    const rawCode = codeInput?.value.trim() || '';
    if (!rawCode) {
        showError("Please paste the Amazon redirect URL or authorization code.");
        return;
    }
    try {
        const response = await fetch('/api/settings/amazon/credentials', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Secret: rawCode, UserClientId: "amazon" })
        });
        if (response.ok) {
            showSuccess("Amazon Games account successfully authenticated.");
            if (codeInput) codeInput.value = '';
            await updateAmazonApiStatusLabel();
            if (typeof window.refreshNavbarStatus === 'function') {
                await window.refreshNavbarStatus();
            }
        } else {
            const resData = await response.json().catch(() => ({}));
            showError('Failed to authenticate Amazon: ' + (resData.Message || resData.message || 'Invalid code'));
            await updateAmazonApiStatusLabel();
        }
    } catch (err) {
        showError("Network error while authenticating Amazon credentials.");
        await updateAmazonApiStatusLabel();
    }
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// Click Event Delegator for External Services Settings
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
    } else if (e.target.id === 'save-amazon-credentials-btn') {
        saveAmazonCredentials();
    } else if (e.target.id === 'help-modal-close-btn' || e.target.id === 'help-modal-close-icon') {
        closeHelpModal();
    } else if (e.target.id === 'settings-help-modal') {
        closeHelpModal();
    }
});
// Close help modal on Escape key press
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeHelpModal();
    }
});