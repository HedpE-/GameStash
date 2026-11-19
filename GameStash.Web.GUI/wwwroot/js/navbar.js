/**
 * Navbar Controller (Sidebar & Accordion Manager)
 */

import { navigateTo } from './router.js';

export async function initNavbarController(workspaceElement) {
    await refreshNavbarState();
    setupNavbarListeners();
}

/**
 * Validates integrations status and applies dynamic access states.
 */
async function refreshNavbarState() {
    const [hasIgdb, hasSteam, hasEpic, hasGog] = await Promise.all([
        checkIgdbCredentialsStatus(),
        checkSteamCredentialsStatus(),
        checkEpicCredentialsStatus(),
        checkGogCredentialsStatus()
    ]);

    // Update Local Stash Root Folders visibility
    if (hasIgdb) {
        await checkRootFoldersAndToggleVisibility();
    }

    // Apply granular locked/unlocked states per stash type
    applyGranularNavLockouts({ hasIgdb, hasSteam, hasEpic, hasGog });
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
function applyGranularNavLockouts({ hasIgdb, hasSteam, hasEpic, hasGog }) {
    // 1. IGDB Requirement (Browse)
    const browseItem = document.getElementById('nav-browse');
    if (browseItem) {
        browseItem.classList.toggle('nav-disabled', !hasIgdb);
        browseItem.setAttribute('title', !hasIgdb ? 'Requires active and validated IGDB API credentials' : '');
    }

    // 2. Steam Requirement
    const steamItem = document.getElementById('nav-steamstash');
    if (steamItem) {
        steamItem.classList.toggle('nav-disabled', !hasSteam);
        steamItem.setAttribute('title', !hasSteam ? 'Requires active and validated Steam API credentials' : '');
    }

    // 3. Epic Requirement
    const epicItem = document.getElementById('nav-epicstash');
    if (epicItem) {
        epicItem.classList.toggle('nav-disabled', !hasEpic);
        epicItem.setAttribute('title', !hasEpic ? 'Requires active and validated Epic Games credentials' : '');
    }

    // 4. GOG Requirement
    const gogItem = document.getElementById('nav-gogstash');
    if (gogItem) {
        gogItem.classList.toggle('nav-disabled', !hasGog);
        gogItem.setAttribute('title', !hasGog ? 'Requires active and validated GOG tokens' : '');
    }

    // 5. Other future stashes stay locked until implemented
    const amazonItem = document.getElementById('nav-amazonstash');
    if (amazonItem) {
        amazonItem.classList.add('nav-disabled');
        amazonItem.setAttribute('title', 'Amazon Games integration coming soon');
    }

    // Note: 'nav-totalstash', 'nav-home', 'nav-settings', 'nav-system' remain enabled.
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

        // If the item is marked as disabled, block navigation and notify the user
        if (navItem.classList.contains('nav-disabled')) {
            e.preventDefault();
            e.stopPropagation();

            const message = navItem.getAttribute('title') || 'This section requires valid credentials configuration first.';
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

// Global hook for settings page to refresh navbar states after credentials change
window.refreshNavbarStatus = refreshNavbarState;
window.refreshNavbarFoldersVisibility = checkRootFoldersAndToggleVisibility;