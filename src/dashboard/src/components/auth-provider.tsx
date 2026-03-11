import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";

interface AuthContextValue {
  isProtected: boolean;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<string | null>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isProtected, setIsProtected] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  const checkAuth = useCallback(async () => {
    try {
      const res = await fetch("/auth/config", { credentials: "include" });
      if (!res.ok) {
        // Auth endpoint not available — treat as unprotected
        setIsProtected(false);
        setIsAuthenticated(false);
        return;
      }
      const data = await res.json();
      setIsProtected(data.protected);
      setIsAuthenticated(!data.protected || data.authenticated);
    } catch {
      // Network error — treat as unprotected
      setIsProtected(false);
      setIsAuthenticated(false);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  const login = useCallback(
    async (username: string, password: string): Promise<string | null> => {
      try {
        const res = await fetch("/auth/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          credentials: "include",
          body: JSON.stringify({ username, password }),
        });
        const data = await res.json();
        if (!res.ok) {
          return data.error || "Login failed";
        }
        setIsAuthenticated(true);
        return null;
      } catch {
        return "Network error";
      }
    },
    [],
  );

  const logout = useCallback(async () => {
    try {
      await fetch("/auth/logout", {
        method: "POST",
        credentials: "include",
      });
    } catch {
      // Ignore logout errors
    }
    setIsAuthenticated(false);
  }, []);

  return (
    <AuthContext.Provider
      value={{ isProtected, isAuthenticated, isLoading, login, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
