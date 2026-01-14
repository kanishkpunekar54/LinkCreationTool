import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";
import * as path from "path";
import { componentTagger } from "lovable-tagger";

const rootDir = process.cwd();

export default defineConfig(({ mode }) => ({
  root: rootDir,

  build: {
    outDir: path.resolve(rootDir, "wwwroot/dist"),
    emptyOutDir: true,
    // Remove rollupOptions.input — Vite handles it automatically with root: "src"
  },

  server: {
    host: true,
    port: 8080,
    strictPort: true,
    proxy: {
      "/api": {
        target: "https://localhost:7001",
        changeOrigin: true,
        secure: false,
      },
    },
  },

  plugins: [
    react(),
    mode === "development" && componentTagger(),
  ].filter(Boolean),

  resolve: {
    alias: {
      "@": path.resolve(rootDir, "src"),
    },
  },
}));