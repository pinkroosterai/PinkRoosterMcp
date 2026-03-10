import { useAnimatedCount } from "@/hooks/use-animated-count";

interface AnimatedCountProps {
  value: number;
  duration?: number;
  suffix?: string;
  className?: string;
}

export function AnimatedCount({ value, duration = 600, suffix, className }: AnimatedCountProps) {
  const animated = useAnimatedCount(value, duration);
  return (
    <span className={className}>
      {animated}
      {suffix}
    </span>
  );
}
