import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateInventoryItemRequest,
    InventoryItemDto,
    UpdateInventoryItemRequest,
} from "../lib/api";
import { useDebouncedInvalidation } from "./useDebouncedInvalidation";

export type {
    CreateInventoryItemRequest,
    InventoryItemDto,
    UpdateInventoryItemRequest,
};

export const inventoryItemKeys = {
    all: ["inventoryItems"] as const,
    lists: () => [...inventoryItemKeys.all, "list"] as const,
    list: (filters?: string) =>
        [...inventoryItemKeys.lists(), { filters }] as const,
    details: () => [...inventoryItemKeys.all, "detail"] as const,
    detail: (id: number) => [...inventoryItemKeys.details(), id] as const,
    byInventory: (inventoryId: number) =>
        [...inventoryItemKeys.all, "inventory", inventoryId] as const,
} as const;

export const useInventoryItems = (inventoryId: number, enabled = true) => {
    return useQuery({
        queryKey: inventoryItemKeys.byInventory(inventoryId),
        queryFn: () =>
            ClientApi.inventoryItems.getApiInventoryInventoryItems(inventoryId),
        enabled: enabled && inventoryId > 0,
        staleTime: 1000 * 30,
    });
};

export const useCreateInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            inventoryId,
            data,
        }: {
            inventoryId: number;
            data: CreateInventoryItemRequest;
        }) => {
            return ClientApi.inventoryItems.postApiInventoryInventoryItems(
                inventoryId,
                data,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: inventoryItemKeys.byInventory(variables.inventoryId),
            });

            // Snapshot the previous value for rollback
            const previousItems = queryClient.getQueryData<InventoryItemDto[]>(
                inventoryItemKeys.byInventory(variables.inventoryId),
            );

            // Create optimistic item with temporary ID
            const optimisticItem: InventoryItemDto = {
                id: Date.now(), // Temporary ID until server responds
                text: variables.data.text,
                quantity: variables.data.quantity,
                expiryDate: variables.data.expiryDate,
                sortOrder: 999999999, // Will be at the bottom of items
                inventoryId: variables.inventoryId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                isExpiring: false,
            };

            // Optimistically add the item to the cache
            queryClient.setQueryData<InventoryItemDto[]>(
                inventoryItemKeys.byInventory(variables.inventoryId),
                (old) => {
                    if (!old) return [optimisticItem];

                    // Add new item at the beginning, update sortOrder for existing items
                    const updatedItems = old.map((item) => ({
                        ...item,
                        sortOrder: (item.sortOrder || 0) + 1,
                    }));

                    return [optimisticItem, ...updatedItems];
                },
            );

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous items
            if (context?.previousItems) {
                queryClient.setQueryData(
                    inventoryItemKeys.byInventory(variables.inventoryId),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            // Debounced invalidate the inventory items query to refetch with real data
            debouncedInvalidate(
                inventoryItemKeys.byInventory(variables.inventoryId),
            );
        },
    });
};

export const useUpdateInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            inventoryId,
            itemId,
            data,
        }: {
            inventoryId: number;
            itemId: number;
            data: UpdateInventoryItemRequest;
        }) => {
            return ClientApi.inventoryItems.putApiInventoryInventoryItems(
                inventoryId,
                itemId,
                data,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: inventoryItemKeys.byInventory(variables.inventoryId),
            });
            await queryClient.cancelQueries({
                queryKey: inventoryItemKeys.detail(variables.itemId),
            });

            // Snapshot the previous values for rollback
            const previousItems = queryClient.getQueryData<InventoryItemDto[]>(
                inventoryItemKeys.byInventory(variables.inventoryId),
            );
            const previousItem = queryClient.getQueryData<InventoryItemDto>(
                inventoryItemKeys.detail(variables.itemId),
            );

            // Optimistically update the item in the cache
            queryClient.setQueryData<InventoryItemDto[]>(
                inventoryItemKeys.byInventory(variables.inventoryId),
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? {
                                  ...item,
                                  text: variables.data.text || item.text,
                                  quantity:
                                      variables.data.quantity || item.quantity,
                                  expiryDate:
                                      variables.data.expiryDate !== undefined
                                          ? variables.data.expiryDate
                                          : item.expiryDate,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            // Also update the individual item cache if it exists
            if (previousItem) {
                queryClient.setQueryData<InventoryItemDto>(
                    inventoryItemKeys.detail(variables.itemId),
                    {
                        ...previousItem,
                        text: variables.data.text || previousItem.text,
                        quantity:
                            variables.data.quantity || previousItem.quantity,
                        expiryDate:
                            variables.data.expiryDate !== undefined
                                ? variables.data.expiryDate
                                : previousItem.expiryDate,
                        updatedAt: new Date().toISOString(),
                    },
                );
            }

            return { previousItems, previousItem };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous items
            if (context?.previousItems) {
                queryClient.setQueryData(
                    inventoryItemKeys.byInventory(variables.inventoryId),
                    context.previousItems,
                );
            }
            if (context?.previousItem) {
                queryClient.setQueryData(
                    inventoryItemKeys.detail(variables.itemId),
                    context.previousItem,
                );
            }
        },
        onSuccess: (_, variables) => {
            // Debounced invalidate both the inventory items and individual item queries
            debouncedInvalidate(
                inventoryItemKeys.byInventory(variables.inventoryId),
            );
            debouncedInvalidate(inventoryItemKeys.detail(variables.itemId));
        },
    });
};

export const useDeleteInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            inventoryId,
            itemId,
        }: {
            inventoryId: number;
            itemId: number;
        }) => {
            return ClientApi.inventoryItems.deleteApiInventoryInventoryItems(
                inventoryId,
                itemId,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: inventoryItemKeys.byInventory(variables.inventoryId),
            });

            // Snapshot the previous value for rollback
            const previousItems = queryClient.getQueryData<InventoryItemDto[]>(
                inventoryItemKeys.byInventory(variables.inventoryId),
            );

            // Optimistically remove the item from the cache
            queryClient.setQueryData<InventoryItemDto[]>(
                inventoryItemKeys.byInventory(variables.inventoryId),
                (old) => {
                    if (!old) return old;
                    return old.filter((item) => item.id !== variables.itemId);
                },
            );

            // Also remove the individual item from cache
            queryClient.removeQueries({
                queryKey: inventoryItemKeys.detail(variables.itemId),
            });

            return { previousItems };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous items
            if (context?.previousItems) {
                queryClient.setQueryData(
                    inventoryItemKeys.byInventory(variables.inventoryId),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            // On success, ensure the item is removed from cache
            queryClient.removeQueries({
                queryKey: inventoryItemKeys.detail(variables.itemId),
            });
        },
        onSettled: (_, __, variables) => {
            // Always refetch to ensure consistency with server (debounced)
            debouncedInvalidate(
                inventoryItemKeys.byInventory(variables.inventoryId),
            );
        },
    });
};
