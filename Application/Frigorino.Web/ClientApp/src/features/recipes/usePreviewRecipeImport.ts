import { useMutation } from "@tanstack/react-query";
import { previewRecipeImportMutation } from "../../lib/api/@tanstack/react-query.gen";

// Read-only peek of a recipe URL (name + image) before importing. Arg-less; caller passes
// { body: { url } } to mutate. No invalidation — it persists nothing.
export const usePreviewRecipeImport = () =>
    useMutation({ ...previewRecipeImportMutation() });
