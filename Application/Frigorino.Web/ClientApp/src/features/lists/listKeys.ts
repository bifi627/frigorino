export const listKeys = {
    all: ["lists"] as const,
    byHousehold: (householdId: number) =>
        [...listKeys.all, "household", householdId] as const,
    detail: (listId: number) => [...listKeys.all, "detail", listId] as const,
} as const;
