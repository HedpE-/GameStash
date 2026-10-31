/**
 * GameStash Web UI - Core Orchestrator
 * Fully typed wrapper for interacting with the ASP.NET Core Web API
 */

// Import core module hooks natively using ES6 bounds tracking
import { handleRouting, navigateTo } from '/js/router.js';
import { initBrowseView } from '/js/browse.js';
import { initLocalLibraryView } from '/js/locallibrary.js';

// Expose routing interfaces globally to link old DOM triggers cleanly if needed
window.navigateToView = navigateTo;

// Lifecycle orchestrator attachment
window.addEventListener('hashchange', handleRouting);
window.addEventListener('load', handleRouting);

// Intercept global navigation items for sidebar events execution
document.addEventListener('DOMContentLoaded', () => {
    // Immediate initial route parsing trigger to clear the "Loading view..." placeholder
    handleRouting();

    document.querySelectorAll('.nav-item').forEach(item => {
        item.addEventListener('click', () => {
            // Extracts the view name based on the DOM element's ID pattern
            const targetView = item.id.replace('nav-', '');
            navigateTo(targetView);
        });
    });
});

const workspace = document.getElementById('content-workspace');

document.querySelectorAll('.sidebar .nav-item').forEach(item => {
    item.addEventListener('click', (e) => {
        const tag = item.getAttribute('data-tag');

        // Update active menu state
        document.querySelectorAll('.sidebar .nav-item').forEach(el => el.classList.remove('active'));
        item.classList.add('active');

        // Route using the exact same switch cases from MainWindow.xaml.cs
        switch (tag) {
            case 'Home':
                // loadHomeView();
                break;
            case 'Browse':
                initBrowseView(workspace);
                break;
            case 'LocalLibrary':
                initLocalLibraryView(workspace);
                break;
            case 'TotalLibrary':
            case 'EpicLibrary':
            case 'SteamLibrary':
            case 'AmazonLibrary':
            case 'GlobalSettings':
            case 'ImportFromRootFolders':
            case 'System':
            default:
                // loadFeatureNotImplementedView();
                break;
        }
    });
});
