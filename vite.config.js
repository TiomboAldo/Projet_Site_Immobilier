import { defineConfig } from 'vite';
import { resolve } from 'path';

export default defineConfig({

  // ==================== SERVEUR DE DÉVELOPPEMENT ====================
  server: {
    port: 3000,          // Port fixe (change si besoin)
    strictPort: false,   // Si port occupé, Vite en cherche un autre
    open: true,          // Ouvre automatiquement le navigateur au démarrage
    watch: {
      usePolling: true,  // Utile sur Windows pour détecter les changements
    },
  },

  // ==================== BUILD (PRODUCTION) ====================
  build: {
    outDir: 'dist',      // Dossier de sortie pour la production
    emptyOutDir: true,   // Vide le dossier dist avant chaque build
    
    rollupOptions: {
      input: {
        // Pages principales
        main:          resolve(__dirname, 'index.html'),
        
        // Pages dans src/pages
        login:         resolve(__dirname, 'src/pages/login.html'),
        accueil_user:  resolve(__dirname, 'src/pages/accueil_user.html'),
        details:       resolve(__dirname, 'src/pages/details.html'),
        espace_proprietaire: resolve(__dirname, 'src/pages/espace_proprietaire.html'),
        espace_admin_region: resolve(__dirname, 'src/pages/espace_admin_region.html'),
        // ↑ Ajoute ici toutes tes nouvelles pages
      },
    },
  },

  // ==================== RÉSOLUTION DES CHEMINS ====================
  resolve: {
    alias: {
      '@':       resolve(__dirname, 'src'),          // "@/js/main.js" → "src/js/main.js"
      '@assets': resolve(__dirname, 'src/assets'),   // "@assets/images/..." 
      '@pages':  resolve(__dirname, 'src/pages'),    // "@pages/login.html"
      '@styles': resolve(__dirname, 'src/style'),    // "@styles/input.css"
    },
  },

});