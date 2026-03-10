import path from "path"
import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"
import tailwindcss from "@tailwindcss/vite"

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      "/api": {
        target: process.env.API_URL ?? "http://localhost:5100",
        changeOrigin: true,
        headers: {
          "X-Api-Key": process.env.API_KEY ?? "changeme_dev_only",
        },
      },
    },
  },
})
