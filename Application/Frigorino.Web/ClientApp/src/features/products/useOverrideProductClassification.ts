import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getProductsQueryKey,
    overrideProductClassificationMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useOverrideProductClassification = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...overrideProductClassificationMutation(),
        onSettled: (_data, _error, variables) => {
            queryClient.invalidateQueries({
                queryKey: getProductsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
