import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteBlueprintMutation,
    getBlueprintsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...deleteBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
