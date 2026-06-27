import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getProductsQueryKey,
    resetProductClassificationMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useResetProductClassification = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...resetProductClassificationMutation(),
        onSettled: (_data, _error, variables) => {
            queryClient.invalidateQueries({
                queryKey: getProductsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
