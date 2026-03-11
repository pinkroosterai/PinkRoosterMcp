import { Outlet, useLocation } from "react-router";
import { SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { AppSidebar } from "./app-sidebar";
import { Separator } from "@/components/ui/separator";
import { ThemeToggle } from "@/components/theme-toggle";
import { Toaster } from "@/components/ui/sonner";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useServerEvents, type ConnectionState } from "@/hooks/use-server-events";

const connectionColors: Record<ConnectionState, string> = {
  connected: "bg-emerald-500",
  reconnecting: "bg-yellow-500 animate-pulse",
  disconnected: "bg-red-500",
};

function useProjectIdFromUrl(): number | undefined {
  const { pathname } = useLocation();
  const match = pathname.match(/^\/projects\/(\d+)/);
  return match ? Number(match[1]) : undefined;
}

export function AppLayout() {
  const projectId = useProjectIdFromUrl();
  const { connectionState } = useServerEvents(projectId);

  return (
    <>
      <SidebarProvider>
        <AppSidebar />
        <main className="flex flex-1 flex-col">
          <header className="flex h-14 items-center gap-3 border-b px-5">
            <SidebarTrigger />
            <Separator orientation="vertical" className="h-5" />
            <span className="text-sm text-muted-foreground">Dashboard</span>
            {projectId && (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span
                      className={`ml-1 inline-block h-1.5 w-1.5 rounded-full ${connectionColors[connectionState]}`}
                    />
                  </TooltipTrigger>
                  <TooltipContent side="bottom">
                    <p className="text-xs capitalize">Live: {connectionState}</p>
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            )}
            <div className="ml-auto">
              <ThemeToggle />
            </div>
          </header>
          <div className="flex-1 overflow-auto p-6">
            <Outlet />
          </div>
        </main>
      </SidebarProvider>
      <Toaster position="bottom-right" richColors />
    </>
  );
}
