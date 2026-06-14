import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createRecipeMutation,
    getRecipesQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useCreateRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...createRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
