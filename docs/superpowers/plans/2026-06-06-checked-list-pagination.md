# Checked-List Pagination + Drag Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make ~1000-item lists (≈95% checked) render fast on low-spec devices by paginating the checked section and removing its drag-and-drop wiring.

**Architecture:** Single-component change in the shared `SortableList` (used by both Lists and Inventories). Render only the first 25 checked items with a "Show 25 more" button; render checked rows via the existing non-draggable `dragHandle="none"` path so they need no `SortableContext`/`useSortable`; simplify the drag state to unchecked-only.

**Tech Stack:** React 19, TypeScript, MUI, dnd-kit, i18next, Vite.

**Branch:** `perf/checked-list-pagination` (created off `stage`, in-place — no worktree).

---

## Context

Some users have lists with close to **1000 items, ~95% of them checked**. The shared
`SortableList` component (`src/components/sortables/SortableList.tsx`, used by both
Lists and Inventories) renders **every** item into the DOM — including all ~950 idle
checked rows. Each row is a `SortableListItem` (`ListItemButton` + `Checkbox` +
`ListItemText` + `Menu`) and, for the checked section, also a live `dnd-kit`
`useSortable` registration inside a `SortableContext`. On low-spec devices this is the
dominant render cost, even though the checked pile is rarely looked at.

The active (unchecked) list is small (~50 items) and works fine. This change targets the
checked section only:

1. **Paginate the checked section** — render the first **25** checked items (in their
   current order), with a **"Show 25 more"** button that reveals the next chunk. Worst
   case drops from ~1000 mounted rows to ~75.
2. **Remove drag-and-drop from the checked section** — checked items are never reordered
   (confirmed with user), so they don't need `useSortable`/`SortableContext`. This also
   removes ~950 sortable registrations even when the section is fully expanded.

**Out of scope** (decided with user):
- No virtualization library — collapse + pagination is enough for this workload; revisit only if profiling shows the expanded section is still janky.
- No reordering/sort-behavior change to the checked section — keep existing order, just cap visible count.
- No leaf-component memoization (`TextItemRenderer` etc.) — marginal with only ~75 visible rows.

## Key facts established during exploration

- `SortableItem` (`src/components/common/sortable/SortableItem.tsx:35-37`) already has a
  **non-draggable path**: with `dragHandle="none"` it renders a plain `<Box>` and never
  calls `useSortable`. So rendering checked rows with `showDragHandles={false}` requires
  **no** `SortableContext` — clean removal.
- Drag never crosses sections: status changes via the checkbox, and `handleDragOver`
  already skips cross-section moves (`SortableList.tsx:223`). So the drag state can be
  simplified from `{ unchecked, checked }` to **unchecked-only**.
- `SortableList` is consumed by `ListContainer.tsx:99` and `InventoryContainer.tsx`.
  Both benefit; the change is generic and touches neither container.
- `src/components/common/sortable/SortableList.tsx` appears to be **dead code** (nothing
  imports it; only `SortableItem` from that dir is used). Out of scope — note only.

## Files to modify

- **Modify:** `src/components/sortables/SortableList.tsx` (the only component change)
- **Modify:** `public/locales/en/translation.json` (add one key under `lists`)
- **Modify:** `public/locales/de/translation.json` (add one key under `lists`)

---

## Task 1: Paginate the checked section (cap at 25 + "Show 25 more")

Additive, lowest risk. Checked items stay draggable at this step — only the visible
count is capped.

**File:** `src/components/sortables/SortableList.tsx`

- [ ] **Step 1.1 — Add `Button` to the MUI import** (lines 18-26): add `Button` to the
  `@mui/material` import list.

- [ ] **Step 1.2 — Add the page-size constant** near the top (next to `idStr`, ~line 38):

```tsx
// Checked items are rarely revisited; render them in pages to keep the DOM small.
const CHECKED_PAGE_SIZE = 25;
```

- [ ] **Step 1.3 — Add visible-count state** inside the component (next to `activeItem`,
  ~line 90):

```tsx
const [visibleCheckedCount, setVisibleCheckedCount] = useState(CHECKED_PAGE_SIZE);
```

- [ ] **Step 1.4 — Derive the visible slice** after `displayChecked` is computed (~line 180):

```tsx
const visibleChecked = displayChecked.slice(0, visibleCheckedCount);
const remainingChecked = displayChecked.length - visibleChecked.length;
```

- [ ] **Step 1.5 — Render the slice + "Show more" button.** In the checked section
  (`SortableList.tsx:359-395`), map over `visibleChecked` instead of `displayChecked`,
  and slice `checkedItemIds` accordingly so the `SortableContext` still matches the
  rendered rows (`items={checkedItemIds.slice(0, visibleCheckedCount)}`). After the
  `<List>`, inside the `success.25` `<Box>`, add:

```tsx
{remainingChecked > 0 && (
    <Box sx={{ display: "flex", justifyContent: "center", py: 1 }}>
        <Button
            size="small"
            onClick={() =>
                setVisibleCheckedCount((c) => c + CHECKED_PAGE_SIZE)
            }
            data-testid="show-more-checked-button"
        >
            {t("lists.showMoreChecked", {
                count: Math.min(CHECKED_PAGE_SIZE, remainingChecked),
            })}
        </Button>
    </Box>
)}
```

- [ ] **Step 1.6 — Add i18n key.** `public/locales/en/translation.json` under `lists`:
  `"showMoreChecked": "Show {{count}} more"`. `public/locales/de/translation.json` under
  `lists`: `"showMoreChecked": "{{count}} weitere anzeigen"`.

- [ ] **Step 1.7 — Verify & commit:** `npm run tsc && npm run lint`, then
  `feat(lists): paginate checked items at 25 with show-more`.

---

## Task 2: Remove drag-and-drop from the checked section

Checked rows become plain (non-sortable); drag state simplifies to unchecked-only.

**File:** `src/components/sortables/SortableList.tsx`

- [ ] **Step 2.1 — Simplify `dragOrder` state to a flat unchecked array** (lines 95-98):

```tsx
const [dragOrder, setDragOrder] = useState<T[] | null>(null);
```

- [ ] **Step 2.2 — Update display derivations** (lines 178-180):

```tsx
const displayUnchecked = dragOrder ?? uncheckedItems;
const displayChecked = checkedItems;
```

- [ ] **Step 2.3 — Delete `sectionOf`** (lines 192-198) — no longer needed.

- [ ] **Step 2.4 — Rewrite the three drag handlers** to operate on the single unchecked array:

```tsx
const handleDragStart = useCallback(
    (event: DragStartEvent) => {
        const item = items.find((item) => idStr(item) === event.active.id);
        setActiveItem(item || null);
        setDragOrder(uncheckedItems);
        isDraggingRef.current = true;
    },
    [items, uncheckedItems],
);

const handleDragOver = useCallback((event: DragOverEvent) => {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    setDragOrder((prev) => {
        if (!prev) return prev;
        const from = prev.findIndex((item) => idStr(item) === active.id);
        const to = prev.findIndex((item) => idStr(item) === over.id);
        if (from === -1 || to === -1 || from === to) return prev;
        return arrayMove(prev, from, to);
    });
}, []);

const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
        const { active, over } = event;
        setActiveItem(null);
        isDraggingRef.current = false;
        if (!over || !dragOrder) {
            setDragOrder(null);
            return;
        }
        const index = dragOrder.findIndex((item) => idStr(item) === active.id);
        if (index === -1) {
            setDragOrder(null);
            return;
        }
        const afterId = index > 0 ? Number(dragOrder[index - 1].id) : 0;
        void onReorder(Number(active.id), afterId);
    },
    [dragOrder, onReorder],
);
```

- [ ] **Step 2.5 — Remove `checkedItemIds` memo** (lines 289-292) — now unused.

- [ ] **Step 2.6 — Make checked rows non-draggable.** In the checked section, drop the
  `<SortableContext items={...}>` wrapper entirely (keep the `success.25` `<Box>` and
  `<List>`), and render each `SortableListItem` with `showDragHandles={false}` (forces
  `dragHandle="none"` → plain `<Box>`, no `useSortable`). The unchecked section keeps its
  `SortableContext` and `showDragHandles={showDragHandles}` unchanged.

- [ ] **Step 2.7 — Verify & commit:** `npm run tsc && npm run lint && npm run prettier`,
  then `perf(lists): drop dnd-kit from checked section`.

**Note (expected visual change):** when drag handles are enabled (custom sort, no active
search), unchecked rows show a 48px drag-handle gutter; checked rows will no longer have
it, so checked rows sit slightly further left. Acceptable — checked items are already
de-emphasized (opacity 0.7, green background) in their own section.

---

## Verification

Frontend-only change; the project has **no JS test runner** (per CLAUDE.md), so
verification is static checks + manual browser observation.

1. **Static** (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc`, `npm run lint`,
   `npm run prettier`.

2. **Manual** (the real net — catches DOM/runtime issues static checks miss):
   - Bring up the stack via the `/dev-up` skill; `npm run build` first so the SPA reflects
     the edits (dev/IT serve `ClientApp/build`).
   - In the dev app (authenticated as `dev@frigorino.local`), open or seed a list with
     **>50 checked items** plus a handful of unchecked.
   - Confirm: only **25** checked items render initially; **"Show 25 more"** appears and
     reveals the next 25 per click until exhausted (button then disappears).
   - Confirm: **unchecked** items still drag-reorder correctly (handle visible in custom
     sort); **checked** items show **no** drag handle and cannot be dragged.
   - Confirm: toggling a checked item back to unchecked moves it to the unchecked section,
     and vice versa.
   - Sanity-check the same behavior on an **Inventory** (same shared component).
   - Spot-check perceived render performance with a large checked count.

3. **Regression guard:** run `dotnet test Application/Frigorino.sln` once before finishing
   (covers the Reqnroll/Playwright list scenarios that drive this component).

## Out-of-scope follow-ups (do NOT do now)

- Virtualize the expanded checked list with `@tanstack/react-virtual` — only if manual
  verification shows fully-expanding a huge checked section is still janky.
- Delete dead `src/components/common/sortable/SortableList.tsx` — verify zero imports
  first; separate cleanup.
