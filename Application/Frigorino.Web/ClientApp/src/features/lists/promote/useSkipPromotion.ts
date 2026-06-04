import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getListQueryKey,
    getPendingPromotionsQueryKey,
    skipPromotionMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

// Resolve-as-skipped (X = one id, Clear All = all pending ids). Caller passes
// { path: { householdId, listId }, body: { listItemIds } }.
export const useSkipPromotion = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...skipPromotionMutation(),
        onSuccess: (_data, variables) => {
            const { householdId, listId } = variables.path;
            queryClient.invalidateQueries({
                queryKey: getPendingPromotionsQueryKey({
                    path: { householdId, listId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getListQueryKey({ path: { householdId, listId } }),
            });
        },
    });
};
