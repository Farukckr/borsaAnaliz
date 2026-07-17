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
