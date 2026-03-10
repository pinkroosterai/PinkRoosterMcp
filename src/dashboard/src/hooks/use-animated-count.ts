import { useEffect, useRef, useState } from "react";

/**
 * Animates a number from 0 to the target value over a given duration.
 * Returns the current animated value.
 */
export function useAnimatedCount(target: number, duration = 600): number {
  const [value, setValue] = useState(0);
  const prevTarget = useRef(0);
  const rafId = useRef(0);

  useEffect(() => {
    const from = prevTarget.current;
    prevTarget.current = target;

    if (from === target) {
      setValue(target);
      return;
    }

    const start = performance.now();
    const diff = target - from;

    function tick(now: number) {
      const elapsed = now - start;
      const progress = Math.min(elapsed / duration, 1);
      // ease-out quad
      const eased = 1 - (1 - progress) * (1 - progress);
      setValue(Math.round(from + diff * eased));
      if (progress < 1) {
        rafId.current = requestAnimationFrame(tick);
      }
    }

    rafId.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafId.current);
  }, [target, duration]);

  return value;
}
