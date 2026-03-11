import { useState, type ComponentProps } from "react";
import { Badge } from "@/components/ui/badge";
import { usePreviousValue } from "@/hooks/use-previous-value";
import { cn } from "@/lib/utils";

interface AnimatedBadgeProps extends ComponentProps<typeof Badge> {
  value: string;
  glowColor?: string;
}

export function AnimatedBadge({
  value,
  glowColor,
  className,
  ...props
}: AnimatedBadgeProps) {
  const previousValue = usePreviousValue(value);
  const [isAnimating, setIsAnimating] = useState(false);

  const shouldAnimate =
    previousValue !== undefined && previousValue !== value && !isAnimating;

  if (shouldAnimate) {
    setIsAnimating(true);
  }

  return (
    <Badge
      className={cn(isAnimating && "animate-badge-pulse", className)}
      style={glowColor ? { "--glow-color": glowColor } as React.CSSProperties : undefined}
      onAnimationEnd={() => setIsAnimating(false)}
      {...props}
    />
  );
}
