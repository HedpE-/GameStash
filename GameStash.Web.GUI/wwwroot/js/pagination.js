/**
 * Reusable Advanced Pagination Controller with dynamic jump steps and SVG icons
 */
export class PaginationController {
    /**
     * @param {Object} config
     * @param {number} config.paginationRadius - Visible numeric pages radius around current page (default: 2)
     * @param {number[]} config.jumpSteps - Array of page increments for quick jumping (default:)
     * @param {Function} config.onPageChange - Callback invoked when active page changes: (newPage) => void
     */
    constructor({ paginationRadius = 2, jumpSteps = 0, onPageChange = () => { } } = {}) {
        this.paginationRadius = paginationRadius;
        this.jumpSteps = jumpSteps.sort((a, b) => a - b); // Garante ordem crescente (ex:)
        this.onPageChange = onPageChange;
        this.currentPage = 1;
        this.pageSize = 15;
        this.totalItems = 0;
        this.totalPages = 1;
    }

    /**
     * Sets internal dataset and re-evaluates boundary pages.
     */
    setPagination(totalItems, pageSize, currentPage = null) {
        this.totalItems = totalItems;
        this.pageSize = pageSize > 0 ? pageSize : 1;
        this.totalPages = Math.ceil(this.totalItems / this.pageSize) || 1;

        if (currentPage !== null) {
            this.currentPage = currentPage;
        }

        if (this.currentPage > this.totalPages) this.currentPage = this.totalPages;
        if (this.currentPage < 1) this.currentPage = 1;
    }

    /**
     * Returns { startIndex, endIndex } for slicing the current page array.
     */
    getSliceIndices() {
        const startIndex = (this.currentPage - 1) * this.pageSize;
        const endIndex = startIndex + this.pageSize;
        return { startIndex, endIndex };
    }

    /**
     * Navigates to target page and notifies subscriber.
     */
    goToPage(page) {
        const clamped = Math.max(1, Math.min(this.totalPages, page));
        if (this.currentPage !== clamped) {
            this.currentPage = clamped;
            this.onPageChange(this.currentPage);
        }
    }

    /**
     * Updates pagination UI containers and binds dynamic action workflows.
     */
    renderControls() {
        const infoSpan = document.getElementById('pagination-info');
        if (infoSpan) {
            infoSpan.innerText = `Page ${this.currentPage} of ${this.totalPages}`;
        }

        const isFirst = this.currentPage === 1;
        const isLast = this.currentPage === this.totalPages;

        // Contentor Principal da Barra de Paginação
        const container = document.getElementById('pagination-numbers-container');
        if (!container) return;
        container.innerHTML = '';

        // --- 1. BOTÕES DE CONTROLE ESQUERDOS (First, Jumps decrescentes, Prev) ---
        const firstBtn = this._createSvgButton('M18.5 5L12 12l6.5 7M11.5 5L5 12l6.5 7', isFirst, () => this.goToPage(1));
        container.appendChild(firstBtn);

        // Renderiza os Jumps de trás para a frente (ex: -10, -5) para manter a ordem lógica no ecrã
        [...this.jumpSteps].reverse().forEach(step => {
            const disabledCondition = this.currentPage === 1;
            const jumpBtn = this._createTextButton(`-${step}`, disabledCondition, () => this.goToPage(Math.max(1, this.currentPage - step)));
            container.appendChild(jumpBtn);
        });

        const prevBtn = this._createSvgButton('M15.5 19l-7-7 7-7', isFirst, () => this.goToPage(this.currentPage - 1));
        container.appendChild(prevBtn);

        // --- 2. BLOCO CENTRAL DE NÚMEROS DE PÁGINA (± RADIUS) ---
        const numbersGroup = document.createElement('div');
        numbersGroup.className = 'pagination-numbers';

        const startPage = Math.max(1, this.currentPage - this.paginationRadius);
        const endPage = Math.min(this.totalPages, this.currentPage + this.paginationRadius);

        for (let i = startPage; i <= endPage; i++) {
            const numBtn = document.createElement('button');
            numBtn.type = 'button';
            numBtn.className = `page-number-btn ${i === this.currentPage ? 'active' : ''}`;
            numBtn.innerText = i;
            numBtn.addEventListener('click', () => this.goToPage(i));
            numbersGroup.appendChild(numBtn);
        }
        container.appendChild(numbersGroup);

        // --- 3. BOTÕES DE CONTROLE DIREITOS (Next, Jumps crescentes, Last) ---
        const nextBtn = this._createSvgButton('M8.5 5l7 7-7 7', isLast, () => this.goToPage(this.currentPage + 1));
        container.appendChild(nextBtn);

        this.jumpSteps.forEach(step => {
            const disabledCondition = this.currentPage === this.totalPages;
            const jumpBtn = this._createTextButton(`+${step}`, disabledCondition, () => this.goToPage(Math.min(this.totalPages, this.currentPage + step)));
            container.appendChild(jumpBtn);
        });

        const lastBtn = this._createSvgButton('M5.5 5L12 12l-6.5 7M12.5 5l6.5 7-6.5 7', isLast, () => this.goToPage(this.totalPages));
        container.appendChild(lastBtn);
    }

    /**
     * Helper to instantiate a text-based jump action control button node.
     */
    _createTextButton(text, disabled, callback) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pagination-btn jump-btn';
        btn.innerText = text;
        btn.disabled = disabled;
        if (!disabled) btn.addEventListener('click', callback);
        return btn;
    }

    /**
     * Helper to instantiate a perfectly scaled and balanced SVG vector icon control button node.
     */
    _createSvgButton(svgPath, disabled, callback) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pagination-btn icon-btn';
        btn.disabled = disabled;

        btn.innerHTML = `
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="${svgPath}"></path>
            </svg>
        `;

        if (!disabled) btn.addEventListener('click', callback);
        return btn;
    }
}
