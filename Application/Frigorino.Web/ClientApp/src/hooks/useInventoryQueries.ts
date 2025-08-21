import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateInventoryRequest,
    InventoryDto,
    UpdateInventoryRequest,
} from "../lib/api";
import { useDebouncedInvalidation } from "./useDebouncedInvalidation";

// Re-export types for convenience
export type { CreateInventoryRequest, InventoryDto, UpdateInventoryRequest };

// Query Keys - centralized for consistency
export const inventoryKeys = {
    all: ["inventories"] as const,
    lists: () => [...inventoryKeys.all, "list"] as const,
    list: (filters?: string) =>
        [...inventoryKeys.lists(), { filters }] as const,
    details: () => [...inventoryKeys.all, "detail"] as const,
    detail: (id: number) => [...inventoryKeys.details(), id] as const,
    byHousehold: (householdId: number) =>
        [...inventoryKeys.all, "household", householdId] as const,
} as const;

// Household Inventories Hook
export const useHouseholdInventories = (
    householdId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: inventoryKeys.byHousehold(householdId),
        queryFn: () =>
            ClientApi.inventories.getApiHouseholdInventories(householdId),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2, // 2 minutes
    });
};

// Get Single Inventory Hook
export const useInventory = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: inventoryKeys.detail(inventoryId),
        queryFn: () =>
            ClientApi.inventories.getApiHouseholdInventories1(
                householdId,
                inventoryId,
            ),
        enabled: enabled && inventoryId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2, // 2 minutes
    });
};

// Create Inventory Hook
export const useCreateInventory = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            data,
        }: {
            householdId: number;
            data: CreateInventoryRequest;
        }) => {
            return ClientApi.inventories.postApiHouseholdInventories(
                householdId,
                data,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });

            // Snapshot the previous value for rollback
            const previousInventories = queryClient.getQueryData<InventoryDto[]>(
                inventoryKeys.byHousehold(variables.householdId),
            );

            // Create optimistic inventory with temporary ID
            const optimisticInventory: InventoryDto = {
                id: Date.now(), // Temporary ID until server responds
                name: variables.data.name,
                description: variables.data.description,
                householdId: variables.householdId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            // Optimistically add the inventory to the cache
            queryClient.setQueryData<InventoryDto[]>(
                inventoryKeys.byHousehold(variables.householdId),
                (old) => {
                    if (!old) return [optimisticInventory];
                    return [optimisticInventory, ...old];
                },
            );

            return { previousInventories };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous inventories
            if (context?.previousInventories) {
                queryClient.setQueryData(
                    inventoryKeys.byHousehold(variables.householdId),
                    context.previousInventories,
                );
            }
        },
        onSuccess: (data, variables) => {
            // Add the new inventory to the cache with real data
            if (data.id) {
                queryClient.setQueryData(inventoryKeys.detail(data.id), data);
            }

            // Debounced invalidate household inventories to refetch with real data
            debouncedInvalidate(
                inventoryKeys.byHousehold(variables.householdId),
            );
        },
    });
};

// Update Inventory Hook
export const useUpdateInventory = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            inventoryId,
            data,
        }: {
            householdId: number;
            inventoryId: number;
            data: UpdateInventoryRequest;
        }) => {
            return ClientApi.inventories.putApiHouseholdInventories(
                householdId,
                inventoryId,
                data,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });
            await queryClient.cancelQueries({
                queryKey: inventoryKeys.detail(variables.inventoryId),
            });

            // Snapshot the previous values for rollback
            const previousInventories = queryClient.getQueryData<InventoryDto[]>(
                inventoryKeys.byHousehold(variables.householdId),
            );
            const previousInventory = queryClient.getQueryData<InventoryDto>(
                inventoryKeys.detail(variables.inventoryId),
            );

            // Optimistically update the inventory in the household list cache
            queryClient.setQueryData<InventoryDto[]>(
                inventoryKeys.byHousehold(variables.householdId),
                (old) => {
                    if (!old) return old;
                    return old.map((inventory) =>
                        inventory.id === variables.inventoryId
                            ? {
                                  ...inventory,
                                  name: variables.data.name || inventory.name,
                                  description: variables.data.description || inventory.description,
                                  updatedAt: new Date().toISOString(),
                              }
                            : inventory,
                    );
                },
            );

            // Also update the individual inventory cache if it exists
            if (previousInventory) {
                queryClient.setQueryData<InventoryDto>(
                    inventoryKeys.detail(variables.inventoryId),
                    {
                        ...previousInventory,
                        name: variables.data.name || previousInventory.name,
                        description: variables.data.description || previousInventory.description,
                        updatedAt: new Date().toISOString(),
                    },
                );
            }

            return { previousInventories, previousInventory };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous inventories
            if (context?.previousInventories) {
                queryClient.setQueryData(
                    inventoryKeys.byHousehold(variables.householdId),
                    context.previousInventories,
                );
            }
            if (context?.previousInventory) {
                queryClient.setQueryData(
                    inventoryKeys.detail(variables.inventoryId),
                    context.previousInventory,
                );
            }
        },
        onSuccess: (data, variables) => {
            // Update with real data from server
            if (data?.id) {
                queryClient.setQueryData(inventoryKeys.detail(data.id), data);
            }

            // Debounced invalidate both queries
            debouncedInvalidate(
                inventoryKeys.byHousehold(variables.householdId),
            );
            debouncedInvalidate(inventoryKeys.detail(variables.inventoryId));
        },
    });
};

// Delete Inventory Hook
export const useDeleteInventory = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: async ({
            householdId,
            inventoryId,
        }: {
            householdId: number;
            inventoryId: number;
        }) => {
            return ClientApi.inventories.deleteApiHouseholdInventories(
                householdId,
                inventoryId,
            );
        },
        onMutate: async (variables) => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });

            // Snapshot the previous value for rollback
            const previousInventories = queryClient.getQueryData<InventoryDto[]>(
                inventoryKeys.byHousehold(variables.householdId),
            );

            // Optimistically remove the inventory from the cache
            queryClient.setQueryData<InventoryDto[]>(
                inventoryKeys.byHousehold(variables.householdId),
                (old) => {
                    if (!old) return old;
                    return old.filter((inventory) => inventory.id !== variables.inventoryId);
                },
            );

            // Also remove the individual inventory from cache
            queryClient.removeQueries({
                queryKey: inventoryKeys.detail(variables.inventoryId),
            });

            return { previousInventories };
        },
        onError: (_, variables, context) => {
            // Rollback on error - restore the previous inventories
            if (context?.previousInventories) {
                queryClient.setQueryData(
                    inventoryKeys.byHousehold(variables.householdId),
                    context.previousInventories,
                );
            }
        },
        onSuccess: (_, variables) => {
            // On success, ensure the inventory is removed from cache
            queryClient.removeQueries({
                queryKey: inventoryKeys.detail(variables.inventoryId),
            });
        },
        onSettled: (_, __, variables) => {
            // Always refetch to ensure consistency with server (debounced)
            debouncedInvalidate(
                inventoryKeys.byHousehold(variables.householdId),
            );
        },
    });
};
