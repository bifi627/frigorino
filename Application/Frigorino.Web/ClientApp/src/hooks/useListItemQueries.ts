import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateListItemRequest,
    ListItemDto,
    ReorderItemRequest,
    UpdateListItemRequest,
} from "../lib/api";

// Re-export types for convenience
export type {
    CreateListItemRequest,
    ListItemDto,
    ReorderItemRequest,
    UpdateListItemRequest,
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
        queryFn: () =>
            ClientApi.listItems.getApiHouseholdListsListItems(
                householdId,
                listId,
            ),
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
            ClientApi.listItems.getApiHouseholdListsListItems1(
                householdId,
                listId,
                itemId,
            ),
        enabled: enabled && itemId > 0 && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2, // 2 minutes
    });
};

// Create List Item Hook
export const useCreateListItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            data,
        }: {
            householdId: number;
            listId: number;
            data: CreateListItemRequest;
        }) => {
            return ClientApi.listItems.postApiHouseholdListsListItems(
                householdId,
                listId,
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
            const previousItems = queryClient.getQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Create optimistic item with temporary ID
            const optimisticItem: ListItemDto = {
                id: Date.now(), // Temporary ID until server responds
                text: variables.data.text,
                quantity: variables.data.quantity,
                status: false, // New items are always unchecked
                sortOrder: 999999999, // Will be at the bottom of unchecked items
                listId: variables.listId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            // Optimistically add the item to the cache
            queryClient.setQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return [optimisticItem];

                    // Add new item at the beginning of unchecked items
                    // Update sortOrder for existing unchecked items
                    const updatedItems = old.map((item) =>
                        !item.status
                            ? { ...item, sortOrder: (item.sortOrder || 0) + 1 }
                            : item,
                    );

                    return [optimisticItem, ...updatedItems];
                },
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
            // Invalidate the list items query to refetch with real data
            queryClient.invalidateQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });
        },
    });
};

// Update List Item Hook
export const useUpdateListItem = () => {
    const queryClient = useQueryClient();

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
            data: UpdateListItemRequest;
        }) => {
            return ClientApi.listItems.putApiHouseholdListsListItems(
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
            const previousItems = queryClient.getQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically update the item in the cache
            queryClient.setQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? {
                                  ...item,
                                  text: variables.data.text || item.text,
                                  quantity:
                                      variables.data.quantity || item.quantity,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            // Also update the individual item cache if it exists
            const currentItem = queryClient.getQueryData<ListItemDto>(
                listItemKeys.detail(variables.itemId),
            );
            if (currentItem) {
                queryClient.setQueryData<ListItemDto>(
                    listItemKeys.detail(variables.itemId),
                    {
                        ...currentItem,
                        text: variables.data.text || currentItem.text,
                        quantity:
                            variables.data.quantity || currentItem.quantity,
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
            // Invalidate both the list items and individual item queries
            queryClient.invalidateQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });
            queryClient.invalidateQueries({
                queryKey: listItemKeys.detail(variables.itemId),
            });
        },
    });
};

// Delete List Item Hook
export const useDeleteListItem = () => {
    const queryClient = useQueryClient();

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
            return ClientApi.listItems.deleteApiHouseholdListsListItems(
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
            const previousItems = queryClient.getQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically remove the item from the cache
            queryClient.setQueryData<ListItemDto[]>(
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
            // Always refetch to ensure consistency with server
            queryClient.invalidateQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });
        },
    });
};

// Toggle List Item Status Hook
export const useToggleListItemStatus = () => {
    const queryClient = useQueryClient();

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
            return ClientApi.listItems.patchApiHouseholdListsListItemsToggleStatus(
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
            const previousItems = queryClient.getQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically update the cache
            queryClient.setQueryData<ListItemDto[]>(
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
        onSettled: (_, __, variables) => {
            // Always refetch to ensure consistency
            queryClient.invalidateQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });
        },
    });
};

// Reorder List Item Hook
export const useReorderListItem = () => {
    const queryClient = useQueryClient();

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
            return ClientApi.listItems.patchApiHouseholdListsListItemsReorder(
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
            const previousItems = queryClient.getQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Optimistically update the cache with new sortOrder
            queryClient.setQueryData<ListItemDto[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;

                    const movedItem = old.find(
                        (item) => item.id === variables.itemId,
                    );
                    if (!movedItem) return old;

                    // Separate items by status
                    const targetSection = old.filter(
                        (item) => item.status === movedItem.status,
                    );

                    // Find the new sortOrder based on afterId
                    let newSortOrder = 1;
                    if (
                        variables.data.afterId !== undefined &&
                        variables.data.afterId !== 0
                    ) {
                        const afterItem = targetSection.find(
                            (item) => item.id === variables.data.afterId,
                        );
                        if (afterItem) {
                            newSortOrder = (afterItem.sortOrder || 0) + 1;
                        }
                    }

                    // Update sortOrder for affected items
                    return old.map((item) => {
                        if (item.id === variables.itemId) {
                            // Update the moved item's sortOrder
                            return { ...item, sortOrder: newSortOrder };
                        } else if (
                            item.status === movedItem.status &&
                            (item.sortOrder || 0) >= newSortOrder &&
                            item.id !== variables.itemId
                        ) {
                            // Increment sortOrder for items that need to shift down
                            return {
                                ...item,
                                sortOrder: (item.sortOrder || 0) + 1,
                            };
                        }
                        return item;
                    });
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
            // Always refetch to ensure consistency with server
            queryClient.invalidateQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });
        },
    });
};

// Compact List Items Hook
export const useCompactListItems = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
        }: {
            householdId: number;
            listId: number;
        }) => {
            return ClientApi.listItems.postApiHouseholdListsListItemsCompact(
                householdId,
                listId,
            );
        },
        onSuccess: (_, variables) => {
            // Invalidate the list items query to refetch with compacted order
            queryClient.invalidateQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });
        },
    });
};
