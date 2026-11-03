import { PaginationController } from './pagination.js';
import { showSuccess, showError, showWarning } from './snackbar.js';

let foundFolders = [];
let pageSize = 15;
let globalImportPollingTimer = null;
let resizeDebounceTimer = null;

// Instantiate the reusable pagination controller
const pagination = new PaginationController({
    paginationRadius: 2,
    jumpSteps: [5, 10],
    onPageChange: () => renderTable()
});

export async function initImportFoldersView() {
    foundFolders = [];
    updateDynamicPageSize();
    setupEventListeners();
    setupWindowResizeListener();
    await checkGlobalScanningState();
}

/**
 * Calculates optimal rows per page based on current window height.
 */
function updateDynamicPageSize() {
    const contentArea = document.querySelector('.content-area');
    let availableHeight = window.innerHeight;
    if (contentArea) {
        const style = window.getComputedStyle(contentArea);
        const padTop = parseFloat(style.paddingTop) || 0;
        const padBottom = parseFloat(style.paddingBottom) || 0;
        availableHeight = contentArea.clientHeight - (padTop + padBottom);
    }

    const headerRow = document.querySelector('.import-header-row');
    const toolbar = document.querySelector('.import-toolbar');
    const checkboxBar = document.querySelector('.selection-checkboxes-bar');
    const tableHeader = document.querySelector('.import-table thead');
    const footerBar = document.querySelector('.import-footer-bar');

    let fixedControlsHeight = 0;
    if (headerRow) fixedControlsHeight += headerRow.offsetHeight + 10;
    if (toolbar) fixedControlsHeight += toolbar.offsetHeight + 10;
    if (checkboxBar) fixedControlsHeight += checkboxBar.offsetHeight + 10;
    if (tableHeader) fixedControlsHeight += tableHeader.offsetHeight;
    if (footerBar) fixedControlsHeight += footerBar.offsetHeight + 20;

    fixedControlsHeight += 15;

    const remainingHeightForRows = availableHeight - fixedControlsHeight;
    const exactRowHeight = 62;
    const calculatedSize = Math.floor(remainingHeightForRows / exactRowHeight);
    const newPageSize = Math.max(2, calculatedSize);

    if (newPageSize !== pageSize) {
        pageSize = newPageSize;
        return true;
    }
    return false;
}

function setupWindowResizeListener() {
    window.removeEventListener('resize', onWindowResize);
    window.addEventListener('resize', onWindowResize);
}

function onWindowResize() {
    clearTimeout(resizeDebounceTimer);
    resizeDebounceTimer = setTimeout(() => {
        if (document.querySelector('.import-container') && foundFolders.length > 0) {
            const hasChanged = updateDynamicPageSize();
            if (hasChanged) {
                renderTable();
            }
        }
    }, 120);
}

/**
 * Checks if any root folder is currently being scanned in the background.
 */
async function checkGlobalScanningState() {
    try {
        updateMatchSelectedButtonState();

        const tableWrapper = document.querySelector('.import-table-wrapper');
        const footerBar = document.querySelector('.import-footer-bar');
        const checkboxBar = document.querySelector('.selection-checkboxes-bar');
        const container = document.querySelector('.import-container');

        const oldLoader = document.querySelector('.view-loading-indicator');
        if (oldLoader) oldLoader.remove();

        if (foundFolders.length === 0 && !globalImportPollingTimer) {
            if (tableWrapper) tableWrapper.style.display = 'none';
            if (footerBar) footerBar.style.display = 'none';
            if (checkboxBar) checkboxBar.style.display = 'none';

            const initialLoader = document.createElement('div');
            initialLoader.className = 'view-loading-indicator';
            initialLoader.innerHTML = `
                <div class="spinner-large"></div>
                <p class="loading-text">Initializing game catalog and checking system state...</p>
            `;
            if (tableWrapper && container) {
                container.insertBefore(initialLoader, tableWrapper);
            }
        }

        const response = await fetch('/api/folders');
        if (!response.ok) return;
        const folders = await response.json();

        const isAnyScanning = folders.some(f => f.IsImportingFolders === true || f.isImportingFolders === true);

        const tempLoader = document.querySelector('.view-loading-indicator');
        if (tempLoader) tempLoader.remove();

        if (isAnyScanning) {
            if (tableWrapper) tableWrapper.style.display = 'none';
            if (footerBar) footerBar.style.display = 'none';
            if (checkboxBar) checkboxBar.style.display = 'none';

            const scanLoader = document.createElement('div');
            scanLoader.className = 'view-loading-indicator';
            scanLoader.innerHTML = `
                <div class="spinner-large"></div>
                <p class="loading-text">A root folder is currently scanning in the background. Please wait...</p>
            `;
            if (tableWrapper && container) {
                container.insertBefore(scanLoader, tableWrapper);
            }

            if (!globalImportPollingTimer) {
                globalImportPollingTimer = setInterval(checkGlobalScanningState, 2000);
            }
        } else {
            if (globalImportPollingTimer) {
                clearInterval(globalImportPollingTimer);
                globalImportPollingTimer = null;
            }

            if (tableWrapper) tableWrapper.style.display = 'block';
            if (footerBar) footerBar.style.display = 'flex';
            if (checkboxBar) checkboxBar.style.display = 'flex';

            await loadFoundFolders();
        }
    } catch (err) {
        console.error("Error evaluating background engine states:", err);
    }
}

async function loadFoundFolders() {
    try {
        const response = await fetch('/api/folders/found');
        if (!response.ok) throw new Error(`HTTP Error ${response.status}`);

        foundFolders = await response.json();
        updateDynamicPageSize();
        renderTable();
    } catch (err) {
        console.error("Error loading import folders:", err);
    }
}

/**
 * RESTAURADA: Controls whether the global import button is enabled
 */
function updateImportButtonState() {
    const executeBtn = document.getElementById('import-execute-btn');
    if (!executeBtn) return;

    const selectedWithMatch = foundFolders.filter(f => f.IsSelected && f.SelectedMatchId != null);
    executeBtn.disabled = selectedWithMatch.length === 0;
}

function updateMatchSelectedButtonState() {
    const matchSelectedBtn = document.getElementById('import-match-selected-btn');
    if (!matchSelectedBtn) return;

    const anySelected = foundFolders.length > 0 && foundFolders.some(f => f.IsSelected === true);
    matchSelectedBtn.disabled = !anySelected;
}

function setupEventListeners() {
    document.getElementById('import-refresh-btn')?.addEventListener('click', async () => {
        await fetch('/api/folders/rescan', { method: 'PUT' });
        await checkGlobalScanningState();
    });

    // IGDB Match Selected Button (Iterative loop respecting single-match selection rule)
    document.getElementById('import-match-selected-btn')?.addEventListener('click', async () => {
        const selected = foundFolders.filter(f => f.IsSelected);
        if (selected.length === 0) {
            showWarning('Please select at least one folder to match.');
            return;
        }

        const matchBtn = document.getElementById('import-match-selected-btn');
        if (matchBtn) matchBtn.disabled = true;

        selected.forEach(f => {
            f.IsLoadingIgdbMatches = true;
        });
        renderTable();

        for (const folder of selected) {
            try {
                const queryText = folder.MatchName || folder.Name || '';
                const res = await fetch(`/api/folders/found/match?path=${encodeURIComponent(folder.Folder)}&searchText=${encodeURIComponent(queryText)}`);

                if (res.ok) {
                    const updated = await res.json();
                    Object.assign(folder, updated);

                    const matches = folder.IgdbMatches || folder.igdbMatches || [];

                    // Single match -> auto-select; multiple -> wait for manual choice
                    if (matches.length === 1) {
                        folder.SelectedMatchId = matches[0].Id ?? matches[0].id;
                        folder.IsSelected = true;
                    } else {
                        folder.IsSelected = false;
                        if (matches.length > 1) {
                            folder.SelectedMatchId = matches[0].Id ?? matches[0].id;
                        } else {
                            folder.SelectedMatchId = null;
                        }
                    }
                } else {
                    console.warn(`Search failed for ${folder.Folder}: HTTP ${res.status}`);
                    folder.IsSelected = false;
                }
            } catch (err) {
                console.error(`Error matching folder ${folder.Folder}:`, err);
                folder.IsSelected = false;
            } finally {
                folder.IsLoadingIgdbMatches = false;
                renderTable();
            }
        }

        updateImportButtonState();
        updateMatchSelectedButtonState();
    });

    document.getElementById('import-check-all')?.addEventListener('change', (e) => {
        const isChecked = e.target.checked;
        foundFolders.forEach(f => f.IsSelected = isChecked);
        renderTable();
        updateMatchSelectedButtonState();
    });

    document.getElementById('import-check-all-matched')?.addEventListener('change', (e) => {
        const isChecked = e.target.checked;
        foundFolders.forEach(f => {
            f.IsSelected = isChecked && f.SelectedMatchId != null;
        });
        renderTable();
        updateMatchSelectedButtonState();
    });

    document.getElementById('import-check-page')?.addEventListener('change', (e) => {
        const isChecked = e.target.checked;
        const { startIndex, endIndex } = pagination.getSliceIndices();
        const pagedFolders = foundFolders.slice(startIndex, endIndex);

        pagedFolders.forEach(f => f.IsSelected = isChecked);
        renderTable();
        updateMatchSelectedButtonState();
    });

    // Execute Import Button (Sequential loop with Snackbar feedback)
    document.getElementById('import-execute-btn')?.addEventListener('click', async () => {
        const selected = foundFolders.filter(f => f.IsSelected && f.SelectedMatchId != null);
        if (selected.length === 0) {
            showWarning("No matched games selected for import.");
            return;
        }

        const executeBtn = document.getElementById('import-execute-btn');
        if (executeBtn) {
            executeBtn.disabled = true;
            executeBtn.innerText = 'Importing...';
        }

        let successCount = 0;
        let failCount = 0;

        for (const item of selected) {
            try {
                const url = `/api/stash/local?folderPath=${encodeURIComponent(item.Folder)}`;
                const res = await fetch(url, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(item.Folder)
                });

                if (res.ok) {
                    successCount++;
                    foundFolders = foundFolders.filter(f => f.Folder !== item.Folder);
                    renderTable();
                } else {
                    failCount++;
                    console.error(`Failed to import: ${item.Folder} (Status ${res.status})`);
                }
            } catch (err) {
                console.error(`Error importing folder: ${item.Folder}`, err);
                failCount++;
            }
        }

        if (executeBtn) {
            executeBtn.innerText = '✓ Import Selected';
        }

        if (successCount > 0 && failCount === 0) {
            showSuccess(`Successfully imported ${successCount} game(s) into your library!`);
        } else if (successCount > 0 && failCount > 0) {
            showWarning(`Imported ${successCount} game(s), but ${failCount} failed.`);
        } else {
            showError("Failed to import selected games.");
        }

        updateImportButtonState();
        updateMatchSelectedButtonState();
        await checkGlobalScanningState();
    });

    document.getElementById('import-help-btn')?.addEventListener('click', () => {
        document.getElementById('import-help-modal')?.classList.add('active');
    });

    document.getElementById('import-help-close-btn')?.addEventListener('click', () => {
        document.getElementById('import-help-modal')?.classList.remove('active');
    });

    // Close any open custom match dropdown when clicking outside
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.match-dropdown')) {
            document.querySelectorAll('.match-dropdown.open').forEach(d => {
                d.classList.remove('open');
                d.classList.remove('drop-up');
            });
        }
    });
}

function renderTable() {
    const tbody = document.getElementById('import-table-body');
    const badge = document.getElementById('import-count-badge');
    if (!tbody) return;

    if (badge) badge.innerText = foundFolders.length;
    tbody.innerHTML = '';

    updateMatchSelectedButtonState();

    // Sincroniza e renderiza a paginação delegada
    pagination.setPagination(foundFolders.length, pageSize);
    pagination.renderControls();

    const { startIndex, endIndex } = pagination.getSliceIndices();
    const pagedFolders = foundFolders.slice(startIndex, endIndex);

    if (pagedFolders.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding: 20px; color: var(--text-muted); font-style:italic;">No unimported folders found. Try refreshing.</td></tr>';
        updateImportButtonState();
        return;
    }

    pagedFolders.forEach(folder => {
        const row = document.createElement('tr');

        if (folder.IsSelected) {
            row.classList.add('selected-row');
        }

        // 1. Checkbox Column
        const checkTd = document.createElement('td');
        checkTd.style.textAlign = 'center';
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.checked = folder.IsSelected || false;
        checkbox.addEventListener('change', (e) => {
            folder.IsSelected = e.target.checked;
            row.classList.toggle('selected-row', folder.IsSelected);
            updateImportButtonState();
            updateMatchSelectedButtonState();
        });
        checkTd.appendChild(checkbox);
        row.appendChild(checkTd);

        // 2. Match Name Column
        const nameTd = document.createElement('td');
        nameTd.innerHTML = `
            <div class="copyable-cell">
                <span title="${folder.MatchName}">${escapeHtml(folder.MatchName || '')}</span>
                <button type="button" class="copy-btn" title="Copy Match Name">📋</button>
            </div>
        `;
        nameTd.querySelector('.copy-btn').addEventListener('click', () => {
            navigator.clipboard.writeText(folder.MatchName || '');
        });
        row.appendChild(nameTd);

        // 3. Type Column
        const typeTd = document.createElement('td');
        typeTd.innerText = folder.Type || 'Unknown';
        row.appendChild(typeTd);

        // 4. Folder Path Column
        const folderTd = document.createElement('td');
        folderTd.innerHTML = `
            <div class="copyable-cell">
                <span title="${folder.Folder}" style="word-break: break-all;">${escapeHtml(folder.Folder || '')}</span>
                <button type="button" class="copy-btn" title="Copy Folder Path">📋</button>
            </div>
        `;
        folderTd.querySelector('.copy-btn').addEventListener('click', () => {
            navigator.clipboard.writeText(folder.RelativeFolderPath || '');
        });
        row.appendChild(folderTd);

        // 5. Matched Game Column (Enhanced Combobox)
        const matchTd = document.createElement('td');
        const container = document.createElement('div');
        container.className = 'match-cell-container';

        const matchBtn = document.createElement('button');
        matchBtn.type = 'button';
        matchBtn.className = 'btn-match-single';

        if (folder.IsLoadingIgdbMatches) {
            matchBtn.disabled = true;
            matchBtn.innerHTML = '<span class="cell-spinner"></span> Matching...';
        } else {
            matchBtn.innerText = '🔍 Match';
        }

        // SCENARIO A: Has IGDB matches -> Show Clear button + Dropdown + Match button
        if (folder.IgdbMatches && folder.IgdbMatches.length > 0) {
            const clearBtn = document.createElement('button');
            clearBtn.type = 'button';
            clearBtn.className = 'btn-clear-match';
            clearBtn.title = 'Clear matches and search manually';
            clearBtn.innerText = '✕';

            clearBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                folder.IgdbMatches = [];
                folder.SelectedMatchId = null;
                folder.IsSelected = false;
                renderTable();
            });

            const dropdown = document.createElement('div');
            dropdown.className = 'match-dropdown';

            const selectedMatch = folder.IgdbMatches.find(m => m.Id === folder.SelectedMatchId) || folder.IgdbMatches[0];

            const trigger = document.createElement('button');
            trigger.type = 'button';
            trigger.className = 'match-dropdown-trigger';
            if (folder.IsLoadingIgdbMatches) trigger.disabled = true;

            trigger.innerHTML = renderTriggerContent(selectedMatch);

            const menu = document.createElement('div');
            menu.className = 'match-dropdown-menu';

            folder.IgdbMatches.forEach(match => {
                const item = document.createElement('div');
                item.className = `match-dropdown-item ${match.Id === folder.SelectedMatchId ? 'selected' : ''}`;
                item.title = `${match.Id}. ${match.Name}`;

                const platforms = formatPlatforms(match.Platforms ?? match.platforms);
                const ratingBadge = renderRatingBadge(match.Rating ?? match.rating);
                const dateInfo = getReleaseDateInfo(match.ReleaseDate ?? match.releaseDate ?? 'TBD');

                let metaParts = [];
                if (platforms) metaParts.push(`<span class="match-meta-platforms">${platforms}</span>`);

                item.innerHTML = `
                    <div class="match-line-header">
                        <div class="match-line-title" title="${escapeHtml(match.Name || '')}">
                            <strong>${match.Id}.</strong> ${escapeHtml(match.Name || '')}
                        </div>
                        ${ratingBadge}
                    </div>
                    <div class="match-line-meta">
                        <div class="match-meta-left">
                            ${metaParts.join(' • ')}
                        </div>
                        <span class="match-meta-date ${dateInfo.cssClass}">${dateInfo.text}</span>
                    </div>
                `;

                item.addEventListener('click', async (e) => {
                    e.stopPropagation();
                    dropdown.classList.remove('open');

                    folder.SelectedMatchId = match.Id;
                    folder.IsSelected = true;
                    checkbox.checked = true;
                    row.classList.add('selected-row');

                    trigger.title = `${match.Id}. ${match.Name}`;
                    trigger.innerHTML = renderTriggerContent(match);

                    menu.querySelectorAll('.match-dropdown-item').forEach(el => el.classList.remove('selected'));
                    item.classList.add('selected');

                    updateImportButtonState();
                    updateMatchSelectedButtonState();
                });

                menu.appendChild(item);
            });

            trigger.addEventListener('click', (e) => {
                e.stopPropagation();

                const isAlreadyOpen = dropdown.classList.contains('open');

                document.querySelectorAll('.match-dropdown.open').forEach(d => {
                    d.classList.remove('open');
                    d.classList.remove('drop-up');
                });

                if (!isAlreadyOpen) {
                    const rect = trigger.getBoundingClientRect();
                    const estimatedMenuHeight = 240;
                    const spaceBelow = window.innerHeight - rect.bottom;
                    const spaceAbove = rect.top;

                    if (spaceBelow < estimatedMenuHeight && spaceAbove > spaceBelow) {
                        dropdown.classList.add('drop-up');
                    } else {
                        dropdown.classList.remove('drop-up');
                    }

                    dropdown.classList.add('open');
                }
            });

            dropdown.appendChild(trigger);
            dropdown.appendChild(menu);

            container.insertBefore(dropdown, container.firstChild);
            container.insertBefore(clearBtn, dropdown);

            matchBtn.addEventListener('click', async () => {
                const queryText = folder.MatchName;
                if (!queryText) return;

                folder.IsLoadingIgdbMatches = true;
                renderTable();

                try {
                    const res = await fetch(`/api/folders/found/match?path=${encodeURIComponent(folder.Folder)}&searchText=${encodeURIComponent(queryText)}`);
                    if (res.ok) {
                        const updated = await res.json();
                        Object.assign(folder, updated);
                    } else {
                        alert(`Search failed: HTTP ${res.status}`);
                    }
                } catch (err) {
                    console.error("Backend unreachable:", err);
                    alert("Could not connect to the server.");
                } finally {
                    folder.IsLoadingIgdbMatches = false;
                    renderTable();
                }
            });

            // SCENARIO B: No matches yet -> Show Editable Input Box
        } else if (!folder.IsLoadingIgdbMatches) {
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'match-search-input';
            input.placeholder = 'Type custom title and click Match...';
            input.value = folder.MatchName || '';

            input.addEventListener('input', (e) => {
                folder.MatchName = e.target.value;
            });

            input.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    folder.MatchName = e.target.value;
                    matchBtn.click();
                }
            });
            container.insertBefore(input, container.firstChild);

            matchBtn.addEventListener('click', async () => {
                const queryText = input.value.trim();
                if (!queryText) {
                    showWarning('Please enter a game title to search.');
                    input.focus();
                    return;
                }

                folder.MatchName = queryText;
                folder.IsLoadingIgdbMatches = true;
                renderTable();

                try {
                    const res = await fetch(`/api/folders/found/match?path=${encodeURIComponent(folder.Folder)}&searchText=${encodeURIComponent(queryText)}`);
                    if (res.ok) {
                        const updated = await res.json();
                        Object.assign(folder, updated);
                    } else {
                        showError(`Search failed: HTTP ${res.status}`);
                    }
                } catch (err) {
                    console.error("Backend unreachable:", err);
                    showError("Could not connect to the server.");
                } finally {
                    folder.IsLoadingIgdbMatches = false;
                    renderTable();
                }
            });

            // SCENARIO C: Loading search state
        } else {
            const inputPlaceholder = document.createElement('div');
            inputPlaceholder.className = 'match-input-placeholder-loading';
            inputPlaceholder.innerText = 'Searching IGDB database...';
            container.insertBefore(inputPlaceholder, container.firstChild);
        }

        container.appendChild(matchBtn);
        matchTd.appendChild(container);
        row.appendChild(matchTd);
        tbody.appendChild(row);
    });

    updateImportButtonState();
    updateMatchSelectedButtonState();

    const pageCheckbox = document.getElementById('import-check-page');
    if (pageCheckbox) {
        const hasElements = pagedFolders.length > 0;
        pageCheckbox.checked = hasElements && pagedFolders.every(f => f.IsSelected === true);
    }
}

function getReleaseDateInfo(rawDate) {
    if (!rawDate || rawDate === 'TBD') {
        return { text: 'TBD', cssClass: 'date-tbd' };
    }

    let releaseYear = null;
    let displayText = '';

    const num = Number(rawDate);
    if (!isNaN(num) && typeof rawDate !== 'boolean') {
        const date = new Date(num > 10000000000 ? num : num * 1000);
        if (!isNaN(date.getTime())) {
            releaseYear = date.getUTCFullYear();
            const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
            const day = String(date.getUTCDate()).padStart(2, '0');
            displayText = `${day} ${months[date.getUTCMonth()]}, ${releaseYear}`;
        }
    } else {
        displayText = String(rawDate);
        const yearMatch = displayText.match(/\b(19\d{2}|20\d{2})\b/);
        if (yearMatch) {
            releaseYear = parseInt(yearMatch[1], 10);
        }
    }

    if (!releaseYear) {
        return { text: displayText || 'TBD', cssClass: 'date-tbd' };
    }

    const currentYear = new Date().getFullYear();
    const isOlderThan10Years = (currentYear - releaseYear) >= 10;

    return {
        text: displayText || 'TBD',
        cssClass: isOlderThan10Years ? 'date-older' : 'date-newer'
    };
}

function renderRatingBadge(rating) {
    if (rating == null || isNaN(rating) || Number(rating) <= 0) return '';
    const formattedVal = Number(rating).toFixed(1);
    return `<span class="match-top-rating"><span class="star-icon">★</span> (${formattedVal}%)</span>`;
}

function renderTriggerContent(match) {
    if (!match) {
        return `
            <div class="match-dropdown-content">
                <span class="match-line-title" style="color: var(--text-muted); font-style: italic;">Select matching game...</span>
            </div>
            <span class="match-arrow">▼</span>
        `;
    }

    const platforms = formatPlatforms(match.Platforms ?? match.platforms);
    const dateInfo = getReleaseDateInfo(match.ReleaseDate ?? match.releaseDate ?? 'TBD');
    const ratingBadge = renderRatingBadge(match.Rating ?? match.rating);

    let metaParts = [];
    if (platforms) metaParts.push(`<span class="match-meta-platforms" title="${escapeHtml(platforms)}">${platforms}</span>`);

    return `
        <div class="match-dropdown-content">
            <div class="match-line-header">
                <div class="match-line-title" title="${escapeHtml(match.Name || '')}">
                    <strong>${match.Id}.</strong> ${escapeHtml(match.Name || '')}
                </div>
                ${ratingBadge}
            </div>
            <div class="match-line-meta">
                <div class="match-meta-left">
                    ${metaParts.join(' • ')}
                </div>
                <span class="match-meta-date ${dateInfo.cssClass}">${dateInfo.text}</span>
            </div>
        </div>
        <span class="match-arrow">▼</span>
    `;
}

function formatPlatforms(platforms) {
    if (!platforms) return '';
    if (Array.isArray(platforms)) {
        return platforms.map(p => p.Abbreviation || p.abbreviation || p.Name || p.name || p).filter(Boolean).join(', ');
    }
    if (typeof platforms === 'object' && platforms.Values) {
        return platforms.Values.map(p => p.Abbreviation || p.Name).filter(Boolean).join(', ');
    }
    return String(platforms);
}

function formatRating(rating) {
    if (rating == null || isNaN(rating) || rating === 0) return '';
    return `★ ${Math.round(rating)}%`;
}

function formatGameType(type) {
    if (!type) return '';
    if (typeof type === 'object') return type.Name || type.name || '';
    return String(type);
}

function formatYear(dateVal) {
    if (!dateVal) return '';
    if (typeof dateVal === 'number') {
        const d = new Date(dateVal * 1000);
        return d.getFullYear() ? `(${d.getFullYear()})` : '';
    }
    const d = new Date(dateVal);
    return isNaN(d.getFullYear()) ? '' : `(${d.getFullYear()})`;
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}