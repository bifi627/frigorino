import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    applyBlueprintMutation,
    getItemsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useApplyBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...applyBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
        },
    });
};
