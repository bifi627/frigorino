import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { ClientApi } from '../common/apiClient';
import type {
    AddMemberRequest,
    CreateHouseholdRequest,
    CurrentHouseholdResponse,
    HouseholdDto,
    HouseholdRole,
    UpdateHouseholdRequest,
    UpdateMemberRoleRequest
} from '../lib/api';

// Re-export types for convenience
export type {
    CreateHouseholdRequest,
    CurrentHouseholdResponse,
    HouseholdDto,
    HouseholdRole
};

// Query Keys - centralized for consistency
export const householdKeys = {
  all: ['households'] as const,
  lists: () => [...householdKeys.all, 'list'] as const,
  list: (filters?: string) => [...householdKeys.lists(), { filters }] as const,
  details: () => [...householdKeys.all, 'detail'] as const,
  detail: (id: number) => [...householdKeys.details(), id] as const,
  current: () => ['currentHousehold'] as const,
  members: (householdId: number) => [...householdKeys.all, 'members', householdId] as const,
} as const;

// Current Household Hook
export const useCurrentHousehold = () => {
  return useQuery({
    queryKey: householdKeys.current(),
    queryFn: () => ClientApi.currentHousehold.getApiCurrentHousehold(),
    staleTime: 1000 * 60 * 5, // 5 minutes
    retry: false, // Don't retry if no current household
  });
};

// User Households List Hook
export const useUserHouseholds = (enabled = true) => {
  return useQuery({
    queryKey: householdKeys.lists(),
    queryFn: () => ClientApi.household.getApiHousehold(),
    enabled,
    staleTime: 1000 * 60 * 5, // 5 minutes
  });
};

// Household Members Hook
export const useHouseholdMembers = (householdId: number, enabled = true) => {
  return useQuery({
    queryKey: householdKeys.members(householdId),
    queryFn: () => ClientApi.members.getApiHouseholdMembers(householdId),
    enabled: enabled && !!householdId,
  });
};

// Create Household Hook
export const useCreateHousehold = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateHouseholdRequest) => 
      ClientApi.household.postApiHousehold(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
    },
  });
};

// Set Current Household Hook
export const useSetCurrentHousehold = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (householdId: number) => 
      ClientApi.currentHousehold.postApiCurrentHousehold(householdId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: householdKeys.current() });
      queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
    },
  });
};

// Update Household Hook
export const useUpdateHousehold = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateHouseholdRequest }) => 
      ClientApi.household.putApiHousehold(id, data),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: householdKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
      queryClient.invalidateQueries({ queryKey: householdKeys.current() });
    },
  });
};

// Delete Household Hook
export const useDeleteHousehold = () => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: (householdId: number) => 
      ClientApi.household.deleteApiHousehold(householdId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: householdKeys.current() });
      queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
      navigate({ to: "/" });
    },
  });
};

// Add Member Hook
export const useAddMember = (householdId: number) => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: AddMemberRequest) => 
      ClientApi.members.postApiHouseholdMembers(householdId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: householdKeys.members(householdId) });
      queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
    },
  });
};

// Update Member Role Hook
export const useUpdateMemberRole = (householdId: number) => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: UpdateMemberRoleRequest['role'] }) => 
      ClientApi.members.putApiHouseholdMembersRole(householdId, userId, { role }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: householdKeys.members(householdId) });
    },
  });
};

// Remove Member Hook
export const useRemoveMember = (householdId: number) => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) => 
      ClientApi.members.deleteApiHouseholdMembers(householdId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: householdKeys.members(householdId) });
      queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
    },
  });
};

// Combined Hook for Current Household with Details
export const useCurrentHouseholdWithDetails = () => {
  const currentHouseholdQuery = useCurrentHousehold();
  const householdsQuery = useUserHouseholds(!!currentHouseholdQuery.data?.householdId);

  const currentHouseholdDetails = householdsQuery.data?.find(
    h => h.id === currentHouseholdQuery.data?.householdId
  );

  return {
    currentHousehold: currentHouseholdQuery.data,
    currentHouseholdDetails,
    isLoading: currentHouseholdQuery.isLoading || householdsQuery.isLoading,
    error: currentHouseholdQuery.error || householdsQuery.error,
    hasActiveHousehold: currentHouseholdQuery.data?.hasActiveHousehold && !!currentHouseholdQuery.data?.householdId,
  };
};
