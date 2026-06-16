# Performance Optimization

The performance-sensitive surface is the shopping/inventory **list view**: a list can accumulate hundreds or low-thousands of (mostly checked-off) items, rendered on low-spec phones. The optimizations live in `ClientApp/src/components/sortables/` (`SortableList.tsx`, `SortableListItem.tsx`) plus the per-feature optimistic hooks. There is **no virtualization library** — `react-window` and friends are not dependencies; the strategy is to keep the mounted DOM small by construction.

## Ordering is fractional-index, not integer sort

List/inventory items carry a lexicographic **`Rank`** (`Domain/Entities/FractionalIndex.cs`), not an integer `SortOrder`. The server returns items already ordered by `Rank` (+ `Id` tiebreaker), so the client trusts array order — `sortMode="custom"` short-circuits with no client-side sort. A reorder persists a single key minted *between* the new neighbours; there is no bulk re-sequencing and no periodic "compaction" pass (the old integer `SortOrderCalculator` + `/items/compact` endpoints are gone). Optimistic reorders just splice the cached array into the new visual order.

The inventory-only `expiryDateAsc` / `expiryDateDesc` modes do sort client-side by date (null dates last); `custom` does not.

## Bounded DOM for checked items

`SortableList` splits items into an unchecked (draggable) section and a checked (archive) section, and renders them differently:

- **Checked items are paginated** — only the first `CHECKED_PAGE_SIZE` (25) mount; a "Show more" button grows the window by 25 (`visibleCheckedCount`), resetting on remount. This is the real defense against ~1000-item lists.
- **Checked items render as plain rows** — no `useSortable` / `SortableContext` registration (they're never reordered), which cuts both DOM node count and dnd-kit's per-item bookkeeping.
- Only the unchecked section is wrapped in `SortableContext`, with a memoized id array.

## Drag interaction

- **Sensors** — `PointerSensor` with an 8px activation distance and `TouchSensor` with a 200ms press delay + 5px tolerance, so scrolling never starts a drag and touch drags are reliable.
- **Live drag order** — `onDragOver` reorders a local `dragOrder` array (`arrayMove`) so rows shift symmetrically under the pointer; an `isDraggingRef` guard stops a mid-drag refetch (e.g. a previous reorder's debounced invalidation) from yanking the order away. On drop the order is held until the optimistic update resyncs — no snap-back flash.
- The `DragOverlay` clones the dragged row with a left gutter matching the handle width, so the content stays visible above the finger on touch.

## Memoization (React Compiler + manual)

The build runs the **React Compiler** — `babel-plugin-react-compiler` via `@rolldown/plugin-babel` (`reactCompilerPreset()` in `vite.config.ts`), targeting React 19 (its native runtime). It auto-memoizes components and hooks at compile time, so **new code rarely needs hand-rolled `memo`/`useMemo`/`useCallback`**; opt a single file out with the `"use no memo"` directive if the compiler ever misbehaves.

The hot list components still carry explicit memoization (predating, and belt-and-suspenders with, the compiler): `SortableListItem` is `memo()`-wrapped; `SortableList` memoizes the unchecked/checked split, the `SortableContext` id array, and every handler. These are load-bearing for the drag path's referential stability — keep them.

## Server state

Reads go through TanStack Query with a per-hook `staleTime`. Mutation hooks patch the cache optimistically and coalesce refetches with a debounced invalidation helper, so a burst of toggles/reorders doesn't fan out into one request per change. Hook conventions: `API_Integration.md`.

## Backend note

`Frigorino.Web.csproj` sets `PublishReadyToRun`, so published IL is pre-JIT'd at the Dockerfile's `linux-x64` publish — first-request latency after a Railway cold start doesn't pay tier-1 JIT. (Only active when publish supplies a RID; local `dotnet run` is unaffected.)
