import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import {
  checkAuthConfig,
  getCurrentUser,
  login as apiLogin,
  logout as apiLogout,
  register as apiRegister,
  type AuthUser,
} from "@/api/auth";

interface AuthContextValue {
  isProtected: boolean;
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  login: (email: string, password: string) => Promise<string | null>;
  logout: () => Promise<void>;
  register: (
    email: string,
    password: string,
    displayName: string,
  ) => Promise<string | null>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isProtected, setIsProtected] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [user, setUser] = useState<AuthUser | null>(null);

  const checkAuth = useCallback(async () => {
    try {
      const config = await checkAuthConfig();
      setIsProtected(config.isProtected);

      if (config.isProtected) {
        // Check if we have a valid session
        const currentUser = await getCurrentUser();
        if (currentUser) {
          setUser(currentUser);
          setIsAuthenticated(true);
        } else {
          setUser(null);
          setIsAuthenticated(false);
        }
      } else {
        // No users yet — show registration
        setUser(null);
        setIsAuthenticated(false);
      }
    } catch {
      // API not available — treat as unprotected
      setIsProtected(false);
      setIsAuthenticated(false);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  const login = useCallback(
    async (email: string, password: string): Promise<string | null> => {
      try {
        const response = await apiLogin(email, password);
        setUser(response.user);
        setIsAuthenticated(true);
        setIsProtected(true);
        return null;
      } catch (err) {
        return err instanceof Error ? err.message : "Login failed";
      }
    },
    [],
  );

  const register = useCallback(
    async (
      email: string,
      password: string,
      displayName: string,
    ): Promise<string | null> => {
      try {
        await apiRegister(email, password, displayName);
        // Auto-login after registration
        const response = await apiLogin(email, password);
        setUser(response.user);
        setIsAuthenticated(true);
        setIsProtected(true);
        return null;
      } catch (err) {
        return err instanceof Error ? err.message : "Registration failed";
      }
    },
    [],
  );

  const logout = useCallback(async () => {
    try {
      await apiLogout();
    } catch {
      // Ignore logout errors
    }
    setUser(null);
    setIsAuthenticated(false);
  }, []);

  return (
    <AuthContext.Provider
      value={{
        isProtected,
        isAuthenticated,
        isLoading,
        user,
        login,
        logout,
        register,
      }}
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
