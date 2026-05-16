import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateItemRequest,
    ListItemResponse,
    ReorderItemRequest,
    UpdateItemRequest,
} from "../lib/api";
import { useDebouncedInvalidation } from "./useDebouncedInvalidation";

// Re-export types for convenience
export type {
    CreateItemRequest,
    ListItemResponse,
    ReorderItemRequest,
    UpdateItemRequest,
};

// Query Keys - centralized for consistency
export const listItemKeys = {
    all: ["listItems"] as const,
    lists: () => [...listItemKeys.all, "list"] as const,
    list: (filters?: string) => [...listItemKeys.lists(), { filters }] as const,
    details: () => [...listItemKeys.all, "detail"] as const,
    detail: (id: number) => [...listItemKeys.details(), id] as const,
    byList: (householdId: number, listId: number) =>
        [
            ...listItemKeys.all,
            "household",
            householdId,
            "list",
            listId,
        ] as const,
} as const;

// Get List Items Hook
export const useListItems = (
    householdId: number,
    listId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: listItemKeys.byList(householdId, listId),
        queryFn: () => ClientApi.listItems.getItems(householdId, listId),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 30, // 30 seconds (more frequent updates for collaborative lists)
    });
};

// Get Single List Item Hook
export const useListItem = (
    householdId: number,
    listId: number,
    itemId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: listItemKeys.detail(itemId),
        queryFn: () =>
            ClientApi.listItems.getItem(householdId, listId, itemId),
        enabled: enabled && itemId > 0 && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2, // 2 minutes
    });
};

// Create List Item Hook
export const useCreateListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            data,
        }: {
            householdId: number;
            listId: number;
            data: CreateItemRequest;
        }) => {
            return ClientApi.listItems.createItem(householdId, listId, data);
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Append below the last unchecked item to match the server's AddItem semantics
            // (List.ComputeAppendSortOrder: last + DefaultGap). Falls back to a sentinel so the
            // item appears at the bottom of unchecked when the cache is empty.
            const lastUncheckedSortOrder = previousItems
                ?.filter((i) => !i.status)
                .reduce((max, i) => Math.max(max, i.sortOrder), 0) ?? 0;

            const optimisticItem: ListItemResponse = {
                id: Date.now(),
                text: variables.data.text,
                quantity: variables.data.quantity,
                status: false,
                sortOrder: lastUncheckedSortOrder + 1,
                listId: variables.listId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => (old ? [...old, optimisticItem] : [optimisticItem]),
            );

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous items
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            // Debounced invalidate the list items query to refetch with real data
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

// Update List Item Hook
export const useUpdateListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            itemId,
            data,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
            data: UpdateItemRequest;
        }) => {
            return ClientApi.listItems.updateItem(
                householdId,
                listId,
                itemId,
                data,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            // Snapshot the previous value for rollback
            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically update the item in the cache
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? {
                                  ...item,
                                  text: variables.data.text ?? item.text,
                                  quantity:
                                      variables.data.quantity ?? item.quantity,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            // Also update the individual item cache if it exists
            const currentItem = queryClient.getQueryData<ListItemResponse>(
                listItemKeys.detail(variables.itemId),
            );
            if (currentItem) {
                queryClient.setQueryData<ListItemResponse>(
                    listItemKeys.detail(variables.itemId),
                    {
                        ...currentItem,
                        text: variables.data.text ?? currentItem.text,
                        quantity:
                            variables.data.quantity ?? currentItem.quantity,
                        updatedAt: new Date().toISOString(),
                    },
                );
            }

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous items
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            // Debounced invalidate both the list items and individual item queries
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
            debouncedInvalidate(listItemKeys.detail(variables.itemId));
        },
    });
};

// Delete List Item Hook
export const useDeleteListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            itemId,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
        }) => {
            return ClientApi.listItems.deleteItem(householdId, listId, itemId);
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            // Snapshot the previous value for rollback
            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically remove the item from the cache
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    return old.filter((item) => item.id !== variables.itemId);
                },
            );

            // Also remove the individual item from cache
            queryClient.removeQueries({
                queryKey: listItemKeys.detail(variables.itemId),
            });

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous items
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            // On success, ensure the item is removed from cache
            queryClient.removeQueries({
                queryKey: listItemKeys.detail(variables.itemId),
            });
        },
        onSettled: (_, __, variables) => {
            // Always refetch to ensure consistency with server (debounced)
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

// Toggle List Item Status Hook
export const useToggleListItemStatus = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            itemId,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
        }) => {
            return ClientApi.listItems.toggleItemStatus(
                householdId,
                listId,
                itemId,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            // Snapshot the previous value for rollback
            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically update the cache
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? { ...item, status: !item.status }
                            : item,
                    );
                },
            );

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSuccess: () => {
            // For toggle status, we rely on optimistic updates and only invalidate on settled
            // No invalidation here to avoid double calls
        },
        onSettled: (_, __, variables) => {
            // Always refetch to ensure consistency (debounced)
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

// Reorder List Item Hook
export const useReorderListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            itemId,
            data,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
            data: ReorderItemRequest;
        }) => {
            return ClientApi.listItems.reorderItem(
                householdId,
                listId,
                itemId,
                data,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            // Snapshot the previous value for rollback
            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Mirror the server's midpoint math (List.ReorderItem):
            //   afterId == 0  → first.sortOrder - DefaultGap (top of section)
            //   no `before`   → after.sortOrder + DefaultGap (last of section)
            //   otherwise     → midpoint between after and before
            // We don't shift sibling sortOrders — the server doesn't either.
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    const movedItem = old.find((i) => i.id === variables.itemId);
                    if (!movedItem) return old;

                    const section = old
                        .filter(
                            (i) =>
                                i.status === movedItem.status &&
                                i.id !== movedItem.id,
                        )
                        .sort((a, b) => a.sortOrder - b.sortOrder);

                    const DEFAULT_GAP = 10_000;
                    const UNCHECKED_MIN = 1_000_000;
                    const CHECKED_MIN = 10_000_000;

                    const afterItem =
                        variables.data.afterId && variables.data.afterId !== 0
                            ? section.find(
                                  (i) => i.id === variables.data.afterId,
                              )
                            : undefined;
                    const beforeItem = afterItem
                        ? section.find((i) => i.sortOrder > afterItem.sortOrder)
                        : undefined;

                    let newSortOrder: number;
                    if (!afterItem) {
                        newSortOrder = section.length
                            ? section[0].sortOrder - DEFAULT_GAP
                            : (movedItem.status ? CHECKED_MIN : UNCHECKED_MIN) +
                              DEFAULT_GAP;
                    } else if (!beforeItem) {
                        newSortOrder = afterItem.sortOrder + DEFAULT_GAP;
                    } else {
                        newSortOrder = Math.floor(
                            afterItem.sortOrder +
                                (beforeItem.sortOrder - afterItem.sortOrder) /
                                    2,
                        );
                    }

                    return old.map((i) =>
                        i.id === movedItem.id
                            ? { ...i, sortOrder: newSortOrder }
                            : i,
                    );
                },
            );

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSettled: (_, __, variables) => {
            // Always refetch to ensure consistency with server (debounced)
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

// Compact List Items Hook
export const useCompactListItems = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
        }: {
            householdId: number;
            listId: number;
        }) => {
            return ClientApi.listItems.compactItems(householdId, listId);
        },
        onSuccess: (_, variables) => {
            // Debounced invalidate the list items query to refetch with compacted order
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};
