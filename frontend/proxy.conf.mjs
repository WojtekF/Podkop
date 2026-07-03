// Proxies /api requests from the Angular dev server to the backend.
// Target resolution order:
//   1. API_URL                     — explicit manual override
//   2. services__server__http__0   — injected by Aspire via WithReference(server)
//   3. http://localhost:5381      — Podkop.Server default (launchSettings.json)
const target =
  process.env['API_URL'] ??
  process.env['services__server__http__0'] ??
  'http://localhost:5381';

export default {
  '/api': {
    target,
    changeOrigin: true,
    secure: false,
  },
};
