const CACHE = 'said-africa-img-v1';
const IMG_HOSTS = ['res.cloudinary.com', 'cloudinary.com'];

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const url = event.request.url;
    const isCloudinary = IMG_HOSTS.some(h => url.includes(h));
    if (!isCloudinary) return;

    event.respondWith(
        caches.open(CACHE).then(cache =>
            cache.match(event.request).then(cached => {
                if (cached) return cached;
                return fetch(event.request).then(res => {
                    if (res.ok) cache.put(event.request, res.clone());
                    return res;
                });
            })
        )
    );
});
