import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../common/apiClient";
import type {
    CreateHouseholdRequest,
    CurrentHouseholdResponse,
    HouseholdDto,
    HouseholdRole,
} from "../lib/api";

// Re-export types for convenience
export type {
    CreateHouseholdRequest,
    CurrentHouseholdResponse,
    HouseholdDto,
    HouseholdRole
};

// Query keys for consistent caching
export const householdKeys = {
    all: ["households"] as const,
    lists: () => [...householdKeys.all, "list"] as const,
    list: (filters: string) => [...householdKeys.lists(), { filters }] as const,
    details: () => [...householdKeys.all, "detail"] as const,
    detail: (id: number) => [...householdKeys.details(), id] as const,
    current: () => ["currentHousehold"] as const,
} as const;

// Custom hooks using React Query
export const useUserHouseholds = () => {
    return useQuery({
        queryKey: householdKeys.lists(),
        queryFn: async () => {
            return await ClientApi.household.getApiHousehold();
        },
        staleTime: 1000 * 60 * 5, // 5 minutes
    });
};

export const useCurrentHousehold = () => {
    return useQuery({
        queryKey: householdKeys.current(),
        queryFn: async () => {
            return await ClientApi.currentHousehold.getApiCurrentHousehold();
        },
        staleTime: 1000 * 60 * 5, // 5 minutes
        retry: false, // Don't retry if no current household
    });
};

export const useCreateHousehold = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async (data: CreateHouseholdRequest) => {
            return await ClientApi.household.postApiHousehold(data);
        },
        onSuccess: () => {
            // Invalidate and refetch households list
            queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
        },
    });
};

export const useSetCurrentHousehold = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async (householdId: number) => {
            return await ClientApi.currentHousehold.postApiCurrentHousehold(
                householdId,
            );
        },
        onSuccess: () => {
            // Invalidate current household to refetch
            queryClient.invalidateQueries({
                queryKey: householdKeys.current(),
            });
        },
    });
};
