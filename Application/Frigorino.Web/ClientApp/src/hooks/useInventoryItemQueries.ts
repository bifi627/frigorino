import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateInventoryItemRequest,
    InventoryItemDto,
    UpdateInventoryItemRequest,
} from "../lib/api";

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
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({
                queryKey: inventoryItemKeys.byInventory(variables.inventoryId),
            });
        },
    });
};

export const useUpdateInventoryItem = () => {
    const queryClient = useQueryClient();

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
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({
                queryKey: inventoryItemKeys.byInventory(variables.inventoryId),
            });
            queryClient.invalidateQueries({
                queryKey: inventoryItemKeys.detail(variables.itemId),
            });
        },
    });
};

export const useDeleteInventoryItem = () => {
    const queryClient = useQueryClient();

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
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({
                queryKey: inventoryItemKeys.byInventory(variables.inventoryId),
            });
            queryClient.removeQueries({
                queryKey: inventoryItemKeys.detail(variables.itemId),
            });
        },
    });
};
