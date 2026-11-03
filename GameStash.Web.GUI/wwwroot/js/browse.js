import { openGameDetails } from './gamedetails.js';

let currentSearchResults = [];
let currentSearchQuery = '';
let selectedSortOption = 'Name'; // 'Name' | 'ReleaseDate' | 'Rating'
let sortDirection = 'asc'; // 'asc' | 'desc'

/**
 * Initializes the Browse Games view into the workspace.
 * @param {HTMLElement} workspaceElement - The content injection target container.
 */
export async function initBrowseView(workspaceElement) {
    const response = await fetch('/browse.html');
    if (!response.ok) {
        workspaceElement.innerHTML = `<p class="loading-text">Failed to load browse view layout.</p>`;
        return;
    }
    workspaceElement.innerHTML = await response.text();

    setupBrowseEvents();
}

function setupBrowseEvents() {
    const input = document.getElementById('browse-search-input');
    const searchBtn = document.getElementById('browse-search-btn');
    const clearInputBtn = document.getElementById('browse-search-clear-input-btn');
    const clearResultsBtn = document.getElementById('browse-clear-results-btn');
    const sortToggle = document.getElementById('browse-sort-toggle');
    const sortMenu = document.getElementById('browse-sort-menu');

    // Enter key triggers search
    input?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.keyCode === 13) {
            e.preventDefault();
            executeSearch();
        }
    });

    // Show/hide the inline clear button when typing
    input?.addEventListener('input', () => {
        if (clearInputBtn) {
            clearInputBtn.style.display = input.value.trim() ? 'block' : 'none';
        }
    });

    // Inline input clear button ('✕' inside input box)
    clearInputBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (input) {
            input.value = '';
            clearInputBtn.style.display = 'none';
            input.focus();
        }
    });

    // Search button click
    searchBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        executeSearch();
    });

    // Main clear button: resets input, state, and results list
    clearResultsBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        currentSearchResults = [];
        currentSearchQuery = '';

        if (input) {
            input.value = '';
            input.focus();
        }
        if (clearInputBtn) {
            clearInputBtn.style.display = 'none';
        }

        const toolbar = document.getElementById('browse-meta-toolbar');
        const resultsList = document.getElementById('browse-results-list');
        const loader = document.getElementById('browse-loading-indicator');

        if (toolbar) toolbar.style.display = 'none';
        if (loader) loader.style.display = 'none';
        if (resultsList) {
            resultsList.style.display = 'none';
            resultsList.innerHTML = '';
        }
    });

    // Sort Dropdown toggle
    sortToggle?.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (sortMenu) {
            sortMenu.style.display = sortMenu.style.display === 'block' ? 'none' : 'block';
        }
    });

    window.addEventListener('click', () => {
        if (sortMenu) sortMenu.style.display = 'none';
    });

    // Sort Dropdown selection
    document.querySelectorAll('.dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            e.stopPropagation();
            const option = item.getAttribute('data-sort');
            if (selectedSortOption === option) {
                sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
                selectedSortOption = option;
                sortDirection = 'asc';
            }
            updateSortUI();
            sortAndRenderResults();
            if (sortMenu) sortMenu.style.display = 'none';
        });
    });
}

async function executeSearch() {
    const input = document.getElementById('browse-search-input');
    const query = input?.value.trim();
    const limit = 500;
    if (!query) return;

    const loader = document.getElementById('browse-loading-indicator');
    const toolbar = document.getElementById('browse-meta-toolbar');
    const resultsContainer = document.getElementById('browse-results-list');

    if (loader) loader.style.display = 'flex';
    if (toolbar) toolbar.style.display = 'none';
    if (resultsContainer) {
        resultsContainer.style.display = 'none';
        resultsContainer.innerHTML = '';
    }

    try {
        const response = await fetch(`/api/games/search?query=${encodeURIComponent(query)}&limitResults=${limit}`);
        if (!response.ok) {
            const errData = await response.json().catch(() => null);
            throw new Error(errData?.Details || `HTTP Error ${response.status}`);
        }

        currentSearchResults = await response.json();
        currentSearchQuery = query;

        const queryLabel = document.getElementById('browse-query-label');
        const countLabel = document.getElementById('browse-count-label');

        if (queryLabel) queryLabel.innerText = query;
        if (countLabel) countLabel.innerText = currentSearchResults.length;

        if (toolbar) toolbar.style.display = 'flex';
        sortAndRenderResults();
    } catch (err) {
        console.error("Error executing game catalog search:", err);
        if (resultsContainer) {
            resultsContainer.innerHTML = `<p class="loading-text" style="color: #ff5252;">Search failed: ${err.message}</p>`;
            resultsContainer.style.display = 'block';
        }
    } finally {
        if (loader) loader.style.display = 'none';
    }
}

function updateSortUI() {
    const label = document.getElementById('browse-current-sort-label');
    const arrow = sortDirection === 'asc' ? '↑' : '↓';
    if (label) label.innerText = `Sort: ${selectedSortOption} ${arrow}`;

    document.querySelectorAll('.dropdown-item').forEach(item => {
        const opt = item.getAttribute('data-sort');
        const indicator = item.querySelector('.sort-indicator');
        if (opt === selectedSortOption) {
            item.classList.add('active');
            if (indicator) indicator.innerText = arrow;
        } else {
            item.classList.remove('active');
            if (indicator) indicator.innerText = '';
        }
    });
}

function sortAndRenderResults() {
    const resultsContainer = document.getElementById('browse-results-list');
    if (!resultsContainer) return;

    if (!currentSearchResults || currentSearchResults.length === 0) {
        resultsContainer.innerHTML = `<p class="loading-text">No games found matching your search.</p>`;
        resultsContainer.style.display = 'block';
        return;
    }

    currentSearchResults.sort((a, b) => {
        let valA, valB;
        if (selectedSortOption === 'ReleaseDate') {
            valA = a.RawReleaseDate ? new Date(a.RawReleaseDate).getTime() : 0;
            valB = b.RawReleaseDate ? new Date(b.RawReleaseDate).getTime() : 0;
        } else if (selectedSortOption === 'Rating') {
            valA = a.Rating || 0;
            valB = b.Rating || 0;
        } else {
            valA = (a.Name || '').toLowerCase();
            valB = (b.Name || '').toLowerCase();
        }

        if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
        if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
        return 0;
    });

    renderList();
}

function renderList() {
    const container = document.getElementById('browse-results-list');
    if (!container) return;
    container.innerHTML = '';

    currentSearchResults.forEach(game => {
        const row = document.createElement('div');
        row.className = 'game-row-item';

        const ratingPercentage = Math.max(0, Math.min(100, game.Rating));
        const devLogoHtml = game.DeveloperLogoUrl
            ? `<img src="${game.DeveloperLogoUrl}" alt="Developer Logo" class="game-row-dev-logo" />`
            : '';

        row.innerHTML = `
            <div class="game-row-cover" style="background-image: url('${game.CoverUrl}');"></div>
            <div class="game-row-details">
                <div class="game-row-title">${game.Name}</div>
                <div class="game-row-date">${game.ReleaseDate}</div>
                <div class="game-row-developer">
                    ${devLogoHtml}
                    <span>${game.DeveloperName}</span>
                </div>
                <div class="game-row-platforms">${game.Platforms}</div>
                <div class="rating-bar-container">
                    <div class="stars-empty">★★★★★</div>
                    <div class="stars-fill" style="width: ${ratingPercentage}%;">★★★★★</div>
                </div>
            </div>
            <div class="game-row-chip">${game.Category}</div>
        `;

        // Click navigates straight into the Game Details modal
        row.addEventListener('click', () => openGameDetails(game.Id, 'browse'));
        container.appendChild(row);
    });

    container.style.display = 'flex';
}