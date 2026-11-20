/**
 * SPA Router and View Lifecycle Orchestrator
 */

let isNavigating = false;

import { initExternalServicesSettingsView } from './externalservicessettings.js';
import { initRootFoldersSettingsView } from './rootfolderssettings.js';
import { loadDashboard } from './dashboard.js';
import { initBrowseView } from './browse.js';
import { initImportFoldersView } from './importfolders.js';
import { initPlaylistView } from './playlist.js';
import { initStashView } from './stash.js';

const routes = {
    'home': { templateUrl: '/home.html', init: loadDashboard },
    'browse': { templateUrl: '/browse.html', init: initBrowseView },
    'unifiedstash': { templateUrl: '/stash.html', init: (ws) => initStashView(ws, 'unified') },
    'localstash': { templateUrl: '/stash.html', init: (ws) => initStashView(ws, 'local') },
    'importfolders': { templateUrl: '/importfolders.html', init: initImportFoldersView },
    'steamstash': { templateUrl: '/stash.html', init: (ws) => initStashView(ws, 'steam') },
    'epicstash': { templateUrl: '/stash.html', init: (ws) => initStashView(ws, 'epic') },
    'gogstash': { templateUrl: '/stash.html', init: (ws) => initStashView(ws, 'gog') },
    'amazonstash': { templateUrl: '/stash.html', init: (ws) => initStashView(ws, 'amazon') },
    'playlist': { templateUrl: '/playlist.html', init: initPlaylistView },
    'externalservicessettings': { templateUrl: '/externalservicessettings.html', init: initExternalServicesSettingsView },
    'rootfolderssettings': { templateUrl: '/rootfolderssettings.html', init: initRootFoldersSettingsView }
};

const routeAliases = {
//     'local-stash': 'localstash',
//     'importfromrootfolders': 'importfolders',
//     'import-folders': 'importfolders',
//     'globalsettings': 'settings'
};

function syncNavbarAccordionState(routeKey) {
    const localLibrarySubViews = ['localstash', 'importfolders'];

    if (localLibrarySubViews.includes(routeKey)) {
        const groupToggleBtn = document.getElementById('nav-group-local-stash');
        const subGroup = document.getElementById('local-stash-sub-group');

        if (groupToggleBtn && subGroup) {
            subGroup.classList.remove('collapsed');
            groupToggleBtn.classList.add('expanded');
        }
    }
}

/**
 * Resolves the route from the dictionary, loads the template, and invokes the view controller.
 * @param {string} rawViewName - The target route name or data-tag.
 */
export async function navigateTo(rawViewName) {
    if (!rawViewName) return;

    // Safety fallback: if navigation gets stuck for more than 4 seconds, force unlock
    if (isNavigating) {
        console.warn("Navigation locked, forcing unlock override...");
        isNavigating = false;
    }

    isNavigating = true;

    // Normalize raw token: lowercase, strip hyphens/underscores
    const normalizedKey = rawViewName.toLowerCase().replace(/[-_]/g, '');
    const routeKey = routeAliases[normalizedKey] || routeAliases[rawViewName.toLowerCase()] || normalizedKey;

    const routeConfig = routes[routeKey];
    if (!routeConfig) {
        console.warn(`Unregistered navigation route requested: ${rawViewName} (resolved to: ${routeKey})`);
        isNavigating = false;
        return;
    }

    window.location.hash = routeKey;

    // Synchronize active state on sidebar navigation links
    document.querySelectorAll('.sidebar .nav-item').forEach(item => item.classList.remove('active'));

    const activeNav = document.getElementById(`nav-${routeKey}`) || document.querySelector(`[data-view="${routeKey}"]`);
    if (activeNav) activeNav.classList.add('active');

    // Force expand accordion if target is a local stash subsystem
    syncNavbarAccordionState(routeKey);

    const workspace = document.getElementById('content-workspace');
    if (!workspace) {
        console.error("Critical: #content-workspace target element not found in DOM.");
        isNavigating = false;
        return;
    }

    try {
        const response = await fetch(routeConfig.templateUrl);
        if (!response.ok) {
            throw new Error(`Failed to load view template: ${routeConfig.templateUrl} (Status: ${response.status})`);
        }

        const htmlContent = await response.text();
        // Inject clean template markup stripping embedded script tags
        workspace.innerHTML = htmlContent.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, "");

        // Invoke the registered view controller dynamically
        if (typeof routeConfig.init === 'function') {
            await routeConfig.init(workspace);
        }

    } catch (err) {
        console.error("Router navigation error:", err);
        workspace.innerHTML = `<p style="color: #ff4a4a; padding: 20px;">Error rendering view: ${rawViewName} (${err.message})</p>`;
    } finally {
        // ALWAYS guarantees navigation lock releases
        isNavigating = false;
    }
}

/**
 * Evaluates current browser window hash and routes accordingly.
 */
export function handleRouting() {
    const currentHash = window.location.hash.replace('#', '').trim() || 'home';
    navigateTo(currentHash);
}