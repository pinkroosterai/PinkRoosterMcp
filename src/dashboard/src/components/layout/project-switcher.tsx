import { ChevronsUpDown, FolderOpen } from "lucide-react";
import { useNavigate } from "react-router";
import { useProjects } from "@/hooks/use-projects";
import { useProjectContext } from "@/hooks/use-project-context";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SidebarMenuButton } from "@/components/ui/sidebar";
import { Badge } from "@/components/ui/badge";
import { Link } from "react-router";

export function ProjectSwitcher() {
  const { data: projects } = useProjects();
  const { selectedProject, setSelectedProject } = useProjectContext();
  const navigate = useNavigate();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <SidebarMenuButton
          size="lg"
          className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
        >
          <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
            <FolderOpen className="size-4" />
          </div>
          <div className="grid flex-1 text-left text-sm leading-tight">
            <span className="truncate font-semibold">
              {selectedProject?.name ?? "Select a project"}
            </span>
            <span className="truncate text-xs text-muted-foreground">
              {selectedProject?.projectPath ?? "No project selected"}
            </span>
          </div>
          <ChevronsUpDown className="ml-auto" />
        </SidebarMenuButton>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="w-[--radix-dropdown-menu-trigger-width] min-w-56"
        align="start"
        sideOffset={4}
      >
        <DropdownMenuLabel className="text-xs text-muted-foreground">
          Projects
        </DropdownMenuLabel>
        {projects?.length ? (
          projects.map((project) => (
            <DropdownMenuItem
              key={project.id}
              onClick={() => {
                setSelectedProject(project);
                navigate("/");
              }}
              className="gap-2 p-2"
            >
              <div className="flex flex-1 flex-col gap-0.5">
                <div className="flex items-center gap-2">
                  <span className="font-medium">{project.name}</span>
                  <Badge variant="outline" className="text-[10px] px-1 py-0">
                    {project.projectId}
                  </Badge>
                </div>
                <span className="truncate text-xs text-muted-foreground">
                  {project.projectPath}
                </span>
              </div>
            </DropdownMenuItem>
          ))
        ) : (
          <div className="px-2 py-4 text-center text-sm text-muted-foreground">
            No projects yet
          </div>
        )}
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/projects" className="gap-2 p-2">
            <FolderOpen className="size-4" />
            <span>All Projects</span>
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
