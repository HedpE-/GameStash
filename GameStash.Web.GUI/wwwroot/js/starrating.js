/**
 * Reusable Star Rating Component Generator
 */

/**
 * Generates an HTML snippet for a 5-star rating filled by percentage, followed by the numeric value.
 * @param {number|null} score - The rating score (0 to 100).
 * @param {Object} options - Customization options.
 * @param {boolean} options.showValue - Whether to display the numeric value next to the stars (default: true).
 * @param {boolean} options.asPercentage - Whether to format the score with a '%' suffix (default: false).
 * @param {number} options.decimals - Decimal places for the numeric score (default: 1).
 * @param {string} options.size - Optional size modifier: 'sm', 'md', or 'lg' (default: 'md').
 * @returns {string} HTML string.
 */
export function renderStarRating(score, options = {}) {
    const {
        showValue = true,
        asPercentage = false,
        decimals = 1,
        size = 'md'
    } = options;

    const numericScore = (typeof score === 'number' && !isNaN(score) && score > 0) ? score : 0;
    const fillPercent = Math.max(0, Math.min(100, numericScore));

    let formattedText = numericScore > 0 ? numericScore.toFixed(decimals) : '0.0';
    if (asPercentage && numericScore > 0) {
        formattedText += '%';
    }

    const valueHtml = showValue
        ? `<span class="star-rating-val">${formattedText}</span>`
        : '';

    return `
        <span class="star-rating-wrapper size-${size}">
            <span class="star-rating-box" aria-label="${formattedText} out of 100">
                <span class="stars-empty">★★★★★</span>
                <span class="stars-fill" style="width: ${fillPercent}%;">★★★★★</span>
            </span>
            ${valueHtml}
        </span>
    `;
}

/**
 * Updates an existing star rating element in the DOM directly without rebuilding markup.
 * @param {HTMLElement} containerElement - Container with .stars-fill and optional .star-rating-val.
 * @param {number} score - Score (0 to 100).
 * @param {number} decimals - Decimals for the text (default: 1).
 */
export function updateStarRatingElement(containerElement, score, decimals = 1) {
    if (!containerElement) return;

    const numericScore = (typeof score === 'number' && !isNaN(score) && score > 0) ? score : 0;
    const fillPercent = Math.max(0, Math.min(100, numericScore));

    const fillElement = containerElement.querySelector('.stars-fill');
    if (fillElement) {
        fillElement.style.width = `${fillPercent}%`;
    }

    const valElement = containerElement.querySelector('.star-rating-val');
    if (valElement) {
        valElement.innerText = numericScore > 0 ? numericScore.toFixed(decimals) : '0.0';
    }
}