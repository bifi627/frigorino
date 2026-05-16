export const inventoryKeys = {
    all: ["inventories"] as const,
    byHousehold: (householdId: number) =>
        [...inventoryKeys.all, "household", householdId] as const,
    detail: (inventoryId: number) =>
        [...inventoryKeys.all, "detail", inventoryId] as const,
} as const;
