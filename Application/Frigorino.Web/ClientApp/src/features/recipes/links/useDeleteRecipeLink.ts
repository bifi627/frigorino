import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteRecipeLinkMutation,
    getRecipeLinksQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeLinkResponse } from "../../../lib/api/types.gen";
import { useRestoreRecipeLink } from "./useRestoreRecipeLink";

export const useDeleteRecipeLink = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreLink = useRestoreRecipeLink();

    return useMutation({
        ...deleteRecipeLinkMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeLinksQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousLinks =
                queryClient.getQueryData<RecipeLinkResponse[]>(queryKey);

            queryClient.setQueryData<RecipeLinkResponse[]>(queryKey, (old) =>
                old?.filter((l) => l.id !== variables.path.linkId),
            );

            return { previousLinks };
        },
        onError: (_data, variables, context) => {
            if (context?.previousLinks) {
                queryClient.setQueryData(
                    getRecipeLinksQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousLinks,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("recipes.linkDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreLink.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeLinksQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};
