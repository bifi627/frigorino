import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import type { UpdateMemberRoleRequest } from "../../../lib/api";
import { householdKeys } from "../householdKeys";

export const useUpdateMemberRole = (householdId: number) => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            userId,
            role,
        }: {
            userId: string;
            role: UpdateMemberRoleRequest["role"];
        }) => ClientApi.members.updateMemberRole(householdId, userId, { role }),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: householdKeys.members(householdId),
            });
        },
    });
};
