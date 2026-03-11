import type { Plugin } from "vite";
import { randomUUID, timingSafeEqual, createHmac } from "crypto";

const TOKEN_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours
const TOKEN_TTL_S = 24 * 60 * 60; // 24 hours in seconds
const COOKIE_NAME = "pinkrooster_session";
const RATE_LIMIT_WINDOW_MS = 60 * 1000; // 1 minute
const RATE_LIMIT_MAX_ATTEMPTS = 5;

interface TokenEntry {
  expiresAt: number;
}

interface RateLimitEntry {
  attempts: number;
  windowStart: number;
}

export function authPlugin(): Plugin {
  const tokens = new Map<string, TokenEntry>();
  const loginAttempts = new Map<string, RateLimitEntry>();

  function getClientIp(req: import("http").IncomingMessage): string {
    const forwarded = req.headers["x-forwarded-for"];
    if (typeof forwarded === "string") return forwarded.split(",")[0].trim();
    return req.socket.remoteAddress ?? "unknown";
  }

  function checkRateLimit(ip: string): { allowed: boolean; retryAfter: number } {
    const now = Date.now();
    const entry = loginAttempts.get(ip);
    if (!entry || now - entry.windowStart > RATE_LIMIT_WINDOW_MS) {
      loginAttempts.set(ip, { attempts: 1, windowStart: now });
      return { allowed: true, retryAfter: 0 };
    }
    entry.attempts++;
    if (entry.attempts > RATE_LIMIT_MAX_ATTEMPTS) {
      const retryAfter = Math.ceil((entry.windowStart + RATE_LIMIT_WINDOW_MS - now) / 1000);
      return { allowed: false, retryAfter };
    }
    return { allowed: true, retryAfter: 0 };
  }

  function safeCompare(a: string, b: string): boolean {
    const hmacA = createHmac("sha256", "compare").update(a).digest();
    const hmacB = createHmac("sha256", "compare").update(b).digest();
    return timingSafeEqual(hmacA, hmacB);
  }

  function isProtected(): boolean {
    return !!(process.env.DASHBOARD_USER && process.env.DASHBOARD_PASSWORD);
  }

  function parseCookie(cookieHeader: string | undefined, name: string): string | null {
    if (!cookieHeader) return null;
    const match = cookieHeader.split(";").map((c) => c.trim()).find((c) => c.startsWith(`${name}=`));
    return match ? match.slice(name.length + 1) : null;
  }

  function isValidTokenFromCookie(cookieHeader: string | undefined): boolean {
    const token = parseCookie(cookieHeader, COOKIE_NAME);
    if (!token) return false;
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
          ? isValidTokenFromCookie(req.headers.cookie)
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

        const ip = getClientIp(req);
        const { allowed, retryAfter } = checkRateLimit(ip);
        if (!allowed) {
          res.statusCode = 429;
          res.setHeader("Content-Type", "application/json");
          res.setHeader("Retry-After", String(retryAfter));
          res.end(JSON.stringify({ error: "Too many login attempts. Try again later." }));
          return;
        }

        try {
          const body = JSON.parse(await readBody(req));
          const { username, password } = body;

          if (
            safeCompare(username, process.env.DASHBOARD_USER!) &&
            safeCompare(password, process.env.DASHBOARD_PASSWORD!)
          ) {
            const token = randomUUID();
            tokens.set(token, { expiresAt: Date.now() + TOKEN_TTL_MS });
            res.setHeader("Set-Cookie", `${COOKIE_NAME}=${token}; HttpOnly; SameSite=Strict; Path=/auth; Max-Age=${TOKEN_TTL_S}`);
            res.setHeader("Content-Type", "application/json");
            res.end(JSON.stringify({ success: true }));
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

        const token = parseCookie(req.headers.cookie, COOKIE_NAME);
        if (token) tokens.delete(token);

        res.setHeader("Set-Cookie", `${COOKIE_NAME}=; HttpOnly; SameSite=Strict; Path=/auth; Max-Age=0`);
        res.setHeader("Content-Type", "application/json");
        res.end(JSON.stringify({ success: true }));
      });
    },
  };
}
