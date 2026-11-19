import { openGameDetails } from './gamedetails.js';
import { renderStarRating } from './starrating.js';
import { getAlphabetGroup } from './alphabetbar.js';

export function createGameCard(game, currentSortOption, onDeleteCallback, onEditCallback, onFavoriteToggleCallback, stashType = 'local') {
    const card = document.createElement('div');
    card.className = 'game-card-container';
    card.setAttribute('data-id', game.Id);
    card.setAttribute('data-letter', getAlphabetGroup(game.Name || game.name));

    const isDashboard = stashType === 'none' || !stashType;
    const isLocal = stashType === 'local';
    const isSteam = stashType === 'steam';

    game.stashType = stashType;

    const isInPlaylistInitial = !!(game.IsInPlaylist ?? game.isInPlaylist);
    card.setAttribute('data-in-playlist', isInPlaylistInitial.toString());

    const firstChar = (game.SortName || game.Name || '').trim().charAt(0).toUpperCase();
    const title = game.SortName || game.Name || game.name || '';
    card.setAttribute('data-letter', getAlphabetGroup(title));

    // Render subtitle logic
    let subtitleText = '';
    switch (currentSortOption) {
        case 'Release Date': subtitleText = game.ReleaseDateFormatted; break;
        case 'Rating': subtitleText = renderStarRating(game.Rating ?? game.rating); break;
        case 'Date Added': subtitleText = game.DateAddedFormatted; break;
        case 'Folder Size': subtitleText = game.FolderSizeFormatted; break;
        case 'Name':
        default: subtitleText = game.ReleaseYear || ''; break;
    }

    const isMissing = isLocal && !game.IsFolderValid;

    card.innerHTML = `
        <div class="game-card-frame" style="background-image: url('${game.CoverUrl}');">
            ${isMissing ? `<div class="game-card-missing-blur"></div>` : ''}
        
            ${isDashboard ? '' : `
                <div class="game-card-overlay">
                    <button type="button" class="game-card-favorite-btn ${game.IsFavorite ? 'active' : ''}" title="Favorite">
                        <svg class="heart-icon-full" width="18" height="18" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"></path>
                        </svg>
                        <svg class="heart-icon-broken" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M12 5c-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7l1.5-1.5M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.1 0-2 .2-3 1M12 5l-1.5 3.5l2.5 3l-2.5 3.5l1.5 3"></path>
                        </svg>
                    </button>                
                    <div class="game-card-menu-btn-wrapper">
                        <button type="button" class="game-card-menu-btn" title="Options">⋮</button>
                    </div>
                </div>
            `}

            ${isMissing ? `
                <div class="game-card-missing-overlay">
                    <button type="button" class="game-card-missing-delete-btn" title="Remove Missing game folder">
                        <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M3 6h18"></path>
                            <path d="M19 6l-2 14c0 1-1 2-2 2H9c-1 0-2-1-2-2L5 6"></path>
                            <path d="M7 3h10l1 3H6l1-3z"></path>
                            <path d="M14 10l-4 6"></path>
                        </svg>
                    </button>
                </div>
            ` : ''}
        </div>

        ${isDashboard ? '' : `
            <div class="game-card-context-menu">
                    <button type="button" class="menu-action-playlist">${isInPlaylistInitial ? 'Remove from Playlist' : 'Add to Playlist'}</button>
                    <button type="button" class="menu-action-install">${game.IsInstalled ? 'Uninstall' : 'Install'}</button>
                    <button type="button" class="menu-action-refresh">Refresh</button>
                    <button type="button" class="menu-action-edit">Edit</button>
                ${isLocal ? `
                    <button type="button" class="menu-action-delete danger">Delete</button>
                ` : ''}
            </div>
        `}

        <div class="game-card-text">
            <div class="game-card-title" title="${game.Name}">${game.Name}</div>
            <div class="game-card-meta">${subtitleText}</div>
        </div>
    `;

    // Unified click event handler
    card.addEventListener('click', (e) => {
        if (e.target.closest('.game-card-favorite-btn') ||
            e.target.closest('.game-card-menu-btn') ||
            e.target.closest('.game-card-context-menu') ||
            e.target.closest('.game-card-missing-delete-btn')) {
            return;
        }

        const navType = isDashboard ? null : stashType;
        const currentInPlaylist = !!(game.IsInPlaylist ?? game.isInPlaylist);
        openGameDetails(game.Id, navType, { isInPlaylist: currentInPlaylist });
    });

    if (!isDashboard) {
        if (isMissing) {
            card.querySelector('.game-card-missing-delete-btn')?.addEventListener('click', (e) => {
                e.stopPropagation();
                document.querySelectorAll('.game-card-context-menu').forEach(m => m.style.display = 'none');
                if (onDeleteCallback) onDeleteCallback(game);
            });
        }

        const contextMenu = card.querySelector('.game-card-context-menu');

        // Context menu toggle
        card.querySelector('.game-card-menu-btn')?.addEventListener('click', (e) => {
            e.stopPropagation();
            document.querySelectorAll('.game-card-context-menu').forEach(m => {
                if (m !== contextMenu) m.style.display = 'none';
            });
            contextMenu.style.display = contextMenu.style.display === 'flex' ? 'none' : 'flex';
        });

        // Install / Uninstall action (Local and Steam)
        card.querySelector('.menu-action-install')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            contextMenu.style.display = 'none';

            const newInstallState = !game.IsInstalled;
            game.IsInstalled = newInstallState;
            e.target.innerText = newInstallState ? 'Uninstall' : 'Install';

            try {
                const endpoint = isSteam
                    ? `/api/stash/${game.stashType}/${game.ExternalAppId || game.Id}/details`
                    : `/api/stash/${game.stashType}/${game.Id}/details`;

                const bodyData = JSON.stringify({ propertyName: 'IsInstalled', value: newInstallState });

                await fetch(endpoint, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: bodyData
                });
            } catch (err) {
                console.error("Network communication exception during install state commit:", err);
            }
        });

        // Favorite toggle (Supported for Local and Steam)
        card.querySelector('.game-card-favorite-btn')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            const newFav = !game.IsFavorite;
            game.IsFavorite = newFav;
            card.querySelector('.game-card-favorite-btn').classList.toggle('active', newFav);

            const endpoint = isSteam
                ? `/api/stash/${game.stashType}/${game.ExternalAppId || game.Id}/details`
                : `/api/stash/${game.stashType}/${game.Id}/details`;

            await fetch(endpoint, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ propertyName: 'IsFavorite', value: newFav })
            });

            window.dispatchEvent(new CustomEvent('gameFavoriteChanged', {
                detail: { gameId: game.Id, isFavorite: newFav }
            }));
            if (onFavoriteToggleCallback) onFavoriteToggleCallback();
        });
        // Refresh action
        card.querySelector('.menu-action-refresh')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            contextMenu.style.display = 'none';
            const res = await fetch(`/api/stash/${game.stashType}/${game.Id}/refresh`, { method: 'POST' });
            if (res.ok) {
                const updated = await res.json();
                Object.assign(game, updated);
                const meta = card.querySelector('.game-card-meta');
                if (meta && currentSortOption === 'Rating') meta.innerHTML = renderStarRating(game.Rating ?? game.rating);
            }
        });

        // Playlist toggle
        card.querySelector('.menu-action-playlist')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            contextMenu.style.display = 'none';

            const isCurrentlyInPlaylist = !!(game.IsInPlaylist || game.isInPlaylist);
            const newPlaylistState = !isCurrentlyInPlaylist;

            game.IsInPlaylist = newPlaylistState;
            game.isInPlaylist = newPlaylistState;
            card.setAttribute('data-in-playlist', newPlaylistState.toString());
            e.target.innerText = newPlaylistState ? 'Remove from Playlist' : 'Add to Playlist';

            const method = newPlaylistState ? 'PUT' : 'DELETE';
            const url = `/api/stash/${game.stashType}/playlist/${game.Id}`;

            try {
                const res = await fetch(url, { method: method });
                if (res.ok) {
                    window.dispatchEvent(new CustomEvent('gamePlaylistStatusChanged', {
                        detail: { gameId: game.Id, isInPlaylist: newPlaylistState }
                    }));
                    if (typeof onFavoriteToggleCallback === 'function') onFavoriteToggleCallback();
                }
            } catch (err) {
                console.error("Network communication exception during playlist toggle sequence:", err);
            }
        });

        card.querySelector('.menu-action-edit')?.addEventListener('click', (e) => {
            e.stopPropagation();
            contextMenu.style.display = 'none';
            if (onEditCallback) onEditCallback(game);
        });


        if (isLocal) {
            card.querySelector('.menu-action-delete')?.addEventListener('click', (e) => {
                e.stopPropagation();
                contextMenu.style.display = 'none';
                if (onDeleteCallback) onDeleteCallback(game);
            });
        }

        card.addEventListener('mouseleave', () => {
            const contextMenu = card.querySelector('.game-card-context-menu');
            if (contextMenu) contextMenu.style.display = 'none';
        });

        window.addEventListener('gameFavoriteChanged', (e) => {
            const { gameId, isFavorite } = e.detail;
            if (String(game.Id) === String(gameId)) {
                game.IsFavorite = isFavorite;
                const favBtn = card.querySelector('.game-card-favorite-btn');
                if (favBtn) favBtn.classList.toggle('active', isFavorite);
            }
        });

        window.addEventListener('gamePlaylistStatusChanged', (e) => {
            const { gameId, isInPlaylist } = e.detail;
            if (String(gameId) === String(gameId)) {
                game.IsInPlaylist = isInPlaylist;
                game.isInPlaylist = isInPlaylist;
                card.setAttribute('data-in-playlist', isInPlaylist.toString());

                const playlistBtn = card.querySelector('.menu-action-playlist');
                if (playlistBtn) {
                    playlistBtn.innerText = isInPlaylist ? 'Remove from Playlist' : 'Add to Playlist';
                }
                if (typeof onFavoriteToggleCallback === 'function') onFavoriteToggleCallback();
            }
        });
    }

    return card;
}