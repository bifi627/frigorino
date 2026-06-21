import { useCallback } from "react";
import { client } from "../../../lib/api/client.gen";

// Opens a list item's stored file (a PDF document) in a new tab. The /file endpoint requires the
// Bearer token (injected by the fetch client), so a naked link/window.open(url) would 401. Instead
// we fetch the bytes as an authenticated blob and point a tab at the resulting object URL (the
// browser renders the PDF in its native viewer). The tab is opened SYNCHRONOUSLY inside the click
// gesture, then navigated once the fetch resolves — opening after the await would be eaten by popup
// blockers. Mirrors features/recipes/attachments/useOpenRecipeAttachmentFile.ts.
export const useOpenItemFile = (householdId: number, listId: number) =>
    useCallback(
        (itemId: number) => {
            const win = window.open("", "_blank");
            void (async () => {
                try {
                    const { data, error } = await client.get({
                        url: `/api/household/${householdId}/lists/${listId}/items/${itemId}/file`,
                        parseAs: "blob",
                    });
                    if (error || !data || !win) {
                        win?.close();
                        return;
                    }
                    const objectUrl = URL.createObjectURL(data as Blob);
                    win.location.href = objectUrl;
                    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
                } catch {
                    win?.close();
                }
            })();
        },
        [householdId, listId],
    );
