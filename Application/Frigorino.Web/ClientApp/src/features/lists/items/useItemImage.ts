import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { client } from "../../../lib/api/client.gen";

type Variant = "thumbnail" | "file";

// Fetches an item's image bytes (auth'd, via the configured client). The decoded Blob is what we
// cache (keyed by item id + variant) — NOT an object URL. Each consumer derives its own short-lived
// object URL from that Blob via useMemo and revokes it on its own unmount.
//
// Why the Blob and not the URL: an object URL is an instance-scoped resource. Caching the URL and
// revoking it on unmount meant a synchronous unmount→remount (checking off / reordering an image
// row, which moves it between the unchecked/checked lists) revoked the URL while TanStack still held
// it (staleTime: Infinity), then handed the dead URL back to the remounted observer → broken
// thumbnail. Caching the Blob and creating the URL per-instance avoids sharing a revocable handle.
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
            return data as Blob;
        },
    });

    // Per-consumer object URL. createObjectURL + revokeObjectURL MUST be paired in the same effect:
    // under <StrictMode> the mount→unmount→remount probe runs setup→cleanup→setup, so pairing them
    // guarantees the URL the consumer ends up with is the one created by the surviving setup (a
    // useMemo here is unsafe — its value can outlive the cleanup that revoked it, yielding a dead URL).
    const blob = query.data;
    const [url, setUrl] = useState<string>();
    useEffect(() => {
        if (!blob) {
            setUrl(undefined);
            return;
        }
        const objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
        return () => {
            URL.revokeObjectURL(objectUrl);
        };
    }, [blob]);

    return {
        ...query,
        data: url,
        // Stay "loading" until the object URL for the fetched Blob exists, so consumers show their
        // placeholder (skeleton/spinner) during the one-render gap rather than the broken-image state.
        isLoading: query.isLoading || (!!blob && !url),
    };
};
