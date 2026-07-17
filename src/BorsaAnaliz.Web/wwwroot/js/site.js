// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const form = document.querySelector('[data-watchlist-form]');
    const buttons = Array.from(document.querySelectorAll('[data-watchlist-toggle]'));
    if (!form || buttons.length === 0) return;

    const endpoint = form.dataset.endpoint;
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const status = document.getElementById('watchlistStatus');
    if (!endpoint || !token) return;

    const updateButtons = (symbol, added) => {
        buttons
            .filter(button => button.dataset.symbol === symbol)
            .forEach(button => {
                button.dataset.watchlisted = String(added);
                button.setAttribute('aria-pressed', String(added));
                button.title = added ? 'Takipten çıkar' : 'Takibe ekle';
                const icon = button.querySelector('i');
                if (icon) icon.className = `bi ${added ? 'bi-star-fill' : 'bi-star'}`;
                const label = button.querySelector('span');
                if (label) label.textContent = added ? 'Takipten çıkar' : 'Takibe ekle';
            });
    };

    buttons.forEach(button => {
        button.addEventListener('click', async () => {
            const symbol = button.dataset.symbol;
            if (!symbol || button.disabled) return;

            button.disabled = true;
            try {
                const body = new URLSearchParams({ symbol });
                const response = await fetch(endpoint, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
                        'RequestVerificationToken': token
                    },
                    body
                });
                const payload = await response.json().catch(() => ({}));
                if (!response.ok) {
                    throw new Error(payload.message || 'Takip listesi güncellenemedi.');
                }

                updateButtons(symbol, payload.added);
                if (status) {
                    status.textContent = payload.added
                        ? `${symbol} takip listenize eklendi.`
                        : `${symbol} takip listenizden çıkarıldı.`;
                }
            } catch (exception) {
                if (status) status.textContent = exception.message || 'Takip listesi güncellenemedi.';
            } finally {
                button.disabled = false;
            }
        });
    });
})();

(() => {
    const form = document.querySelector('[data-global-stock-search]');
    if (!form) return;

    const input = form.querySelector('input[type="search"]');
    const results = form.querySelector('[role="listbox"]');
    const endpoint = form.dataset.searchEndpoint;
    const stocksUrl = form.dataset.stocksUrl;
    if (!input || !results || !endpoint || !stocksUrl) return;

    let suggestions = [];
    let activeIndex = -1;
    let debounceTimer;
    let activeRequest;

    const closeResults = () => {
        results.classList.add('d-none');
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
        activeIndex = -1;
    };

    const setActive = index => {
        const options = Array.from(results.querySelectorAll('[role="option"]'));
        if (options.length === 0) return;
        activeIndex = (index + options.length) % options.length;
        options.forEach((option, optionIndex) => {
            const active = optionIndex === activeIndex;
            option.classList.toggle('active', active);
            option.setAttribute('aria-selected', String(active));
        });
        input.setAttribute('aria-activedescendant', options[activeIndex].id);
        options[activeIndex].scrollIntoView({ block: 'nearest' });
    };

    const renderResults = items => {
        suggestions = items;
        activeIndex = -1;
        results.replaceChildren();

        if (items.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'navbar-search-empty';
            empty.textContent = 'Eşleşen hisse bulunamadı.';
            results.appendChild(empty);
        } else {
            items.forEach((item, index) => {
                const option = document.createElement('a');
                option.id = `globalStockOption${index}`;
                option.className = 'navbar-search-option';
                option.href = item.url;
                option.role = 'option';
                option.setAttribute('aria-selected', 'false');

                const identity = document.createElement('span');
                const symbol = document.createElement('strong');
                symbol.textContent = item.symbol;
                const name = document.createElement('small');
                name.textContent = item.name;
                identity.append(symbol, name);

                const market = document.createElement('span');
                market.className = 'navbar-search-market';
                market.textContent = item.market;
                option.append(identity, market);
                results.appendChild(option);
            });
        }

        results.classList.remove('d-none');
        input.setAttribute('aria-expanded', 'true');
    };

    const loadSuggestions = async () => {
        const query = input.value.trim();
        if (!query) {
            suggestions = [];
            closeResults();
            return;
        }

        activeRequest?.abort();
        activeRequest = new AbortController();
        try {
            const url = new URL(endpoint, window.location.origin);
            url.searchParams.set('q', query);
            const response = await fetch(url, {
                signal: activeRequest.signal,
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) throw new Error('Arama sonuçları alınamadı.');
            renderResults(await response.json());
        } catch (error) {
            if (error.name !== 'AbortError') closeResults();
        }
    };

    input.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        activeRequest?.abort();
        suggestions = [];
        closeResults();
        debounceTimer = setTimeout(loadSuggestions, 160);
    });
    input.addEventListener('focus', () => {
        if (input.value.trim() && suggestions.length > 0) renderResults(suggestions);
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            setActive(activeIndex + (event.key === 'ArrowDown' ? 1 : -1));
        } else if (event.key === 'Escape') {
            closeResults();
        }
    });
    form.addEventListener('submit', event => {
        event.preventDefault();
        const selected = activeIndex >= 0 ? suggestions[activeIndex] : suggestions[0];
        if (selected?.url) {
            window.location.href = selected.url;
        } else if (input.value.trim()) {
            const url = new URL(stocksUrl, window.location.origin);
            url.searchParams.set('q', input.value.trim());
            window.location.href = url;
        }
    });
    document.addEventListener('click', event => {
        if (!form.contains(event.target)) closeResults();
    });
})();
