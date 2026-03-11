import { createServer } from "http";
import { randomUUID, timingSafeEqual, createHmac } from "crypto";

const PORT = 3001;
const TOKEN_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours
const RATE_LIMIT_WINDOW_MS = 60 * 1000; // 1 minute
const RATE_LIMIT_MAX_ATTEMPTS = 5;
const tokens = new Map();
const loginAttempts = new Map();

function getClientIp(req) {
  const forwarded = req.headers["x-forwarded-for"];
  if (typeof forwarded === "string") return forwarded.split(",")[0].trim();
  return req.socket.remoteAddress ?? "unknown";
}

function checkRateLimit(ip) {
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

function safeCompare(a, b) {
  const hmacA = createHmac("sha256", "compare").update(a).digest();
  const hmacB = createHmac("sha256", "compare").update(b).digest();
  return timingSafeEqual(hmacA, hmacB);
}

function isProtected() {
  return !!(process.env.DASHBOARD_USER && process.env.DASHBOARD_PASSWORD);
}

function isValidToken(authHeader) {
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

function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = "";
    req.on("data", (chunk) => (data += chunk.toString()));
    req.on("end", () => resolve(data));
    req.on("error", reject);
  });
}

function json(res, statusCode, body) {
  res.writeHead(statusCode, { "Content-Type": "application/json" });
  res.end(JSON.stringify(body));
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);

  if (url.pathname === "/auth/config" && req.method === "GET") {
    const prot = isProtected();
    const authenticated = prot
      ? isValidToken(req.headers.authorization)
      : false;
    json(res, 200, { protected: prot, authenticated });
    return;
  }

  if (url.pathname === "/auth/login" && req.method === "POST") {
    if (!isProtected()) {
      json(res, 200, { error: "Authentication is not enabled" });
      return;
    }

    const ip = getClientIp(req);
    const { allowed, retryAfter } = checkRateLimit(ip);
    if (!allowed) {
      res.writeHead(429, {
        "Content-Type": "application/json",
        "Retry-After": String(retryAfter),
      });
      res.end(JSON.stringify({ error: "Too many login attempts. Try again later." }));
      return;
    }

    try {
      const body = JSON.parse(await readBody(req));
      const { username, password } = body;

      if (
        safeCompare(username, process.env.DASHBOARD_USER) &&
        safeCompare(password, process.env.DASHBOARD_PASSWORD)
      ) {
        const token = randomUUID();
        tokens.set(token, { expiresAt: Date.now() + TOKEN_TTL_MS });
        json(res, 200, { token });
      } else {
        json(res, 401, { error: "Invalid credentials" });
      }
    } catch {
      json(res, 400, { error: "Invalid request body" });
    }
    return;
  }

  if (url.pathname === "/auth/logout" && req.method === "POST") {
    const authHeader = req.headers.authorization;
    if (authHeader?.startsWith("Bearer ")) {
      tokens.delete(authHeader.slice(7));
    }
    json(res, 200, { success: true });
    return;
  }

  json(res, 404, { error: "Not found" });
});

server.listen(PORT, () => {
  console.log(`Auth server listening on port ${PORT}`);
});
