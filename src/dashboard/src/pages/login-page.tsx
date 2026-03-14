import { useState, useEffect, lazy, Suspense, type FormEvent } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/components/auth-provider";
import { registerSchema, type RegisterInput } from "@/lib/schemas";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";

const PasswordStrengthBar = lazy(() => import("react-password-strength-bar"));

export function LoginPage() {
  const { isProtected, login, register } = useAuth();

  const [mode, setMode] = useState<"login" | "register">("login");

  // Sync mode with isProtected (may update after async auth check)
  useEffect(() => {
    setMode(isProtected ? "login" : "register");
  }, [isProtected]);

  // ── Login form (simple, no Zod needed) ──
  const [loginEmail, setLoginEmail] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [loginLoading, setLoginLoading] = useState(false);

  async function handleLogin(e: FormEvent) {
    e.preventDefault();
    setLoginError(null);
    setLoginLoading(true);

    const err = await login(loginEmail, loginPassword);
    if (err) {
      setLoginError(err);
      setLoginLoading(false);
    }
  }

  // ── Register form (react-hook-form + Zod) ──
  const regForm = useForm<RegisterInput>({
    resolver: zodResolver(registerSchema),
    defaultValues: { email: "", password: "", confirmPassword: "", displayName: "" },
    mode: "onTouched",
  });

  const [regServerError, setRegServerError] = useState<string | null>(null);

  async function onRegister(data: RegisterInput) {
    setRegServerError(null);
    const err = await register(data.email, data.password, data.displayName);
    if (err) {
      setRegServerError(err);
    }
  }

  const passwordValue = regForm.watch("password");

  if (mode === "register") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background bg-[radial-gradient(ellipse_at_top,hsl(350_80%_55%/0.08),transparent_60%)] p-4">
        <Card className="glass-card w-full max-w-sm">
          <CardHeader className="text-center">
            <img src="/logo_transparent.png" alt="PinkRoosterMCP" className="mx-auto mb-2 h-32 w-32" />
            <CardTitle className="text-xl">PinkRoosterMCP</CardTitle>
            <p className="text-sm text-muted-foreground">
              {isProtected
                ? "Create a new account"
                : "Create the first admin account"}
            </p>
          </CardHeader>
          <CardContent>
            <Form {...regForm}>
              <form onSubmit={regForm.handleSubmit(onRegister)} className="space-y-4">
                <FormField
                  control={regForm.control}
                  name="displayName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Display Name</FormLabel>
                      <FormControl>
                        <Input autoComplete="name" autoFocus {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={regForm.control}
                  name="email"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Email</FormLabel>
                      <FormControl>
                        <Input type="email" autoComplete="email" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={regForm.control}
                  name="password"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Password</FormLabel>
                      <FormControl>
                        <Input type="password" autoComplete="new-password" {...field} />
                      </FormControl>
                      <Suspense fallback={null}>
                        <PasswordStrengthBar password={passwordValue} minLength={8} />
                      </Suspense>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={regForm.control}
                  name="confirmPassword"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Confirm Password</FormLabel>
                      <FormControl>
                        <Input type="password" autoComplete="new-password" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                {regServerError && (
                  <p className="text-sm text-destructive">{regServerError}</p>
                )}
                <Button type="submit" className="w-full" disabled={regForm.formState.isSubmitting}>
                  {regForm.formState.isSubmitting
                    ? "Creating account..."
                    : isProtected
                      ? "Create Account"
                      : "Create Admin Account"}
                </Button>
                {isProtected && (
                  <p className="text-center text-sm text-muted-foreground">
                    Already have an account?{" "}
                    <button
                      type="button"
                      onClick={() => {
                        setMode("login");
                        setRegServerError(null);
                      }}
                      className="text-primary underline-offset-4 hover:underline"
                    >
                      Sign in
                    </button>
                  </p>
                )}
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background bg-[radial-gradient(ellipse_at_top,hsl(350_80%_55%/0.08),transparent_60%)] p-4">
      <Card className="glass-card w-full max-w-sm">
        <CardHeader className="text-center">
          <img src="/logo_transparent.png" alt="PinkRoosterMCP" className="mx-auto mb-2 h-32 w-32" />
          <CardTitle className="text-xl">PinkRoosterMCP</CardTitle>
          <p className="text-sm text-muted-foreground">
            Sign in to access the dashboard
          </p>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleLogin} className="space-y-4">
            <div className="space-y-2">
              <label
                htmlFor="email"
                className="text-sm font-medium leading-none"
              >
                Email
              </label>
              <Input
                id="email"
                type="email"
                value={loginEmail}
                onChange={(e) => setLoginEmail(e.target.value)}
                autoComplete="email"
                autoFocus
                required
              />
            </div>
            <div className="space-y-2">
              <label
                htmlFor="password"
                className="text-sm font-medium leading-none"
              >
                Password
              </label>
              <Input
                id="password"
                type="password"
                value={loginPassword}
                onChange={(e) => setLoginPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </div>
            {loginError && (
              <p className="text-sm text-destructive">{loginError}</p>
            )}
            <Button type="submit" className="w-full" disabled={loginLoading}>
              {loginLoading ? "Signing in..." : "Sign in"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
