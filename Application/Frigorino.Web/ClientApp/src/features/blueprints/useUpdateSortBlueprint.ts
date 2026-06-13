import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getBlueprintQueryKey,
    getBlueprintsQueryKey,
    updateBlueprintMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getBlueprintQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        blueprintId: variables.path.blueprintId,
                    },
                }),
            });
        },
    });
};
