// Shared helpers: requests, toasts, dialogs, selection, context menu and form tracking.
(function () {
    'use strict';

    const App = {};

    App.token = function () {
        const meta = document.querySelector('meta[name="request-verification-token"]');
        return meta ? meta.content : '';
    };

    App.request = function (url, options) {
        const opts = options || {};
        const method = (opts.method || 'GET').toUpperCase();
        const headers = { 'X-Requested-With': 'XMLHttpRequest' };
        if (method !== 'GET') {
            headers.RequestVerificationToken = App.token();
        }
        return fetch(url, { method: method, body: opts.body, headers: headers, signal: opts.signal, credentials: 'same-origin' });
    };

    App.messageFrom = async function (response, fallback) {
        try {
            const data = await response.json();
            if (data && data.message) {
                return data.message;
            }
        } catch (e) {
        }
        return fallback || 'Something went wrong. Please try again.';
    };

    App.debounce = function (fn, wait) {
        let timer;
        return function () {
            const args = arguments;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(null, args); }, wait);
        };
    };

    App.toast = function (text, isError) {
        const container = document.getElementById('toast-container');
        if (!container || !text) {
            return;
        }

        const toast = document.createElement('div');
        toast.className = 'toast app-toast' + (isError ? ' is-error' : '');
        toast.setAttribute('role', isError ? 'alert' : 'status');
        toast.innerHTML =
            '<svg class="icon toast-icon" aria-hidden="true"><use href="#i-' + (isError ? 'alert' : 'check') + '"></use></svg>' +
            '<div class="toast-text"></div>' +
            '<button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>';
        toast.querySelector('.toast-text').textContent = text;
        container.appendChild(toast);

        toast.addEventListener('hidden.bs.toast', function () { toast.remove(); });
        new bootstrap.Toast(toast, { delay: isError ? 7000 : 4000 }).show();
    };

    App.consumeStatus = function (root) {
        root.querySelectorAll('[data-status]').forEach(function (node) {
            App.toast(node.dataset.text, node.dataset.error === 'true');
            node.remove();
        });
    };

    App.confirm = function (options) {
        const element = document.getElementById('confirm-modal');
        const modal = bootstrap.Modal.getOrCreateInstance(element);
        const ok = element.querySelector('[data-confirm-ok]');
        const danger = options.danger !== false;

        element.querySelector('[data-confirm-title]').textContent = options.title || 'Are you sure?';
        element.querySelector('[data-confirm-message]').textContent = options.message || '';
        element.querySelector('[data-confirm-icon]').hidden = !danger;
        ok.textContent = options.confirmText || 'Confirm';
        ok.className = 'btn ' + (danger ? 'btn-danger' : 'btn-primary');

        return new Promise(function (resolve) {
            let confirmed = false;
            function onOk() {
                confirmed = true;
                modal.hide();
            }
            function onShown() {
                element.querySelector('[data-bs-dismiss="modal"]').focus();
            }
            ok.addEventListener('click', onOk);
            element.addEventListener('shown.bs.modal', onShown, { once: true });
            element.addEventListener('hidden.bs.modal', function () {
                ok.removeEventListener('click', onOk);
                resolve(confirmed);
            }, { once: true });
            modal.show();
        });
    };

    App.setBusy = function (button, busy) {
        if (!button) {
            return;
        }
        if (busy) {
            button.dataset.label = button.innerHTML;
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border" role="status" aria-hidden="true"></span><span>' +
                (button.dataset.busyLabel || 'Working…') + '</span>';
        } else if (button.dataset.label !== undefined) {
            button.disabled = false;
            button.innerHTML = button.dataset.label;
            delete button.dataset.label;
        }
    };

    // Selected ids live in a Set, so a selection survives paging and re-renders.
    App.Selection = function (root, options) {
        const opts = options || {};
        this.root = root;
        this.storageKey = opts.storageKey || null;
        this.onChange = opts.onChange || function () {};
        this.ids = new Set(this.load());
        this.lastClicked = null;

        const self = this;

        root.addEventListener('click', function (event) {
            const box = event.target.closest('input[data-select]');
            if (!box) {
                return;
            }
            const boxes = self.pageBoxes();
            if (event.shiftKey && self.lastClicked && boxes.indexOf(self.lastClicked) >= 0) {
                const from = boxes.indexOf(self.lastClicked);
                const to = boxes.indexOf(box);
                boxes.slice(Math.min(from, to), Math.max(from, to) + 1).forEach(function (b) {
                    self.set(b.value, box.checked, false);
                });
                self.commit();
            }
            self.lastClicked = box;
        });

        root.addEventListener('change', function (event) {
            if (event.target.matches('input[data-select-all]')) {
                self.pageBoxes().forEach(function (b) { self.set(b.value, event.target.checked, false); });
                self.commit();
            } else if (event.target.matches('input[data-select]')) {
                self.set(event.target.value, event.target.checked, false);
                self.commit();
            }
        });
    };

    App.Selection.prototype = {
        pageBoxes: function () {
            return Array.from(this.root.querySelectorAll('input[data-select]'));
        },
        has: function (id) {
            return this.ids.has(String(id));
        },
        get size() {
            return this.ids.size;
        },
        values: function () {
            return Array.from(this.ids);
        },
        offPageCount: function () {
            const onPage = new Set(this.pageBoxes().map(function (b) { return b.value; }));
            return this.values().filter(function (id) { return !onPage.has(id); }).length;
        },
        set: function (id, selected, commit) {
            if (selected) {
                this.ids.add(String(id));
            } else {
                this.ids.delete(String(id));
            }
            if (commit !== false) {
                this.commit();
            }
        },
        toggle: function (id) {
            this.set(id, !this.has(id));
        },
        remove: function (ids) {
            const self = this;
            ids.forEach(function (id) { self.ids.delete(String(id)); });
            this.commit();
        },
        clear: function () {
            this.ids.clear();
            this.commit();
        },
        commit: function () {
            this.save();
            this.sync();
            this.onChange(this);
        },
        sync: function () {
            const self = this;
            const boxes = this.pageBoxes();
            boxes.forEach(function (box) {
                box.checked = self.has(box.value);
                const row = box.closest('tr');
                if (row) {
                    row.classList.toggle('is-selected', box.checked);
                }
            });
            const all = this.root.querySelector('input[data-select-all]');
            if (all) {
                const selectedOnPage = boxes.filter(function (b) { return b.checked; }).length;
                all.checked = boxes.length > 0 && selectedOnPage === boxes.length;
                all.indeterminate = selectedOnPage > 0 && selectedOnPage < boxes.length;
            }
        },
        load: function () {
            if (!this.storageKey) {
                return [];
            }
            try {
                return JSON.parse(sessionStorage.getItem(this.storageKey) || '[]');
            } catch (e) {
                return [];
            }
        },
        save: function () {
            if (!this.storageKey) {
                return;
            }
            try {
                sessionStorage.setItem(this.storageKey, JSON.stringify(this.values()));
            } catch (e) {
            }
        },
    };

    const menu = document.createElement('div');
    menu.className = 'context-menu';
    menu.setAttribute('role', 'menu');
    menu.hidden = true;
    let menuReturnFocus = null;

    function menuItems() {
        return Array.from(menu.querySelectorAll('[role="menuitem"]:not(:disabled)'));
    }

    function closeMenu(restoreFocus) {
        if (menu.hidden) {
            return;
        }
        menu.hidden = true;
        if (restoreFocus && menuReturnFocus && document.contains(menuReturnFocus)) {
            menuReturnFocus.focus();
        }
        menuReturnFocus = null;
    }

    App.contextMenu = {
        open: function (x, y, items, opener) {
            if (!menu.isConnected) {
                document.body.appendChild(menu);
            }
            menu.innerHTML = '';

            items.forEach(function (item) {
                if (item === '-') {
                    const divider = document.createElement('div');
                    divider.className = 'context-menu-divider';
                    divider.setAttribute('role', 'separator');
                    menu.appendChild(divider);
                    return;
                }
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'context-menu-item' + (item.danger ? ' is-danger' : '');
                button.setAttribute('role', 'menuitem');
                button.disabled = !!item.disabled;
                button.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#i-' + item.icon + '"></use></svg><span></span>';
                button.querySelector('span').textContent = item.label;
                button.addEventListener('click', function () {
                    closeMenu(false);
                    item.action();
                });
                menu.appendChild(button);
            });

            menuReturnFocus = opener || document.activeElement;
            menu.hidden = false;

            const width = menu.offsetWidth;
            const height = menu.offsetHeight;
            const left = Math.min(x, window.innerWidth - width - 8);
            const top = y + height > window.innerHeight - 8 ? Math.max(8, y - height) : y;
            menu.style.left = Math.max(8, left) + 'px';
            menu.style.top = top + 'px';

            const first = menuItems()[0];
            if (first) {
                first.focus();
            }
        },
        close: function () {
            closeMenu(false);
        },
    };

    menu.addEventListener('keydown', function (event) {
        const items = menuItems();
        const index = items.indexOf(document.activeElement);
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            const step = event.key === 'ArrowDown' ? 1 : -1;
            items[(index + step + items.length) % items.length].focus();
        } else if (event.key === 'Home' || event.key === 'End') {
            event.preventDefault();
            items[event.key === 'Home' ? 0 : items.length - 1].focus();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            closeMenu(true);
        } else if (event.key === 'Tab') {
            closeMenu(true);
        }
    });

    document.addEventListener('mousedown', function (event) {
        if (!menu.hidden && !menu.contains(event.target)) {
            closeMenu(false);
        }
    });
    window.addEventListener('resize', function () { closeMenu(false); });
    window.addEventListener('scroll', function () { closeMenu(false); }, true);

    App.rowContextMenu = function (root, getItems) {
        root.addEventListener('contextmenu', function (event) {
            const row = event.target.closest('tr[data-id]');
            if (!row || !root.contains(row)) {
                return;
            }
            event.preventDefault();

            let x = event.clientX;
            let y = event.clientY;
            if (event.button !== 2 || (x === 0 && y === 0)) {
                const rect = (event.target.closest('a, input') || row).getBoundingClientRect();
                x = rect.left;
                y = rect.bottom + 4;
            }
            App.contextMenu.open(x, y, getItems(row), row.querySelector('a.row-title') || row);
        });
    };

    document.addEventListener('click', function (event) {
        const row = event.target.closest('tr[data-href]');
        if (!row || event.target.closest('a, button, input, label, select, textarea')) {
            return;
        }
        if (window.getSelection && String(window.getSelection()).length > 0) {
            return;
        }
        if (event.ctrlKey || event.metaKey) {
            window.open(row.dataset.href, '_blank');
        } else {
            window.location.href = row.dataset.href;
        }
    });

    document.querySelectorAll('[data-bs-toggle="tab"][data-tab-hash]').forEach(function (tab) {
        tab.addEventListener('shown.bs.tab', function () {
            history.replaceState(null, '', '#' + tab.dataset.tabHash);
        });
    });

    function showTabFromHash() {
        const hash = window.location.hash.replace('#', '');
        if (!hash) {
            return;
        }
        const tab = document.querySelector('[data-bs-toggle="tab"][data-tab-hash="' + CSS.escape(hash) + '"]');
        if (tab) {
            bootstrap.Tab.getOrCreateInstance(tab).show();
        }
    }

    window.addEventListener('hashchange', showTabFromHash);

    document.addEventListener('click', async function (event) {
        const button = event.target.closest('[data-confirm-submit]');
        if (!button) {
            return;
        }
        event.preventDefault();

        const confirmed = await App.confirm({
            title: button.dataset.confirmTitle,
            message: button.dataset.confirmSubmit,
            confirmText: button.dataset.confirmText || 'Delete',
        });
        if (confirmed) {
            App.setBusy(button, true);
            button.form.submit();
        }
    });

    document.addEventListener('click', async function (event) {
        const button = event.target.closest('[data-ajax-post]');
        if (!button) {
            return;
        }
        event.preventDefault();

        if (button.dataset.confirm) {
            const confirmed = await App.confirm({
                title: button.dataset.confirmTitle,
                message: button.dataset.confirm,
                confirmText: button.dataset.confirmText || 'Continue',
                danger: button.dataset.danger === 'true',
            });
            if (!confirmed) {
                return;
            }
        }

        App.setBusy(button, true);
        try {
            const form = button.closest('form');
            const response = await App.request(button.dataset.ajaxPost, { method: 'POST', body: form ? new FormData(form) : null });
            const message = await App.messageFrom(response);
            App.toast(message, !response.ok);
        } catch (e) {
            App.toast('The request failed. Please check the connection and try again.', true);
        } finally {
            App.setBusy(button, false);
        }
    });

    // Save stays disabled until a value actually changes.
    function trackChanges(form) {
        const submit = form.querySelector('[data-dirty-submit]');
        const startsDirty = form.dataset.dirtyCheck === 'start-dirty';
        let submitting = false;

        function snapshot() {
            return JSON.stringify(Array.from(new FormData(form))
                .filter(function (pair) { return pair[0] !== '__RequestVerificationToken'; })
                .map(function (pair) { return [pair[0], String(pair[1]).trim()]; }));
        }

        const initial = snapshot();
        const changed = function () { return snapshot() !== initial; };

        function update() {
            const enabled = startsDirty || changed();
            if (submit) {
                submit.disabled = !enabled;
                submit.title = enabled ? '' : 'No changes to save';
            }
        }

        form.addEventListener('input', update);
        form.addEventListener('change', update);
        form.addEventListener('submit', function () { submitting = true; });
        window.addEventListener('beforeunload', function (event) {
            if (!submitting && changed()) {
                event.preventDefault();
                event.returnValue = '';
            }
        });
        update();
    }

    document.querySelectorAll('form[data-dirty-check]').forEach(trackChanges);

    document.addEventListener('DOMContentLoaded', function () {
        const initial = document.getElementById('initial-status');
        if (initial) {
            try {
                const status = JSON.parse(initial.textContent);
                App.toast(status.text, status.isError);
            } catch (e) {
            }
        }
        showTabFromHash();
    });

    window.App = App;
})();
