import { defineConfig } from 'vite';
import { resolve } from 'path';

export default defineConfig({
  build: {
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
        login: resolve(__dirname, 'src/pages/login.html'), // Ajoute tes pages ici
        accueil_user: resolve(__dirname, 'src/pages/accueil_user.html'),
      },
    },
  },
});