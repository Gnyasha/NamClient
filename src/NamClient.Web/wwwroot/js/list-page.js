// Clients/Contacts list: AJAX search, sort, paging, selection, bulk actions and context menu.
(function () {
    'use strict';

    const root = document.querySelector('[data-list]');
    if (!root) {
        return;
    }

    const body = root.querySelector('[data-list-body]');
    const searchForm = root.querySelector('[data-list-search]');
    const searchInput = searchForm.querySelector('input[name="q"]');
    const sizeSelect = root.querySelector('[data-list-size]');
    const defaultBar = root.querySelector('[data-toolbar-default]');
    const bulkBar = root.querySelector('[data-toolbar-bulk]');
    const bulkCount = root.querySelector('[data-bulk-count]');
    const bulkNote = root.querySelector('[data-bulk-note]');
    const noun = root.dataset.noun;
    const nounPlural = root.dataset.nounPlural;
    const MAX_BATCH = Number(root.dataset.maxBatch || 500);
    const LARGE_EXPORT = 50000;

    let inFlight = null;

    function label(count) {
        return count.toLocaleString() + ' ' + (count === 1 ? noun : nounPlural);
    }

    const selection = new App.Selection(body, {
        storageKey: 'selection:' + root.dataset.url,
        onChange: renderBulkBar,
    });

    function renderBulkBar() {
        const count = selection.size;
        defaultBar.hidden = count > 0;
        bulkBar.hidden = count === 0;
        bulkCount.textContent = label(count) + ' selected';

        const offPage = selection.offPageCount();
        bulkNote.hidden = offPage === 0;
        bulkNote.textContent = offPage === 0 ? '' : '(' + offPage.toLocaleString() + ' on other pages)';
    }

    function urlWith(changes) {
        const url = new URL(root.dataset.url, window.location.origin);
        const params = new URLSearchParams(window.location.search);

        params.delete('after');
        params.delete('before');

        Object.keys(changes).forEach(function (key) {
            const value = changes[key];
            if (value === null || value === undefined || value === '') {
                params.delete(key);
            } else {
                params.set(key, value);
            }
        });

        url.search = params.toString();
        return url;
    }

    async function load(url, push) {
        if (inFlight) {
            inFlight.abort();
        }
        inFlight = new AbortController();

        body.classList.add('is-loading');
        body.setAttribute('aria-busy', 'true');

        try {
            const response = await App.request(url, { signal: inFlight.signal });
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }
            body.innerHTML = await response.text();

            const relative = url.pathname + url.search;
            if (push) {
                history.pushState(null, '', relative);
            } else {
                history.replaceState(null, '', relative);
            }

            selection.sync();
            renderBulkBar();
        } catch (error) {
            if (error.name !== 'AbortError') {
                App.toast('The list could not be loaded. Please try again.', true);
            }
        } finally {
            body.classList.remove('is-loading');
            body.removeAttribute('aria-busy');
        }
    }

    function reload() {
        return load(new URL(window.location.href), false);
    }

    const searchNow = function () {
        load(urlWith({ q: searchInput.value.trim() }), false);
    };

    searchInput.addEventListener('input', App.debounce(searchNow, 300));

    searchForm.addEventListener('submit', function (event) {
        event.preventDefault();
        searchNow();
    });

    if (sizeSelect) {
        sizeSelect.addEventListener('change', function () {
            load(urlWith({ size: sizeSelect.value }), false);
        });
    }

    body.addEventListener('click', function (event) {
        const link = event.target.closest('a[data-list-nav]');
        if (!link || event.ctrlKey || event.metaKey) {
            return;
        }
        event.preventDefault();
        load(new URL(link.href), true).then(function () {
            if (link.closest('.pager')) {
                root.scrollIntoView({ block: 'start', behavior: 'smooth' });
            }
        });
    });

    window.addEventListener('popstate', function () {
        searchInput.value = new URLSearchParams(window.location.search).get('q') || '';
        reload();
    });

    document.addEventListener('keydown', function (event) {
        const typing = event.target.closest('input, textarea, select, [contenteditable]');
        if (event.key === '/' && !typing) {
            event.preventDefault();
            searchInput.focus();
            searchInput.select();
        }
    });

    async function deleteIds(ids) {
        if (ids.length > MAX_BATCH) {
            App.toast('You can delete up to ' + MAX_BATCH + ' ' + nounPlural + ' at a time.', true);
            return;
        }

        const confirmed = await App.confirm({
            title: 'Delete ' + label(ids.length) + '?',
            message: 'This permanently deletes ' + (ids.length === 1 ? 'the ' + noun : 'the selected ' + nounPlural) +
                ' and removes their links. This can\'t be undone.',
            confirmText: 'Delete',
        });
        if (!confirmed) {
            return;
        }

        const form = new FormData();
        ids.forEach(function (id) { form.append('ids', id); });

        try {
            const response = await App.request(root.dataset.deleteUrl, { method: 'POST', body: form });
            App.toast(await App.messageFrom(response), !response.ok);
            if (response.ok) {
                selection.remove(ids);
                await reload();
            }
        } catch (e) {
            App.toast('Delete failed. Please try again.', true);
        }
    }

    function exportIds(ids) {
        if (ids.length > MAX_BATCH) {
            App.toast('You can export up to ' + MAX_BATCH + ' selected ' + nounPlural + '. Use Export with a search for more.', true);
            return;
        }
        const url = new URL(root.dataset.exportUrl, window.location.origin);
        url.searchParams.set('ids', ids.join(','));
        const sort = new URLSearchParams(window.location.search).get('sort');
        if (sort) {
            url.searchParams.set('sort', sort);
        }
        download(url);
    }

    function download(url) {
        App.toast('Preparing your CSV download…');
        window.location.href = url.toString();
    }

    root.querySelector('[data-bulk-clear]').addEventListener('click', function () {
        selection.clear();
    });

    root.querySelector('[data-bulk-delete]').addEventListener('click', function () {
        deleteIds(selection.values());
    });

    root.querySelector('[data-bulk-export]').addEventListener('click', function () {
        exportIds(selection.values());
    });

    root.querySelector('[data-export]').addEventListener('click', async function () {
        const params = new URLSearchParams(window.location.search);
        const url = new URL(root.dataset.exportUrl, window.location.origin);
        ['q', 'sort'].forEach(function (key) {
            if (params.get(key)) {
                url.searchParams.set(key, params.get(key));
            }
        });

        const totalNode = body.querySelector('[data-total]');
        const total = totalNode ? Number(totalNode.dataset.total) : 0;
        if (total === 0) {
            App.toast('There is nothing to export.', true);
            return;
        }

        if (total >= LARGE_EXPORT) {
            const confirmed = await App.confirm({
                title: 'Export ' + label(total) + (totalNode.dataset.lowerBound === 'true' ? '+' : '') + '?',
                message: 'Large exports can take a minute to download. Narrow the list with search, or select rows, to export fewer.',
                confirmText: 'Export',
                danger: false,
            });
            if (!confirmed) {
                return;
            }
        }

        download(url);
    });

    App.rowContextMenu(body, function (row) {
        const id = row.dataset.id;
        const inSelection = selection.has(id) && selection.size > 1;
        const targets = inSelection ? selection.values() : [id];
        const suffix = inSelection ? ' ' + label(targets.length) : '';

        return [
            { label: 'Open', icon: 'open', action: function () { window.location.href = row.dataset.href; } },
            { label: 'Open in new tab', icon: 'external', action: function () { window.open(row.dataset.href, '_blank'); } },
            '-',
            selection.has(id)
                ? { label: 'Deselect', icon: 'square', action: function () { selection.set(id, false); } }
                : { label: 'Select', icon: 'check', action: function () { selection.set(id, true); } },
            { label: 'Export' + suffix + ' to CSV', icon: 'download', action: function () { exportIds(targets); } },
            '-',
            { label: 'Delete' + suffix, icon: 'trash', danger: true, action: function () { deleteIds(targets); } },
        ];
    });

    selection.sync();
    renderBulkBar();
})();
