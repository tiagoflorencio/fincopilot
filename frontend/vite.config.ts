import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// O proxy evita CORS em desenvolvimento: o frontend chama /api e o Vite repassa ao ASP.NET Core.
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
})
