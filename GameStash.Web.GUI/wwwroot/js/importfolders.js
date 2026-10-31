let foundFolders = [];

export async function initImportFoldersView() {
    setupEventListeners();
    await loadFoundFolders();
}

async function loadFoundFolders() {
    try {
        const response = await fetch('/api/import/folders');
        if (!response.ok) throw new Error(`HTTP Error ${response.status}`);

        foundFolders = await response.json();
        renderTable();
    } catch (err) {
        console.error("Error loading import folders:", err);
    }
}

function setupEventListeners() {
    // Refresh Root Folders Button
    document.getElementById('import-refresh-btn')?.addEventListener('click', async () => {
        await fetch('/api/import/refresh', { method: 'POST' });
        await loadFoundFolders();
    });

    // IGDB Match Selected Button
    document.getElementById('import-match-selected-btn')?.addEventListener('click', async () => {
        const selectedFolders = foundFolders.filter(f => f.IsSelected).map(f => f.Folder);
        if (selectedFolders.length === 0) {
            alert('Please select at least one folder to match.');
            return;
        }

        // Show spinner on selected rows
        foundFolders.forEach(f => {
            if (f.IsSelected) f.IsLoadingIgdbMatches = true;
        });
        renderTable();

        try {
            const res = await fetch('/api/import/match-selected', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(selectedFolders)
            });

            if (res.ok) {
                const updatedFolders = await res.json();
                updatedFolders.forEach(updated => {
                    const local = foundFolders.find(f => f.Folder === updated.Folder);
                    if (local) Object.assign(local, updated, { IsLoadingIgdbMatches: false });
                });
            }
        } finally {
            foundFolders.forEach(f => f.IsLoadingIgdbMatches = false);
            renderTable();
        }
    });

    // Checkboxes
    document.getElementById('import-check-all')?.addEventListener('change', (e) => {
        const isChecked = e.target.checked;
        foundFolders.forEach(f => f.IsSelected = isChecked);
        renderTable();
    });

    document.getElementById('import-check-all-matched')?.addEventListener('change', (e) => {
        const isChecked = e.target.checked;
        foundFolders.forEach(f => {
            f.IsSelected = isChecked && f.SelectedMatchId != null;
        });
        renderTable();
    });

    // Import Selected Execute Button
    document.getElementById('import-execute-btn')?.addEventListener('click', async () => {
        const selectedWithMatch = foundFolders.filter(f => f.IsSelected && f.SelectedMatchId != null).map(f => f.Folder);

        if (selectedWithMatch.length === 0) {
            alert('No matched folders selected for import.');
            return;
        }

        const res = await fetch('/api/import/import-selected', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(selectedWithMatch)
        });

        if (res.ok) {
            const data = await res.json();
            alert(`Successfully imported ${data.ImportedCount} game(s) into your Local Library!`);
            await loadFoundFolders();
        }
    });

    // Help Dialog
    document.getElementById('import-help-btn')?.addEventListener('click', () => {
        document.getElementById('import-help-modal')?.classList.add('active');
    });

    document.getElementById('import-help-close-btn')?.addEventListener('click', () => {
        document.getElementById('import-help-modal')?.classList.remove('active');
    });
}

function renderTable() {
    const tbody = document.getElementById('import-table-body');
    const badge = document.getElementById('import-count-badge');
    if (!tbody) return;

    if (badge) badge.innerText = foundFolders.length;
    tbody.innerHTML = '';

    foundFolders.forEach(folder => {
        const row = document.createElement('tr');

        // Checkbox
        const checkTd = document.createElement('td');
        checkTd.style.textAlign = 'center';
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.checked = folder.IsSelected;
        checkbox.addEventListener('change', (e) => {
            folder.IsSelected = e.target.checked;
        });
        checkTd.appendChild(checkbox);
        row.appendChild(checkTd);

        // Match Name
        const nameTd = document.createElement('td');
        nameTd.innerHTML = `
            <div class="copyable-cell">
                <span title="${folder.MatchName}">${folder.MatchName}</span>
                <button type="button" class="copy-btn" title="Copy Match Name">📋</button>
            </div>
        `;
        nameTd.querySelector('.copy-btn').addEventListener('click', () => {
            navigator.clipboard.writeText(folder.MatchName);
        });
        row.appendChild(nameTd);

        // Type
        const typeTd = document.createElement('td');
        typeTd.innerText = folder.Type;
        row.appendChild(typeTd);

        // Folder Path
        const folderTd = document.createElement('td');
        folderTd.innerHTML = `
            <div class="copyable-cell">
                <span title="${folder.Folder}" style="word-break: break-all;">${folder.Folder}</span>
                <button type="button" class="copy-btn" title="Copy Folder Path">📋</button>
            </div>
        `;
        folderTd.querySelector('.copy-btn').addEventListener('click', () => {
            navigator.clipboard.writeText(folder.Folder);
        });
        row.appendChild(folderTd);

        // Matched Game (Combobox / Inline Search)
        const matchTd = document.createElement('td');
        if (folder.IsLoadingIgdbMatches) {
            matchTd.innerHTML = `<div class="cell-spinner"></div>`;
        } else {
            const container = document.createElement('div');
            container.className = 'match-cell-container';

            if (folder.IgdbMatches && folder.IgdbMatches.length > 0) {
                // Select Dropdown
                const select = document.createElement('select');
                select.className = 'match-select-dropdown';

                folder.IgdbMatches.forEach(match => {
                    const opt = document.createElement('option');
                    opt.value = match.Id;
                    opt.innerText = `${match.Id}. ${match.Name} (${match.Year}) ${match.Platforms ? '[' + match.Platforms + ']' : ''}`;
                    if (folder.SelectedMatchId === match.Id) opt.selected = true;
                    select.appendChild(opt);
                });

                select.addEventListener('change', async (e) => {
                    const matchId = parseInt(e.target.value, 10);
                    folder.SelectedMatchId = matchId;
                    await fetch(`/api/import/select-match?folderPath=${encodeURIComponent(folder.Folder)}&matchId=${matchId}`, { method: 'POST' });
                });

                container.appendChild(select);
            } else {
                // Input for manual search when Enter is pressed
                const input = document.createElement('input');
                input.type = 'text';
                input.className = 'match-search-input';
                input.placeholder = 'Press Enter to search IGDB...';
                input.value = folder.MatchName;

                input.addEventListener('keydown', async (e) => {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        const searchText = input.value.trim();
                        if (!searchText) return;

                        folder.IsLoadingIgdbMatches = true;
                        renderTable();

                        const res = await fetch(`/api/import/search-single?folderPath=${encodeURIComponent(folder.Folder)}&searchText=${encodeURIComponent(searchText)}`, { method: 'POST' });
                        if (res.ok) {
                            const updated = await res.json();
                            Object.assign(folder, updated, { IsLoadingIgdbMatches: false });
                        } else {
                            folder.IsLoadingIgdbMatches = false;
                        }
                        renderTable();
                    }
                });

                container.appendChild(input);
            }
            matchTd.appendChild(container);
        }
        row.appendChild(matchTd);

        tbody.appendChild(row);
    });
}