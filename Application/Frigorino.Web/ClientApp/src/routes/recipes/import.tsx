import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { extractSharedUrl } from "../../features/recipes/extractSharedUrl";
import { ImportRecipePage } from "../../features/recipes/pages/ImportRecipePage";

type ImportSearch = {
    sharedUrl?: string;
};

export const Route = createFileRoute("/recipes/import")({
    beforeLoad: requireAuth,
    validateSearch: (search: Record<string, unknown>): ImportSearch => ({
        sharedUrl: extractSharedUrl({
            url: typeof search.url === "string" ? search.url : undefined,
            text: typeof search.text === "string" ? search.text : undefined,
            title: typeof search.title === "string" ? search.title : undefined,
        }),
    }),
    component: () => (
        <RequireHousehold>
            <ImportRecipePage />
        </RequireHousehold>
    ),
});
