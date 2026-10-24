import type { FeedItemDto } from "./types/api";

export type FeedFilter = "all" | "risk" | "opportunity";

export function matchesFilter(item: FeedItemDto, filter: FeedFilter): boolean {
  return filter === "all" || item.direction === filter;
}