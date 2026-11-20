// Contact(s)/Client(s) tab: linked list, unlink and the link picker.
(function () {
    'use strict';

    const panel = document.querySelector('[data-link-panel]');
    if (!panel) {
        return;
    }

    const countBadge = document.querySelector('[data-tab-count]');
    const failure =
        '<div class="empty-state"><p class="empty-message">This list could not be loaded.</p>' +
        '<p class="empty-hint">Refresh the page to try again.</p></div>';

    const noun = panel.dataset.noun;
    const nounPlural = panel.dataset.nounPlural;
    const owner = panel.dataset.owner;

    function label(count) {
        return count.toLocaleString() + ' ' + (count === 1 ? noun : nounPlural);
    }

    const selection = new App.Selection(panel, { onChange: renderBulkBar });

    function renderBulkBar() {
        const defaultBar = panel.querySelector('[data-toolbar-default]');
        const bulkBar = panel.querySelector('[data-toolbar-bulk]');
        if (!defaultBar || !bulkBar) {
            return;
        }
        defaultBar.hidden = selection.size > 0;
        bulkBar.hidden = selection.size === 0;
        panel.querySelector('[data-bulk-count]').textContent = label(selection.size) + ' selected';
    }

    async function render(response) {
        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }
        panel.innerHTML = await response.text();
        App.consumeStatus(panel);

        selection.ids.clear();
        selection.sync();
        renderBulkBar();

        const count = panel.querySelector('[data-linked-count]');
        if (countBadge && count) {
            countBadge.textContent = Number(count.dataset.linkedCount).toLocaleString();
            countBadge.hidden = false;
        }
    }

    async function loadPanel(url, options) {
        panel.setAttribute('aria-busy', 'true');
        try {
            await render(await App.request(url, options));
        } catch (e) {
            panel.innerHTML = failure;
        } finally {
            panel.removeAttribute('aria-busy');
        }
    }

    async function unlink(ids, singleLabel) {
        const what = ids.length === 1 && singleLabel ? singleLabel : label(ids.length);
        const confirmed = await App.confirm({
            title: 'Unlink ' + what + '?',
            message: (ids.length === 1 ? 'It' : 'They') + ' will no longer be linked to ' + owner +
                '. Nothing is deleted; you can link ' + (ids.length === 1 ? 'it' : 'them') + ' again at any time.',
            confirmText: 'Unlink',
        });
        if (!confirmed) {
            return;
        }

        const form = new FormData();
        ids.forEach(function (id) { form.append(panel.dataset.fieldName, id); });
        loadPanel(panel.dataset.unlinkUrl, { method: 'POST', body: form });
    }

    panel.addEventListener('click', function (event) {
        if (event.target.closest('[data-bulk-unlink]')) {
            unlink(selection.values());
        } else if (event.target.closest('[data-bulk-clear]')) {
            selection.clear();
        }
    });

    App.rowContextMenu(panel, function (row) {
        const id = row.dataset.id;
        const inSelection = selection.has(id) && selection.size > 1;
        const targets = inSelection ? selection.values() : [id];

        return [
            { label: 'Open', icon: 'open', action: function () { window.location.href = row.dataset.href; } },
            { label: 'Open in new tab', icon: 'external', action: function () { window.open(row.dataset.href, '_blank'); } },
            '-',
            selection.has(id)
                ? { label: 'Deselect', icon: 'square', action: function () { selection.set(id, false); } }
                : { label: 'Select', icon: 'check', action: function () { selection.set(id, true); } },
            '-',
            {
                label: inSelection ? 'Unlink ' + label(targets.length) : 'Unlink',
                icon: 'unlink',
                danger: true,
                action: function () { unlink(targets, inSelection ? null : row.dataset.label); },
            },
        ];
    });

    const modalElement = document.getElementById('link-picker');
    if (modalElement) {
        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        const search = modalElement.querySelector('[data-picker-search]');
        const results = modalElement.querySelector('[data-picker-results]');
        const submit = modalElement.querySelector('[data-picker-submit]');
        const error = modalElement.querySelector('[data-picker-error]');
        const noun = modalElement.dataset.noun;
        const nounPlural = modalElement.dataset.nounPlural;
        const fieldName = modalElement.dataset.fieldName;
        const selected = new Set();
        let inFlight = null;

        function updateSubmit() {
            const count = selected.size;
            submit.textContent = count === 0
                ? 'Link ' + nounPlural
                : 'Link ' + count + ' ' + (count === 1 ? noun : nounPlural);
            if (count > 0) {
                error.hidden = true;
            }
        }

        async function loadOptions() {
            if (inFlight) {
                inFlight.abort();
            }
            inFlight = new AbortController();

            const url = new URL(modalElement.dataset.optionsUrl, window.location.origin);
            const term = search.value.trim();
            if (term) {
                url.searchParams.set('q', term);
            }

            results.setAttribute('aria-busy', 'true');
            try {
                const response = await App.request(url, { signal: inFlight.signal });
                if (!response.ok) {
                    throw new Error('HTTP ' + response.status);
                }
                results.innerHTML = await response.text();
                results.querySelectorAll('input[type="checkbox"]').forEach(function (box) {
                    box.checked = selected.has(box.value);
                });
            } catch (e) {
                if (e.name !== 'AbortError') {
                    results.innerHTML = '<div class="picker-empty">Results could not be loaded. Please try again.</div>';
                }
            } finally {
                results.removeAttribute('aria-busy');
            }
        }

        modalElement.addEventListener('show.bs.modal', function () {
            selected.clear();
            search.value = '';
            error.hidden = true;
            updateSubmit();
            loadOptions();
        });

        modalElement.addEventListener('shown.bs.modal', function () {
            search.focus();
        });

        search.addEventListener('input', App.debounce(loadOptions, 250));
        search.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
            }
        });

        results.addEventListener('change', function (event) {
            const box = event.target;
            if (box.type !== 'checkbox') {
                return;
            }
            if (box.checked) {
                selected.add(box.value);
            } else {
                selected.delete(box.value);
            }
            updateSubmit();
        });

        submit.addEventListener('click', async function () {
            if (selected.size === 0) {
                error.hidden = false;
                search.focus();
                return;
            }

            const form = new FormData();
            selected.forEach(function (id) { form.append(fieldName, id); });

            App.setBusy(submit, true);
            try {
                await render(await App.request(modalElement.dataset.linkUrl, { method: 'POST', body: form }));
                modal.hide();
            } catch (e) {
                App.toast('Linking failed. Please try again.', true);
            } finally {
                App.setBusy(submit, false);
                updateSubmit();
            }
        });
    }

    loadPanel(panel.dataset.loadUrl);
})();
