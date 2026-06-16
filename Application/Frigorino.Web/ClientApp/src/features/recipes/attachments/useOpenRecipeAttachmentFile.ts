import { useCallback } from "react";
import { client } from "../../../lib/api/client.gen";

// Opens a recipe attachment's file in a new tab. The /file endpoint requires the Bearer token (injected
// by the fetch client), so a naked link/window.open(url) would 401. Instead we fetch the bytes as an
// authenticated blob and point a tab at the resulting object URL (the browser renders by MIME type).
// The tab is opened SYNCHRONOUSLY inside the click gesture, then navigated once the fetch resolves —
// opening after the await would be eaten by popup blockers.
export const useOpenRecipeAttachmentFile = (
    householdId: number,
    recipeId: number,
) =>
    useCallback(
        (attachmentId: number) => {
            const win = window.open("", "_blank");
            void (async () => {
                try {
                    const { data, error } = await client.get({
                        url: `/api/household/${householdId}/recipes/${recipeId}/attachments/${attachmentId}/file`,
                        parseAs: "blob",
                    });
                    if (error || !data || !win) {
                        win?.close();
                        return;
                    }
                    const objectUrl = URL.createObjectURL(data as Blob);
                    win.location.href = objectUrl;
                    // Revoke once the tab has loaded the blob.
                    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
                } catch {
                    win?.close();
                }
            })();
        },
        [householdId, recipeId],
    );
