import { Navigate, useParams } from "react-router";

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  return <Navigate to={`/projects/${id}/issues`} replace />;
}
