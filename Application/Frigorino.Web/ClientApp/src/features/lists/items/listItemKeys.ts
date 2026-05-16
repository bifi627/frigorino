export const listItemKeys = {
    all: ["listItems"] as const,
    byList: (householdId: number, listId: number) =>
        [
            ...listItemKeys.all,
            "household",
            householdId,
            "list",
            listId,
        ] as const,
    detail: (itemId: number) =>
        [...listItemKeys.all, "detail", itemId] as const,
} as const;
