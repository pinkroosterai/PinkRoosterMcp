import { useState } from "react";
import { Brain, Trash2, Plus, Search, Tag, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useMemories, useMemory, useUpsertMemory, useDeleteMemory } from "@/hooks/use-memories";
import { toast } from "sonner";

interface MemoryPanelProps {
  projectId: number;
}

export function MemoryPanel({ projectId }: MemoryPanelProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [tagFilter, setTagFilter] = useState<string>();
  const [selectedMemoryNumber, setSelectedMemoryNumber] = useState<number>();
  const [showCreate, setShowCreate] = useState(false);

  const { data: memories = [] } = useMemories(projectId, search || undefined, tagFilter);

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8">
                <Brain className="h-4 w-4" />
              </Button>
            </SheetTrigger>
          </TooltipTrigger>
          <TooltipContent side="bottom">
            <p className="text-xs">Project Memories</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
      <SheetContent className="flex w-[420px] flex-col gap-0 sm:max-w-[420px]">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Brain className="h-5 w-5" />
            Project Memories
          </SheetTitle>
        </SheetHeader>

        {selectedMemoryNumber ? (
          <MemoryDetail
            projectId={projectId}
            memoryNumber={selectedMemoryNumber}
            onBack={() => setSelectedMemoryNumber(undefined)}
          />
        ) : showCreate ? (
          <MemoryCreate
            projectId={projectId}
            onClose={() => setShowCreate(false)}
          />
        ) : (
          <MemoryList
            memories={memories}
            search={search}
            tagFilter={tagFilter}
            onSearchChange={setSearch}
            onTagFilterChange={setTagFilter}
            onSelect={setSelectedMemoryNumber}
            onCreateNew={() => setShowCreate(true)}
          />
        )}
      </SheetContent>
    </Sheet>
  );
}

function MemoryList({
  memories,
  search,
  tagFilter,
  onSearchChange,
  onTagFilterChange,
  onSelect,
  onCreateNew,
}: {
  memories: { memoryId: string; name: string; tags: string[]; updatedAt: string }[];
  search: string;
  tagFilter: string | undefined;
  onSearchChange: (v: string) => void;
  onTagFilterChange: (v: string | undefined) => void;
  onSelect: (memoryNumber: number) => void;
  onCreateNew: () => void;
}) {
  return (
    <div className="flex flex-1 flex-col gap-3 overflow-hidden">
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search memories..."
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            className="pl-9"
          />
        </div>
        <Button size="icon" variant="outline" onClick={onCreateNew}>
          <Plus className="h-4 w-4" />
        </Button>
      </div>

      {tagFilter && (
        <div className="flex items-center gap-2">
          <Tag className="h-3 w-3 text-muted-foreground" />
          <Badge variant="secondary" className="cursor-pointer" onClick={() => onTagFilterChange(undefined)}>
            {tagFilter} &times;
          </Badge>
        </div>
      )}

      <div className="flex-1 space-y-2 overflow-y-auto">
        {memories.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">
            No memories found.
          </p>
        ) : (
          memories.map((m) => {
            const num = parseInt(m.memoryId.split("-mem-")[1]);
            return (
              <button
                key={m.memoryId}
                onClick={() => onSelect(num)}
                className="w-full rounded-lg border p-3 text-left transition-colors hover:bg-muted/50"
              >
                <div className="flex items-start justify-between gap-2">
                  <span className="text-sm font-medium leading-tight">{m.name}</span>
                  <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground">
                    <Clock className="h-3 w-3" />
                    {new Date(m.updatedAt).toLocaleDateString()}
                  </span>
                </div>
                {m.tags.length > 0 && (
                  <div className="mt-1.5 flex flex-wrap gap-1">
                    {m.tags.map((tag) => (
                      <Badge
                        key={tag}
                        variant="outline"
                        className="cursor-pointer text-xs"
                        onClick={(e) => {
                          e.stopPropagation();
                          onTagFilterChange(tag);
                        }}
                      >
                        {tag}
                      </Badge>
                    ))}
                  </div>
                )}
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}

function MemoryDetail({
  projectId,
  memoryNumber,
  onBack,
}: {
  projectId: number;
  memoryNumber: number;
  onBack: () => void;
}) {
  const { data: memory, isLoading } = useMemory(projectId, memoryNumber);
  const deleteMutation = useDeleteMemory();

  const handleDelete = async () => {
    if (!confirm("Delete this memory? This cannot be undone.")) return;
    try {
      await deleteMutation.mutateAsync({ projectId, memoryNumber });
      toast.success("Memory deleted");
      onBack();
    } catch {
      toast.error("Failed to delete memory");
    }
  };

  if (isLoading) {
    return <p className="py-8 text-center text-sm text-muted-foreground">Loading...</p>;
  }

  if (!memory) {
    return <p className="py-8 text-center text-sm text-muted-foreground">Memory not found.</p>;
  }

  return (
    <div className="flex flex-1 flex-col gap-4 overflow-hidden">
      <div className="flex items-center justify-between">
        <Button variant="ghost" size="sm" onClick={onBack}>
          &larr; Back
        </Button>
        <Button variant="ghost" size="icon" className="h-8 w-8 text-destructive" onClick={handleDelete}>
          <Trash2 className="h-4 w-4" />
        </Button>
      </div>

      <div>
        <h3 className="text-base font-semibold">{memory.name}</h3>
        <p className="text-xs text-muted-foreground">
          {memory.memoryId} &middot; Updated {new Date(memory.updatedAt).toLocaleString()}
        </p>
      </div>

      {memory.tags.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {memory.tags.map((tag) => (
            <Badge key={tag} variant="outline" className="text-xs">
              {tag}
            </Badge>
          ))}
        </div>
      )}

      <div className="flex-1 overflow-y-auto rounded-md border bg-muted/30 p-3">
        <pre className="whitespace-pre-wrap text-sm">{memory.content}</pre>
      </div>
    </div>
  );
}

function MemoryCreate({
  projectId,
  onClose,
}: {
  projectId: number;
  onClose: () => void;
}) {
  const [name, setName] = useState("");
  const [content, setContent] = useState("");
  const [tagsInput, setTagsInput] = useState("");
  const upsertMutation = useUpsertMemory();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !content.trim()) return;

    const tags = tagsInput
      .split(",")
      .map((t) => t.trim())
      .filter(Boolean);

    try {
      const result = await upsertMutation.mutateAsync({
        projectId,
        data: { name: name.trim(), content: content.trim(), tags: tags.length > 0 ? tags : undefined },
      });
      toast.success(result.wasMerged ? "Content merged into existing memory" : "Memory created");
      onClose();
    } catch {
      toast.error("Failed to save memory");
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-1 flex-col gap-4">
      <div className="flex items-center justify-between">
        <Button type="button" variant="ghost" size="sm" onClick={onClose}>
          &larr; Back
        </Button>
        <span className="text-sm font-medium">New Memory</span>
      </div>

      <Input
        placeholder="Memory name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        required
      />

      <Textarea
        placeholder="Content (markdown supported)..."
        value={content}
        onChange={(e) => setContent(e.target.value)}
        className="min-h-[200px] flex-1"
        required
      />

      <Input
        placeholder="Tags (comma-separated)"
        value={tagsInput}
        onChange={(e) => setTagsInput(e.target.value)}
      />

      <Button type="submit" disabled={upsertMutation.isPending}>
        {upsertMutation.isPending ? "Saving..." : "Save Memory"}
      </Button>
    </form>
  );
}
