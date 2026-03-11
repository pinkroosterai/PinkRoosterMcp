import { useState } from "react";
import { Pencil, Trash2, X, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import type { UserStory } from "@/types";

interface UserStoryCardProps {
  story: UserStory;
  index: number;
  onUpdate: (index: number, story: UserStory) => void;
  onRemove: (index: number) => void;
  disabled?: boolean;
}

export function UserStoryCard({ story, index, onUpdate, onRemove, disabled }: UserStoryCardProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editRole, setEditRole] = useState(story.role);
  const [editGoal, setEditGoal] = useState(story.goal);
  const [editBenefit, setEditBenefit] = useState(story.benefit);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const handleSave = () => {
    if (!editRole.trim() || !editGoal.trim() || !editBenefit.trim()) return;
    onUpdate(index, { role: editRole.trim(), goal: editGoal.trim(), benefit: editBenefit.trim() });
    setIsEditing(false);
  };

  const handleCancel = () => {
    setEditRole(story.role);
    setEditGoal(story.goal);
    setEditBenefit(story.benefit);
    setIsEditing(false);
  };

  const handleEdit = () => {
    setEditRole(story.role);
    setEditGoal(story.goal);
    setEditBenefit(story.benefit);
    setIsEditing(true);
  };

  if (isEditing) {
    return (
      <Card className="border-dashed">
        <CardContent className="pt-4 space-y-3">
          <div className="grid grid-cols-1 gap-3">
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">As a...</label>
              <Input
                value={editRole}
                onChange={(e) => setEditRole(e.target.value)}
                placeholder="user role"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">I want...</label>
              <Input
                value={editGoal}
                onChange={(e) => setEditGoal(e.target.value)}
                placeholder="what the user wants"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">So that...</label>
              <Input
                value={editBenefit}
                onChange={(e) => setEditBenefit(e.target.value)}
                placeholder="the benefit"
              />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Button size="sm" onClick={handleSave} disabled={!editRole.trim() || !editGoal.trim() || !editBenefit.trim()}>
              <Save className="size-3 mr-1" /> Save
            </Button>
            <Button size="sm" variant="outline" onClick={handleCancel}>
              <X className="size-3 mr-1" /> Cancel
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="pt-4">
        <div className="flex items-start justify-between gap-4">
          <p className="text-sm">
            <span className="text-muted-foreground">As a</span>{" "}
            <span className="font-medium">{story.role}</span>
            <span className="text-muted-foreground">, I want</span>{" "}
            <span className="font-medium">{story.goal}</span>
            <span className="text-muted-foreground">, so that</span>{" "}
            <span className="font-medium">{story.benefit}</span>
          </p>
          <div className="flex items-center gap-1 shrink-0">
            <Button variant="ghost" size="icon" className="size-7" onClick={handleEdit} disabled={disabled}>
              <Pencil className="size-3" />
            </Button>
            {showDeleteConfirm ? (
              <div className="flex items-center gap-1">
                <Button
                  variant="destructive"
                  size="sm"
                  className="h-7 text-xs"
                  onClick={() => { onRemove(index); setShowDeleteConfirm(false); }}
                  disabled={disabled}
                >
                  Confirm
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 text-xs"
                  onClick={() => setShowDeleteConfirm(false)}
                >
                  No
                </Button>
              </div>
            ) : (
              <Button
                variant="ghost"
                size="icon"
                className="size-7 text-destructive"
                onClick={() => setShowDeleteConfirm(true)}
                disabled={disabled}
              >
                <Trash2 className="size-3" />
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
