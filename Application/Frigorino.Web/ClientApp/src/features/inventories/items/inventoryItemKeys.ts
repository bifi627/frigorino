export const inventoryItemKeys = {
    all: ["inventoryItems"] as const,
    byInventory: (householdId: number, inventoryId: number) =>
        [
            ...inventoryItemKeys.all,
            "household",
            householdId,
            "inventory",
            inventoryId,
        ] as const,
    detail: (itemId: number) =>
        [...inventoryItemKeys.all, "detail", itemId] as const,
} as const;
