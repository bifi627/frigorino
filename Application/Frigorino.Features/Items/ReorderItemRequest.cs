namespace Frigorino.Features.Items
{
    // Shared item-reorder DTO consumed by both ListItem and InventoryItem reorder slices.
    // AfterId == 0 means "move to the top of the current section". An AfterId that doesn't
    // resolve to an active sibling silently falls back to top-of-section — preserves the
    // wire contract the frontend's optimistic UI depends on.
    public sealed record ReorderItemRequest(int AfterId);
}
