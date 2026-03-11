import { useState } from "react";
import { Plus, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import type { UserStory } from "@/types";

interface AddUserStoryFormProps {
  onAdd: (story: UserStory) => void;
  disabled?: boolean;
}

export function AddUserStoryForm({ onAdd, disabled }: AddUserStoryFormProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [role, setRole] = useState("");
  const [goal, setGoal] = useState("");
  const [benefit, setBenefit] = useState("");

  const handleSubmit = () => {
    if (!role.trim() || !goal.trim() || !benefit.trim()) return;
    onAdd({ role: role.trim(), goal: goal.trim(), benefit: benefit.trim() });
    setRole("");
    setGoal("");
    setBenefit("");
    setIsOpen(false);
  };

  const handleCancel = () => {
    setRole("");
    setGoal("");
    setBenefit("");
    setIsOpen(false);
  };

  if (!isOpen) {
    return (
      <Button variant="outline" size="sm" onClick={() => setIsOpen(true)} disabled={disabled}>
        <Plus className="size-3 mr-1" /> Add User Story
      </Button>
    );
  }

  return (
    <Card className="border-dashed">
      <CardContent className="pt-4 space-y-3">
        <div className="grid grid-cols-1 gap-3">
          <div>
            <label className="text-xs text-muted-foreground mb-1 block">As a...</label>
            <Input
              value={role}
              onChange={(e) => setRole(e.target.value)}
              placeholder="user role (e.g. developer, project manager)"
              autoFocus
            />
          </div>
          <div>
            <label className="text-xs text-muted-foreground mb-1 block">I want...</label>
            <Input
              value={goal}
              onChange={(e) => setGoal(e.target.value)}
              placeholder="what the user wants to achieve"
            />
          </div>
          <div>
            <label className="text-xs text-muted-foreground mb-1 block">So that...</label>
            <Input
              value={benefit}
              onChange={(e) => setBenefit(e.target.value)}
              placeholder="the benefit or reason"
            />
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button
            size="sm"
            onClick={handleSubmit}
            disabled={!role.trim() || !goal.trim() || !benefit.trim() || disabled}
          >
            <Plus className="size-3 mr-1" /> Add
          </Button>
          <Button size="sm" variant="outline" onClick={handleCancel}>
            <X className="size-3 mr-1" /> Cancel
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
