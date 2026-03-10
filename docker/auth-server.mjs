import { createServer } from "http";
import { randomUUID } from "crypto";

const PORT = 3001;
const TOKEN_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours
const tokens = new Map();

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

    try {
      const body = JSON.parse(await readBody(req));
      const { username, password } = body;

      if (
        username === process.env.DASHBOARD_USER &&
        password === process.env.DASHBOARD_PASSWORD
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
