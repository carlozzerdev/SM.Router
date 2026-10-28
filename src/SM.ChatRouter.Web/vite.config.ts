import net from 'node:net';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Puertos de src/SM.ChatRouter.Api/Properties/launchSettings.json.
const API_HTTPS = 'https://localhost:7256';
const API_HTTP = 'http://localhost:5022';

function isListening(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = net.connect({ host: 'localhost', port, timeout: 500 });
    const done = (ok: boolean) => {
      socket.destroy();
      resolve(ok);
    };
    socket.once('connect', () => done(true));
    socket.once('error', () => done(false));
    socket.once('timeout', () => done(false));
  });
}

// Con el perfil "https" la API redirige http → https (UseHttpsRedirection) y el navegador no puede seguir
// esa redirección cross-origin, así que en ese caso el proxy va directo a https. API_URL fuerza el destino.
export default defineConfig(async () => {
  const apiTarget = process.env.API_URL ?? ((await isListening(7256)) ? API_HTTPS : API_HTTP);
  console.log(`  API: ${apiTarget}`);

  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        // secure: false acepta el certificado de desarrollo de .NET, que Node no reconoce.
        '/api': { target: apiTarget, changeOrigin: true, secure: false },
      },
    },
  };
});
