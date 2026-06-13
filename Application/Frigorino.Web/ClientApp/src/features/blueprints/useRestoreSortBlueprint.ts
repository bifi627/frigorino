import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getBlueprintsQueryKey,
    restoreBlueprintMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useRestoreSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
