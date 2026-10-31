export async function loadSettings() {
    try {
        const response = await fetch('/api/settings');
        if (!response.ok) return;
        const data = await response.json();

        document.getElementById('client-id-input').value = data.IgdbClientId || '';
        document.getElementById('secret-input').value = data.IgdbSecret || '';

        const foldersContainer = document.getElementById('folders-list');
        foldersContainer.innerHTML = '';

        if (data.RootFolders && data.RootFolders.length > 0) {
            data.RootFolders.forEach(folderObj => {
                foldersContainer.innerHTML += `<span class="folder-tag">${folderObj.Path}</span>`;
            });
        } else {
            foldersContainer.innerHTML = '<p style="color: #666; font-size: 13px; font-style: italic;">No monitored directory layers configured inside SQLite.</p>';
        }
    } catch (err) {
        console.error("Failed to map application configurations:", err);
    }
}

export async function saveCredentials() {
    const clientId = document.getElementById('client-id-input').value;
    const secret = document.getElementById('secret-input').value;

    try {
        const response = await fetch('/api/settings/credentials', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IgdbClientId: clientId, IgdbSecret: secret })
        });

        if (response.ok) {
            alert("IGDB settings successfully synchronized and committed to local database context.");
            loadSettings();
        } else {
            alert("Failed to commit settings rows to the backend service engine runtime.");
        }
    } catch (err) {
        alert("An error occurred while connecting to the database manager infrastructure.");
    }
}

document.addEventListener('click', (e) => {
    if (e.target && e.target.id === 'save-credentials-btn') {
        saveCredentials();
    }
});
