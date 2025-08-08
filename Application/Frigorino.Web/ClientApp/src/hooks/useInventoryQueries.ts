import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateInventoryRequest,
    InventoryDto,
    UpdateInventoryRequest,
} from "../lib/api";

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
        onSuccess: (data, variables) => {
            // Invalidate and refetch household inventories
            queryClient.invalidateQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });

            // Optionally add the new inventory to the cache
            if (data.id) {
                queryClient.setQueryData(inventoryKeys.detail(data.id), data);
            }
        },
    });
};

// Update Inventory Hook
export const useUpdateInventory = () => {
    const queryClient = useQueryClient();

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
        onSuccess: (data, variables) => {
            // Update the specific inventory in cache
            if (data?.id) {
                queryClient.setQueryData(inventoryKeys.detail(data.id), data);
            }

            // Invalidate household inventories to ensure consistency
            queryClient.invalidateQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });
        },
    });
};

// Delete Inventory Hook
export const useDeleteInventory = () => {
    const queryClient = useQueryClient();

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
        onSuccess: (_, variables) => {
            // Remove the specific inventory from cache
            queryClient.removeQueries({
                queryKey: inventoryKeys.detail(variables.inventoryId),
            });

            // Invalidate household inventories
            queryClient.invalidateQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });
        },
    });
};
