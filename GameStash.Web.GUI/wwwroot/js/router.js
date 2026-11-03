/**
 * SPA Router and View Lifecycle Orchestrator
 */

let isNavigating = false;

import { loadSettings } from './settings.js';
import { initImportFoldersView } from './importfolders.js';
import { loadDashboard } from './dashboard.js';
import { initLocalStashView } from './localstash.js';
import { initBrowseView } from './browse.js';

// Route dictionary mapping templates and view lifecycle initializers
const routes = {
    'home': { templateUrl: '/home.html', init: loadDashboard },
    'browse': { templateUrl: '/browse.html', init: initBrowseView },
    'totalstash': { templateUrl: '/totalstash.html', init: null },
    'localstashhome': { templateUrl: '/localstashhome.html', init: null },
    'localstash': { templateUrl: '/localstash.html', init: initLocalStashView },
    'localstashplaylist': { templateUrl: '/localstashplaylist.html', init: null },
    'importfolders': { templateUrl: '/importfolders.html', init: initImportFoldersView },
    'epicstash': { templateUrl: '/epicstash.html', init: null },
    'steamstash': { templateUrl: '/steamstash.html', init: null },
    'amazonstash': { templateUrl: '/amazonstash.html', init: null },
    'settings': { templateUrl: '/settings.html', init: loadSettings },
    'system': { templateUrl: '/system.html', init: null }
};

const routeAliases = {
    'local-stash': 'localstash',
    'importfromrootfolders': 'importfolders',
    'import-folders': 'importfolders',
    'globalsettings': 'settings'
};

function syncNavbarAccordionState(routeKey) {
    const localLibrarySubViews = ['localstashhome', 'localstash', 'localstashplaylist', 'importfolders'];

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