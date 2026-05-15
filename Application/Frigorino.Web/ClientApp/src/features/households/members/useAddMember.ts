import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import type { AddMemberRequest } from "../../../lib/api";
import { householdKeys } from "../householdKeys";

export const useAddMember = (householdId: number) => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (data: AddMemberRequest) =>
            ClientApi.members.addMember(householdId, data),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: householdKeys.members(householdId),
            });
            queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
        },
    });
};
