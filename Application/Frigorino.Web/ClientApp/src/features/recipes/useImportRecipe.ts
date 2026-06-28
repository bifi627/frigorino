import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    importRecipeMutation,
    getRecipesQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useImportRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...importRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
