import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { client } from "../../../lib/api/client.gen";

type Variant = "thumbnail" | "file";

// Fetches an item's image bytes (auth'd, via the configured client) as an object URL.
// Cached by item id + variant; the URL is revoked on unmount / when the query is cleaned up.
export const useItemImage = (
    householdId: number,
    listId: number,
    itemId: number,
    variant: Variant,
    enabled = true,
) => {
    const query = useQuery({
        queryKey: ["item-image", householdId, listId, itemId, variant],
        enabled: enabled && householdId > 0 && listId > 0 && itemId > 0,
        staleTime: Infinity,
        gcTime: 5 * 60 * 1000,
        queryFn: async () => {
            const { data, error } = await client.get({
                url: `/api/household/${householdId}/lists/${listId}/items/${itemId}/${variant}`,
                parseAs: "blob",
            });
            if (error || !data) {
                throw new Error("Failed to load image");
            }
            return URL.createObjectURL(data as Blob);
        },
    });

    // Revoke the object URL when this consumer unmounts or the URL changes.
    useEffect(() => {
        const url = query.data;
        return () => {
            if (url) {
                URL.revokeObjectURL(url);
            }
        };
    }, [query.data]);

    return query;
};
