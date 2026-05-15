export const householdKeys = {
    all: ["households"] as const,
    lists: () => [...householdKeys.all, "list"] as const,
    current: () => ["currentHousehold"] as const,
    members: (householdId: number) =>
        [...householdKeys.all, "members", householdId] as const,
} as const;
