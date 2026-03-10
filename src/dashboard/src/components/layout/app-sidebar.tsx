import { LayoutDashboard, ScrollText, FolderOpen, Bug, Layers } from "lucide-react";
import { Link, useLocation } from "react-router";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarSeparator,
} from "@/components/ui/sidebar";
import { ProjectSwitcher } from "./project-switcher";
import { useProjectContext } from "@/hooks/use-project-context";

const navItems = [
  { title: "Dashboard", href: "/", icon: LayoutDashboard },
  { title: "Activity Log", href: "/activity", icon: ScrollText },
];

const bottomItems = [
  { title: "All Projects", href: "/projects", icon: FolderOpen },
];

export function AppSidebar() {
  const location = useLocation();
  const { selectedProject } = useProjectContext();

  return (
    <Sidebar>
      <SidebarHeader className="border-b p-2">
        <ProjectSwitcher />
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Navigation</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {navItems.map((item) => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    asChild
                    isActive={location.pathname === item.href}
                  >
                    <Link to={item.href}>
                      <item.icon />
                      <span>{item.title}</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
              {selectedProject && (
                <>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      asChild
                      isActive={location.pathname === `/projects/${selectedProject.id}`}
                    >
                      <Link to={`/projects/${selectedProject.id}`}>
                        <Bug />
                        <span>Issues</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      asChild
                      isActive={location.pathname === `/projects/${selectedProject.id}`}
                    >
                      <Link to={`/projects/${selectedProject.id}`}>
                        <Layers />
                        <span>Work Packages</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </>
              )}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
        <SidebarSeparator />
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {bottomItems.map((item) => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    asChild
                    isActive={location.pathname === item.href}
                  >
                    <Link to={item.href}>
                      <item.icon />
                      <span>{item.title}</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  );
}
