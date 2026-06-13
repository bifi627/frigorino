import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createBlueprintMutation,
    getBlueprintsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useCreateSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
