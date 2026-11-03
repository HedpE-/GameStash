/**
 * GameStash Web UI - Core Orchestrator
 * Fully typed wrapper for interacting with the ASP.NET Core Web API
 */

import { initNavbarController } from '/js/navbar.js';
import { handleRouting, navigateTo } from '/js/router.js';

// Expose routing interfaces globally
window.navigateToView = navigateTo;

// Lifecycle orchestrator attachment mapping for native browser histories
window.addEventListener('hashchange', handleRouting);
window.addEventListener('load', handleRouting);

// App bootstrap execution pipeline
document.addEventListener('DOMContentLoaded', () => {
    const workspace = document.getElementById('content-workspace');

    // 1. Initialize the modular navbar controller passing the active view grid
    initNavbarController(workspace);

    // 2. Immediate initial hash route parsing trigger to clean loading components placeholders
    handleRouting();
});
