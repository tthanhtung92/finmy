import http from 'k6/http';

const BASE = __ENV.BASE_URL || 'http://localhost:5079';

export default function () {
    if (__ENV.MODE === 'hit') {
        http.get(`${BASE}/envelopes?page=1&pageSize=20`);
    } else {
        // The miss branch asks for a huge random page number so the cache cannot serve it.
        const page = Math.floor(Math.random() * 1000000) + 1;
        http.get(`${BASE}/envelopes?page=${page}&pageSize=20`);
    }
}
