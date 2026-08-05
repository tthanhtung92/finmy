import http from 'k6/http';

const BASE = __ENV.BASE_URL || 'http://localhost:5079';

// /envelopes now requires an authenticated user (TECH-DEBT #1), so the run needs a token
// before it can hit the endpoint being benchmarked. Register is allowed to fail here: once the
// user exists, later runs against the same environment hit a 409 and move straight to login.
export function setup() {
    const email = 'bench@finmy.local';
    const password = 'Bench-Password-1';

    http.post(`${BASE}/api/v1/identity/register`, JSON.stringify({ email, password }), {
        headers: { 'Content-Type': 'application/json' },
    });

    const login = http.post(`${BASE}/api/v1/identity/login`, JSON.stringify({ email, password }), {
        headers: { 'Content-Type': 'application/json' },
    });

    return { accessToken: login.json('accessToken') };
}

export default function (data) {
    const headers = { Authorization: `Bearer ${data.accessToken}` };

    if (__ENV.MODE === 'hit') {
        http.get(`${BASE}/api/v1/envelopes?page=1&pageSize=20`, { headers });
    } else {
        // The miss branch asks for a huge random page number so the cache cannot serve it.
        const page = Math.floor(Math.random() * 1000000) + 1;
        http.get(`${BASE}/api/v1/envelopes?page=${page}&pageSize=20`, { headers });
    }
}
