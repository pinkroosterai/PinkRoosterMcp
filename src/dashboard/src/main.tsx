import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import "./index.css"
import App from "./App"

// Apply dark class before first paint to prevent FOUC
const stored = localStorage.getItem("pinkrooster-theme");
if (stored !== "light") {
  document.documentElement.classList.add("dark");
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
