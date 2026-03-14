import { useNavigate } from "react-router";
import { Package } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { AnimatedBadge } from "@/components/animated-badge";
import { stateColorClass } from "@/lib/state-colors";
import type { LinkedWorkPackageItem } from "@/types";

interface RelatedWorkPackagesProps {
  items: LinkedWorkPackageItem[];
  projectId: number;
}

export function RelatedWorkPackages({ items, projectId }: RelatedWorkPackagesProps) {
  const navigate = useNavigate();

  if (items.length === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base flex items-center gap-2.5">
          <Package className="size-4" /> Related Work Packages
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>State</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((wp) => {
                const wpNum = wp.workPackageId.split("-wp-")[1];
                return (
                  <TableRow
                    key={wp.workPackageId}
                    className="cursor-pointer hover:bg-accent/50"
                    onClick={() => navigate(`/projects/${projectId}/work-packages/${wpNum}`)}
                  >
                    <TableCell className="font-mono text-sm">{wp.workPackageId}</TableCell>
                    <TableCell className="text-sm">{wp.name}</TableCell>
                    <TableCell><Badge variant="outline">{wp.type}</Badge></TableCell>
                    <TableCell><Badge variant="outline">{wp.priority}</Badge></TableCell>
                    <TableCell>
                      <AnimatedBadge value={wp.state} className={stateColorClass(wp.state)}>{wp.state}</AnimatedBadge>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  );
}
