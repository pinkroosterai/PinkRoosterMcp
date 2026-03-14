import { Paperclip } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { FileReference } from "@/types";

interface AttachmentsTableProps {
  attachments: FileReference[];
}

export function AttachmentsTable({ attachments }: AttachmentsTableProps) {
  if (attachments.length === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base flex items-center gap-2.5">
          <Paperclip className="size-4" /> Attachments
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>File</TableHead>
                <TableHead>Path</TableHead>
                <TableHead>Description</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {attachments.map((att, idx) => (
                <TableRow key={idx}>
                  <TableCell className="font-mono text-sm">{att.fileName}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">{att.relativePath}</TableCell>
                  <TableCell className="text-sm">{att.description ?? "\u2014"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  );
}
