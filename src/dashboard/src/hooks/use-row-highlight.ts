import { useRef, useCallback } from "react";

/**
 * Tracks which rows have been recently updated (by comparing updatedAt timestamps)
 * and returns a rowClassName callback that applies the animate-row-highlight class.
 */
export function useRowHighlight<T extends { updatedAt: string }>(
  data: T[],
  getKey: (item: T) => string,
) {
  const prevTimestamps = useRef<Map<string, string>>(new Map());

  const changedKeys = new Set<string>();
  const currentMap = new Map<string, string>();

  for (const item of data) {
    const key = getKey(item);
    const prev = prevTimestamps.current.get(key);
    currentMap.set(key, item.updatedAt);
    if (prev && prev !== item.updatedAt) {
      changedKeys.add(key);
    }
  }

  // Update ref after diffing
  prevTimestamps.current = currentMap;

  const rowClassName = useCallback(
    (item: T): string => {
      return changedKeys.has(getKey(item)) ? "animate-row-highlight" : "";
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [changedKeys],
  );

  return { rowClassName };
}
