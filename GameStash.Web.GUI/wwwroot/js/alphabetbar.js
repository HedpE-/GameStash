/**
 * Shared Alphabet Jump Bar Component for Stash Views
 */

/**
 * Normalizes the first character of a game title into A-Z or '#' for numbers and special characters.
 * @param {string} title - Game title.
 * @returns {string} Single uppercase letter A-Z or '#'.
 */
export function getAlphabetGroup(title) {
    const firstChar = (title || '').trim().charAt(0).toUpperCase();
    // Normalize accented characters (e.g., 'É' -> 'E') before testing A-Z
    const normalized = firstChar.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
    return /^[A-Z]$/.test(normalized) ? normalized : '#';
}

/**
 * Renders the vertical sticky alphabet navigation bar and binds smooth scroll events.
 * @param {Object} options - Configuration parameters.
 * @param {string} options.barId - DOM ID of the alphabet bar container.
 * @param {string} options.workspaceId - DOM ID of the scrollable workspace container.
 * @param {Array<Object>} options.games - Filtered list of games currently rendered.
 * @param {string} options.sortOption - Current active sort criteria (e.g., 'Name').
 */
export function renderAlphabetBar({ barId, workspaceId, games, sortOption }) {
    const alphabetBar = document.getElementById(barId);
    const workspace = document.getElementById(workspaceId);
    if (!alphabetBar || !workspace) return;

    if (sortOption !== 'Name') {
        alphabetBar.style.display = 'none';
        return;
    }

    alphabetBar.style.display = 'flex';
    alphabetBar.innerHTML = '';

    const letters = new Set();
    games.forEach(g => {
        const title = g.Name || g.name || '';
        letters.add(getAlphabetGroup(title));
    });

    const sortedLetters = Array.from(letters).sort((a, b) => {
        if (a === '#') return -1;
        if (b === '#') return 1;
        return a.localeCompare(b);
    });

    sortedLetters.forEach(letter => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'alphabet-letter-btn';
        btn.innerText = letter;
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const cards = workspace.querySelectorAll('[data-letter]');
            const targetCard = Array.from(cards).find(card => card.getAttribute('data-letter') === letter);
            if (targetCard) {
                const workspaceRect = workspace.getBoundingClientRect();
                const cardRect = targetCard.getBoundingClientRect();
                const targetScrollTop = workspace.scrollTop + (cardRect.top - workspaceRect.top);
                workspace.scrollTo({
                    top: targetScrollTop,
                    behavior: 'smooth'
                });
            }
        });
        alphabetBar.appendChild(btn);
    });
}
