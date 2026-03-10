import type { Plugin } from "vite";
import { randomUUID } from "crypto";

const AUTH_TOKEN_KEY = "pinkrooster-auth";
const TOKEN_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours

interface TokenEntry {
  expiresAt: number;
}

export function authPlugin(): Plugin {
  const tokens = new Map<string, TokenEntry>();

  function isProtected(): boolean {
    return !!(process.env.DASHBOARD_USER && process.env.DASHBOARD_PASSWORD);
  }

  function isValidToken(authHeader: string | undefined): boolean {
    if (!authHeader?.startsWith("Bearer ")) return false;
    const token = authHeader.slice(7);
    const entry = tokens.get(token);
    if (!entry) return false;
    if (Date.now() > entry.expiresAt) {
      tokens.delete(token);
      return false;
    }
    return true;
  }

  function readBody(req: import("http").IncomingMessage): Promise<string> {
    return new Promise((resolve, reject) => {
      let data = "";
      req.on("data", (chunk: Buffer) => (data += chunk.toString()));
      req.on("end", () => resolve(data));
      req.on("error", reject);
    });
  }

  return {
    name: "pinkrooster-auth",
    configureServer(server) {
      server.middlewares.use("/auth/config", (req, res) => {
        if (req.method !== "GET") {
          res.statusCode = 405;
          res.end(JSON.stringify({ error: "Method not allowed" }));
          return;
        }

        const prot = isProtected();
        const authenticated = prot
          ? isValidToken(req.headers.authorization)
          : false;

        res.setHeader("Content-Type", "application/json");
        res.end(JSON.stringify({ protected: prot, authenticated }));
      });

      server.middlewares.use("/auth/login", async (req, res) => {
        if (req.method !== "POST") {
          res.statusCode = 405;
          res.end(JSON.stringify({ error: "Method not allowed" }));
          return;
        }

        if (!isProtected()) {
          res.setHeader("Content-Type", "application/json");
          res.end(JSON.stringify({ error: "Authentication is not enabled" }));
          return;
        }

        try {
          const body = JSON.parse(await readBody(req));
          const { username, password } = body;

          if (
            username === process.env.DASHBOARD_USER &&
            password === process.env.DASHBOARD_PASSWORD
          ) {
            const token = randomUUID();
            tokens.set(token, { expiresAt: Date.now() + TOKEN_TTL_MS });
            res.setHeader("Content-Type", "application/json");
            res.end(JSON.stringify({ token }));
          } else {
            res.statusCode = 401;
            res.setHeader("Content-Type", "application/json");
            res.end(JSON.stringify({ error: "Invalid credentials" }));
          }
        } catch {
          res.statusCode = 400;
          res.setHeader("Content-Type", "application/json");
          res.end(JSON.stringify({ error: "Invalid request body" }));
        }
      });

      server.middlewares.use("/auth/logout", (req, res) => {
        if (req.method !== "POST") {
          res.statusCode = 405;
          res.end(JSON.stringify({ error: "Method not allowed" }));
          return;
        }

        const authHeader = req.headers.authorization;
        if (authHeader?.startsWith("Bearer ")) {
          tokens.delete(authHeader.slice(7));
        }

        res.setHeader("Content-Type", "application/json");
        res.end(JSON.stringify({ success: true }));
      });
    },
  };
}
