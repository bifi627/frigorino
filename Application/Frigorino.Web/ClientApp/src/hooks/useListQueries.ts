import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type { CreateListRequest, ListDto, UpdateListRequest } from "../lib/api";

// Re-export types for convenience
export type { CreateListRequest, ListDto, UpdateListRequest };

// Query Keys - centralized for consistency
export const listKeys = {
    all: ["lists"] as const,
    lists: () => [...listKeys.all, "list"] as const,
    list: (filters?: string) => [...listKeys.lists(), { filters }] as const,
    details: () => [...listKeys.all, "detail"] as const,
    detail: (id: number) => [...listKeys.details(), id] as const,
    byHousehold: (householdId: number) =>
        [...listKeys.all, "household", householdId] as const,
} as const;

// Household Lists Hook
export const useHouseholdLists = (householdId: number, enabled = true) => {
    return useQuery({
        queryKey: listKeys.byHousehold(householdId),
        queryFn: () => ClientApi.lists.getApiHouseholdLists(householdId),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2, // 2 minutes
    });
};

// Get Single List Hook
export const useList = (
    householdId: number,
    listId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: listKeys.detail(listId),
        queryFn: () =>
            ClientApi.lists.getApiHouseholdLists1(householdId, listId),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2, // 2 minutes
    });
};

// Create List Hook
export const useCreateList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async ({
            householdId,
            data,
        }: {
            householdId: number;
            data: CreateListRequest;
        }) => {
            return ClientApi.lists.postApiHouseholdLists(householdId, data);
        },
        onSuccess: (data, variables) => {
            // Invalidate and refetch household lists
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });

            // Optionally add the new list to the cache
            if (data.id) {
                queryClient.setQueryData(listKeys.detail(data.id), data);
            }
        },
    });
};

// Update List Hook
export const useUpdateList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
            data,
        }: {
            householdId: number;
            listId: number;
            data: UpdateListRequest;
        }) => {
            return ClientApi.lists.putApiHouseholdLists(
                householdId,
                listId,
                data,
            );
        },
        onSuccess: (data, variables) => {
            // Update the specific list in cache
            if (data?.id) {
                queryClient.setQueryData(listKeys.detail(data.id), data);
            }

            // Invalidate household lists to ensure consistency
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });
        },
    });
};

// Delete List Hook
export const useDeleteList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async ({
            householdId,
            listId,
        }: {
            householdId: number;
            listId: number;
        }) => {
            return ClientApi.lists.deleteApiHouseholdLists(householdId, listId);
        },
        onSuccess: (_, variables) => {
            // Remove the specific list from cache
            queryClient.removeQueries({
                queryKey: listKeys.detail(variables.listId),
            });

            // Invalidate household lists
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });
        },
    });
};
