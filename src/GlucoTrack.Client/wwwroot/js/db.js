// GlucoTrack IndexedDB layer
// All items are stored with camelCase keys. The C# side serializes with camelCase and
// deserializes with case-insensitive matching.
window.glucoDb = (function () {
    const DB_NAME = 'glucotrack_v1';
    const DB_VERSION = 5;
    const STORES = ['meals', 'glucose', 'insulin', 'products', 'therapy', 'settings', 'planned_events', 'profile', 'user_insulins', 'meal_templates', 'modules'];

    let _db = null;

    function open() {
        if (_db) return Promise.resolve(_db);
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, DB_VERSION);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                STORES.forEach(name => {
                    if (!db.objectStoreNames.contains(name))
                        db.createObjectStore(name, { keyPath: 'id' });
                });
            };
            req.onsuccess = e => { _db = e.target.result; resolve(_db); };
            req.onerror = e => reject(e.target.error);
        });
    }

    function tx(store, mode, fn) {
        return open().then(db => new Promise((resolve, reject) => {
            const t = db.transaction(store, mode);
            t.onerror = e => reject(e.target.error);
            resolve(fn(t.objectStore(store)));
        }));
    }

    function promisify(req) {
        return new Promise((resolve, reject) => {
            req.onsuccess = e => resolve(e.target.result);
            req.onerror = e => reject(e.target.error);
        });
    }

    return {
        // Store a JSON-serialized item (from C# camelCase serialization)
        putJson: async function (store, json, pending) {
            const item = JSON.parse(json);
            item._pending = pending;
            return tx(store, 'readwrite', s => promisify(s.put(item)));
        },

        // Returns JSON string of all items in the store
        getAllJson: async function (store) {
            const db = await open();
            return new Promise((resolve, reject) => {
                const t = db.transaction(store, 'readonly');
                const req = t.objectStore(store).getAll();
                req.onsuccess = e => resolve(JSON.stringify(e.target.result));
                req.onerror = e => reject(e.target.error);
            });
        },

        // Returns JSON string of items where _pending === true
        getPendingJson: async function (store) {
            const db = await open();
            return new Promise((resolve, reject) => {
                const t = db.transaction(store, 'readonly');
                const req = t.objectStore(store).getAll();
                req.onsuccess = e => {
                    const pending = e.target.result.filter(i => i._pending === true);
                    resolve(JSON.stringify(pending));
                };
                req.onerror = e => reject(e.target.error);
            });
        },

        // Mark a single item as synced (clear _pending flag)
        markSynced: async function (store, id) {
            const db = await open();
            return new Promise((resolve, reject) => {
                const t = db.transaction(store, 'readwrite');
                const os = t.objectStore(store);
                const req = os.get(id);
                req.onsuccess = e => {
                    const item = e.target.result;
                    if (!item) { resolve(); return; }
                    item._pending = false;
                    const putReq = os.put(item);
                    putReq.onsuccess = () => resolve();
                    putReq.onerror = e2 => reject(e2.target.error);
                };
                req.onerror = e => reject(e.target.error);
            });
        },

        // Replace entire store with server data (used during pull, all _pending=false)
        bulkPutJson: async function (store, json) {
            const items = JSON.parse(json);
            const db = await open();
            return new Promise((resolve, reject) => {
                const t = db.transaction(store, 'readwrite');
                t.oncomplete = resolve;
                t.onerror = e => reject(e.target.error);
                const os = t.objectStore(store);
                items.forEach(item => {
                    item._pending = false;
                    os.put(item);
                });
            });
        },

        // Last sync timestamp (stored in localStorage as string)
        getLastSyncTicks: function () {
            return localStorage.getItem('glucotrack_last_sync') || '0';
        },

        setLastSyncTicks: function (ticks) {
            localStorage.setItem('glucotrack_last_sync', ticks.toString());
        },

        isOnline: function () {
            return navigator.onLine;
        }
    };
})();

// Notify Blazor when connectivity changes
window.registerConnectivityEvents = function (dotnetRef) {
    window.addEventListener('online', () => dotnetRef.invokeMethodAsync('OnOnline'));
    window.addEventListener('offline', () => dotnetRef.invokeMethodAsync('OnOffline'));
};
