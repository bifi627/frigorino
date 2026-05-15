import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { householdKeys } from "../householdKeys";

export const useRemoveMember = (householdId: number) => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (userId: string) =>
            ClientApi.members.removeMember(householdId, userId),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: householdKeys.members(householdId),
            });
            queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
        },
    });
};
