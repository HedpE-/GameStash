/**
 * Navbar Controller (Sidebar & Accordion Manager)
 */

import { navigateTo } from './router.js';

export async function initNavbarController(workspaceElement) {
    await checkRootFoldersAndToggleVisibility();
    setupNavbarListeners();
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
        const navItem = e.target.closest('.nav-item, .nav-group-toggle-btn');
        if (!navItem) return;

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

window.refreshNavbarFoldersVisibility = checkRootFoldersAndToggleVisibility;