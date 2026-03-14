import { useState, lazy, Suspense, type FormEvent } from "react";
import { toast } from "sonner";
import { User, Lock } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useAuth } from "@/components/auth-provider";
import { PageTransition } from "@/components/page-transition";
import { updateProfile, changePassword } from "@/api/auth";
import { changePasswordSchema, type ChangePasswordInput } from "@/lib/schemas";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";

const PasswordStrengthBar = lazy(() => import("react-password-strength-bar"));

export function ProfilePage() {
  const { user } = useAuth();

  // ── Profile form (keeps manual state for email change password requirement) ──
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [email, setEmail] = useState(user?.email ?? "");
  const [profilePassword, setProfilePassword] = useState("");
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);

  const emailChanged = email !== (user?.email ?? "");

  async function handleProfileSubmit(e: FormEvent) {
    e.preventDefault();
    setProfileError(null);

    if (emailChanged && !profilePassword) {
      setProfileError("Current password is required to change email");
      return;
    }

    setProfileLoading(true);
    try {
      await updateProfile({
        displayName,
        email: emailChanged ? email : undefined,
        currentPassword: emailChanged ? profilePassword : undefined,
      });
      setProfilePassword("");
      toast.success("Profile updated");
    } catch (err) {
      setProfileError(err instanceof Error ? err.message : "Failed to update profile");
    } finally {
      setProfileLoading(false);
    }
  }

  // ── Change password form (react-hook-form + Zod) ──
  const pwForm = useForm<ChangePasswordInput>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
    mode: "onTouched",
  });

  const [pwServerError, setPwServerError] = useState<string | null>(null);

  async function onPasswordSubmit(data: ChangePasswordInput) {
    setPwServerError(null);
    try {
      await changePassword({ currentPassword: data.currentPassword, newPassword: data.newPassword });
      toast.success("Password changed successfully");
      pwForm.reset();
    } catch (err) {
      setPwServerError(err instanceof Error ? err.message : "Failed to change password");
    }
  }

  const newPasswordValue = pwForm.watch("newPassword");

  if (!user) return null;

  return (
    <PageTransition>
    <div className="space-y-6 max-w-lg">
      <h1 className="text-2xl font-bold flex items-center gap-2 animate-in-right">
        <User className="size-6" /> Profile
      </h1>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            Account Details
            <Badge variant="outline">{user.globalRole}</Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleProfileSubmit} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="displayName" className="text-sm font-medium">Display Name</label>
              <Input
                id="displayName"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <label htmlFor="email" className="text-sm font-medium">Email</label>
              <Input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>
            {emailChanged && (
              <div className="space-y-2">
                <label htmlFor="profilePassword" className="text-sm font-medium">
                  Current Password <span className="text-muted-foreground">(required to change email)</span>
                </label>
                <Input
                  id="profilePassword"
                  type="password"
                  value={profilePassword}
                  onChange={(e) => setProfilePassword(e.target.value)}
                  required
                />
              </div>
            )}
            {profileError && <p className="text-sm text-destructive">{profileError}</p>}
            <Button type="submit" disabled={profileLoading}>
              {profileLoading ? "Saving..." : "Save Changes"}
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Lock className="size-5" /> Change Password
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Form {...pwForm}>
            <form onSubmit={pwForm.handleSubmit(onPasswordSubmit)} className="space-y-4">
              <FormField
                control={pwForm.control}
                name="currentPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Current Password</FormLabel>
                    <FormControl>
                      <Input type="password" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={pwForm.control}
                name="newPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>New Password</FormLabel>
                    <FormControl>
                      <Input type="password" {...field} />
                    </FormControl>
                    <Suspense fallback={null}>
                      <PasswordStrengthBar password={newPasswordValue} minLength={8} />
                    </Suspense>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={pwForm.control}
                name="confirmPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Confirm New Password</FormLabel>
                    <FormControl>
                      <Input type="password" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              {pwServerError && <p className="text-sm text-destructive">{pwServerError}</p>}
              <Button type="submit" disabled={pwForm.formState.isSubmitting}>
                {pwForm.formState.isSubmitting ? "Changing..." : "Change Password"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
    </PageTransition>
  );
}
