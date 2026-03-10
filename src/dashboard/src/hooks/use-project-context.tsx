import { createContext, useContext, useState, useEffect, type ReactNode } from "react";
import type { Project } from "@/types";
import { useProjects } from "./use-projects";

interface ProjectContextValue {
  selectedProject: Project | null;
  setSelectedProject: (project: Project) => void;
  clearSelectedProject: () => void;
}

const ProjectContext = createContext<ProjectContextValue | null>(null);

const STORAGE_KEY = "pinkrooster-selected-project-id";

export function ProjectProvider({ children }: { children: ReactNode }) {
  const [selectedProject, setSelectedProjectState] = useState<Project | null>(null);
  const { data: projects } = useProjects();

  useEffect(() => {
    if (!projects?.length) return;

    const storedId = localStorage.getItem(STORAGE_KEY);
    if (storedId && !selectedProject) {
      const found = projects.find((p) => p.id === Number(storedId));
      if (found) setSelectedProjectState(found);
    }
  }, [projects, selectedProject]);

  const setSelectedProject = (project: Project) => {
    setSelectedProjectState(project);
    localStorage.setItem(STORAGE_KEY, String(project.id));
  };

  const clearSelectedProject = () => {
    setSelectedProjectState(null);
    localStorage.removeItem(STORAGE_KEY);
  };

  return (
    <ProjectContext value={{ selectedProject, setSelectedProject, clearSelectedProject }}>
      {children}
    </ProjectContext>
  );
}

export function useProjectContext() {
  const context = useContext(ProjectContext);
  if (!context) {
    throw new Error("useProjectContext must be used within a ProjectProvider");
  }
  return context;
}
