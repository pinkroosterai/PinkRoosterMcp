export function formatDate(value: string | null | undefined): string {
  if (!value) return "\u2014";
  return new Date(value).toLocaleString();
}

export function computeDiff<T extends Record<string, unknown>>(
  original: T,
  current: T,
): Record<string, unknown> {
  const diff: Record<string, unknown> = {};
  for (const key of Object.keys(current) as (keyof T)[]) {
    const curr = current[key];
    const orig = original[key];
    if (curr !== orig && curr !== undefined) {
      if (curr === "" && (orig === "" || orig === undefined)) continue;
      diff[key as string] = curr === "" ? null : curr;
    }
  }
  return diff;
}
