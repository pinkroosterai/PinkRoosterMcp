import { useRef, useCallback, useState, useEffect } from "react";

/**
 * Tracks which rows have been recently updated (by comparing updatedAt timestamps)
 * and returns a rowClassName callback that applies the animate-row-highlight class.
 */
export function useRowHighlight<T extends { updatedAt: string }>(
  data: T[],
  getKey: (item: T) => string,
) {
  const prevTimestamps = useRef<Map<string, string>>(new Map());
  const getKeyRef = useRef(getKey);
  getKeyRef.current = getKey;

  const [changedKeys, setChangedKeys] = useState<Set<string>>(new Set());

  useEffect(() => {
    const keyFn = getKeyRef.current;
    const changed = new Set<string>();
    const currentMap = new Map<string, string>();

    for (const item of data) {
      const key = keyFn(item);
      const prev = prevTimestamps.current.get(key);
      currentMap.set(key, item.updatedAt);
      if (prev && prev !== item.updatedAt) {
        changed.add(key);
      }
    }

    prevTimestamps.current = currentMap;

    if (changed.size > 0) {
      setChangedKeys(changed);
    }
  }, [data]);

  const rowClassName = useCallback(
    (item: T): string => {
      return changedKeys.has(getKeyRef.current(item)) ? "animate-row-highlight" : "";
    },
    [changedKeys],
  );

  return { rowClassName };
}
