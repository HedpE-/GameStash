/**
 * Navbar Controller (Sidebar & Accordion Manager)
 */

import { navigateTo } from './router.js';
import { getStoreIconSvg } from './storeicons.js';

export async function initNavbarController(workspaceElement) {
    await renderNavbarTemplate();
    await refreshNavbarState();
    setupNavbarListeners();
}

/**
 * Loads /navbar.html into the existing .sidebar container to avoid duplicating markup in index.html.
 */
async function renderNavbarTemplate() {
    const sidebar = document.querySelector('.sidebar');
    if (!sidebar) return;

    try {
        const response = await fetch('/navbar.html');
        if (response.ok) {
            const html = await response.text();
            sidebar.innerHTML = html.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '');
            sidebar.querySelectorAll('[data-store-icon]').forEach(slot => {
                const svg = getStoreIconSvg(slot.dataset.storeIcon);
                if (svg)
                    slot.innerHTML = svg;
            });
        }
    } catch (error) {
        console.error('Failed to load navbar.html template:', error);
    }
}

/**
 * Validates integrations status and applies dynamic access states.
 */
async function refreshNavbarState() {
    const [hasIgdb, hasSteam, hasEpic, hasGog, hasAmazon] = await Promise.all([
        checkIgdbCredentialsStatus(),
        checkSteamCredentialsStatus(),
        checkEpicCredentialsStatus(),
        checkGogCredentialsStatus(),
        checkAmazonCredentialsStatus()
    ]);

    // Update Local Stash Root Folders visibility
    if (hasIgdb) {
        await checkRootFoldersAndToggleVisibility();
    }

    // Apply master IGDB lockout and granular locked/unlocked states per stash type
    applyGranularNavLockouts({ hasIgdb, hasSteam, hasEpic, hasGog, hasAmazon });
}

async function checkIgdbCredentialsStatus() {
    try {
        const response = await fetch('/api/settings/igdb/status');
        return response.ok;
    } catch (error) {
        console.error('Failed to communicate with IGDB settings status API:', error);
        return false;
    }
}

async function checkSteamCredentialsStatus() {
    try {
        const response = await fetch('/api/settings/steam/status');
        return response.ok;
    } catch (error) {
        console.error('Failed to communicate with Steam settings status API:', error);
        return false;
    }
}

async function checkEpicCredentialsStatus() {
    try {
        const response = await fetch('/api/settings/epic/status');
        return response.ok;
    } catch (error) {
        console.error('Failed to communicate with Epic settings status API:', error);
        return false;
    }
}

async function checkGogCredentialsStatus() {
    try {
        const response = await fetch('/api/settings/gog/status');
        return response.ok;
    } catch (error) {
        console.error('Failed to communicate with GOG settings status API:', error);
        return false;
    }
}

async function checkAmazonCredentialsStatus() {
    try {
        const response = await fetch('/api/settings/amazon/status');
        return response.ok;
    } catch (error) {
        console.error('Failed to communicate with Amazon settings status API:', error);
        return false;
    }
}

async function checkRootFoldersAndToggleVisibility() {
    const groupToggleBtn = document.getElementById('nav-group-local-stash');
    if (!groupToggleBtn) return;

    try {
        const response = await fetch('/api/folders');
        if (!response.ok) return;
        const folders = await response.json();

        if (folders && folders.length > 0) {
            groupToggleBtn.style.display = 'flex';
        } else {
            groupToggleBtn.style.display = 'none';
        }
    } catch (error) {
        console.error('Failed to validate root folders on Navbar:', error);
        groupToggleBtn.style.display = 'none';
    }
}

/**
 * Configures lockout rules per service requirement.
 */
function applyGranularNavLockouts({ hasIgdb, hasSteam, hasEpic, hasGog, hasAmazon }) {
    const igdbMissingMsg = 'Requires active and validated IGDB API credentials';

    // 1. IGDB Requirement (Browse + Master Stashes Container)
    const browseItem = document.getElementById('nav-browse');
    if (browseItem) {
        browseItem.classList.toggle('nav-disabled', !hasIgdb);
        browseItem.setAttribute('title', !hasIgdb ? igdbMissingMsg : '');
    }

    const stashesContainer = document.getElementById('nav-stashes-container');
    if (stashesContainer) {
        stashesContainer.classList.toggle('nav-disabled', !hasIgdb);
        stashesContainer.setAttribute('title', !hasIgdb ? igdbMissingMsg : '');
    }

    // 2. Steam Requirement
    const steamItem = document.getElementById('nav-steamstash');
    if (steamItem) {
        steamItem.classList.toggle('nav-disabled', !hasIgdb || !hasSteam);
        steamItem.setAttribute('title', !hasIgdb ? igdbMissingMsg : (!hasSteam ? 'Requires active and validated Steam API credentials' : ''));
    }

    // 3. Epic Requirement
    const epicItem = document.getElementById('nav-epicstash');
    if (epicItem) {
        epicItem.classList.toggle('nav-disabled', !hasIgdb || !hasEpic);
        epicItem.setAttribute('title', !hasIgdb ? igdbMissingMsg : (!hasEpic ? 'Requires active and validated Epic Games credentials' : ''));
    }

    // 4. GOG Requirement
    const gogItem = document.getElementById('nav-gogstash');
    if (gogItem) {
        gogItem.classList.toggle('nav-disabled', !hasIgdb || !hasGog);
        gogItem.setAttribute('title', !hasIgdb ? igdbMissingMsg : (!hasGog ? 'Requires active and validated GOG tokens' : ''));
    }

    // 5. Amazon Requirement
    const amazonItem = document.getElementById('nav-amazonstash');
    if (amazonItem) {
        amazonItem.classList.toggle('nav-disabled', !hasIgdb || !hasAmazon);
        amazonItem.setAttribute('title', !hasIgdb ? igdbMissingMsg : (!hasAmazon ? 'Requires active and validated Amazon Games credentials' : ''));
    }

    // Note: 'nav-home', 'nav-settings', 'nav-system' remain enabled.
}

function setupNavbarListeners() {
    const sidebar = document.querySelector('.sidebar');
    if (!sidebar) return;

    const groupToggleBtn = document.getElementById('nav-group-local-stash');
    const subGroup = document.getElementById('local-stash-sub-group');

    const toggleLocalStashMenu = (forceState = null) => {
        if (!subGroup || !groupToggleBtn) return;

        const isCollapsed = subGroup.classList.contains('collapsed');
        const shouldExpand = forceState !== null ? forceState : isCollapsed;

        if (shouldExpand) {
            subGroup.classList.remove('collapsed');
            groupToggleBtn.classList.add('expanded');
        } else {
            subGroup.classList.add('collapsed');
            groupToggleBtn.classList.remove('expanded');
        }
    };

    sidebar.addEventListener('click', (e) => {
        const navItem = e.target.closest('.nav-item, .nav-group-toggle-btn, .nav-sub-item');
        if (!navItem) return;

        // Block navigation if either the item itself or its parent container (#nav-stashes-container) is disabled
        const disabledElement = navItem.closest('.nav-disabled');
        if (disabledElement) {
            e.preventDefault();
            e.stopPropagation();

            const message = navItem.getAttribute('title')
                || disabledElement.getAttribute('title')
                || 'This section requires valid credentials configuration first.';

            import('./snackbar.js').then(m => {
                m.showWarning(message);
            });
            return;
        }

        // Accordion behavior for Local Stash parent button
        if (navItem.id === 'nav-group-local-stash') {
            e.preventDefault();
            const isCurrentlyCollapsed = subGroup?.classList.contains('collapsed');

            toggleLocalStashMenu();

            if (isCurrentlyCollapsed) {
                navigateTo('localstash');
            }
            return;
        }

        // Standard Navigation via data-view or id replacement
        const targetView = navItem.getAttribute('data-view') || navItem.id.replace('nav-', '');
        if (targetView && targetView !== 'group-local-stash') {
            navigateTo(targetView);
        }
    });
}

// Global hooks for settings page to refresh navbar states after credentials change
window.refreshNavbarStatus = refreshNavbarState;
window.refreshNavbarFoldersVisibility = checkRootFoldersAndToggleVisibility;
