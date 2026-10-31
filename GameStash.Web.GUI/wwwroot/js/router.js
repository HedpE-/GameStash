let isNavigating = false;

import { loadSettings } from './settings.js';
import { initImportFoldersView } from './importfolders.js';
import { loadDashboard } from './dashboard.js';
import { initLocalLibraryView } from './locallibrary.js';
import { initBrowseView } from './browse.js';

// Route dictionary mapping templates and view lifecycle initializers
const routes = {
    'home': { templateUrl: '/home.html', init: loadDashboard },
    'browse': { templateUrl: '/browse.html', init: initBrowseView },
    'locallibrary': { templateUrl: '/locallibrary.html', init: initLocalLibraryView },
    'importfolders': { templateUrl: '/importfolders.html', init: initImportFoldersView },
    'settings': { templateUrl: '/settings.html', init: loadSettings }
};

// Route aliases to seamlessly handle WPF data-tags and legacy hyphenated keys
const routeAliases = {
    'local-library': 'locallibrary',
    'importfromrootfolders': 'importfolders',
    'import-folders': 'importfolders',
    'globalsettings': 'settings'
};

/**
 * Resolves the route from the dictionary, loads the template, and invokes the view controller.
 * @param {string} rawViewName - The target route name or WPF data-tag.
 */
export async function navigateTo(rawViewName) {
    if (isNavigating) return;
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
    document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
    const activeNav = document.getElementById(`nav-${routeKey}`) ||
        document.querySelector(`[data-tag="${rawViewName}"]`);
    if (activeNav) activeNav.classList.add('active');

    const workspace = document.getElementById('content-workspace');

    try {
        // 1. Fetch HTML template directly from route configuration
        const response = await fetch(routeConfig.templateUrl);
        if (!response.ok) {
            throw new Error(`Failed to load view template: ${routeConfig.templateUrl} (Status: ${response.status})`);
        }

        const htmlContent = await response.text();
        // Inject clean template markup stripping embedded script tags
        workspace.innerHTML = htmlContent.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, "");

        // 2. Invoke the registered view controller dynamically
        if (typeof routeConfig.init === 'function') {
            await routeConfig.init(workspace);
        }

    } catch (err) {
        console.error("Router navigation error:", err);
        workspace.innerHTML = `<p style="color: #ff4a4a; padding: 20px;">Error rendering view: ${rawViewName}</p>`;
    } finally {
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