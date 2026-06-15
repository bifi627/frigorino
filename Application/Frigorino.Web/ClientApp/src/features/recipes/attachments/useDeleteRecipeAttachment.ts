import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteRecipeAttachmentMutation,
    getRecipeAttachmentsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeAttachmentResponse } from "../../../lib/api/types.gen";
import { useRestoreRecipeAttachment } from "./useRestoreRecipeAttachment";

export const useDeleteRecipeAttachment = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreAttachment = useRestoreRecipeAttachment();

    return useMutation({
        ...deleteRecipeAttachmentMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeAttachmentsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousAttachments =
                queryClient.getQueryData<RecipeAttachmentResponse[]>(queryKey);

            queryClient.setQueryData<RecipeAttachmentResponse[]>(
                queryKey,
                (old) =>
                    old?.filter((a) => a.id !== variables.path.attachmentId),
            );

            return { previousAttachments };
        },
        onError: (_data, variables, context) => {
            if (context?.previousAttachments) {
                queryClient.setQueryData(
                    getRecipeAttachmentsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousAttachments,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("recipes.attachmentDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreAttachment.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};
