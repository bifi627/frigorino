import { Box } from "@mui/material";
import type { ReactNode } from "react";

/**
 * Splits `label` around every case-insensitive occurrence of `query` and bolds
 * the matched runs while muting the rest. Used by the suggestion dropdown so the
 * typed term is highlighted wherever it appears — start, middle, or end — to
 * match the "contains" search in useItemComposer. Returns the plain label when
 * the query is empty.
 */
export function highlightMatch(label: string, query: string): ReactNode {
    const needle = query.trim();
    if (!needle) {
        return label;
    }
    const lowerLabel = label.toLowerCase();
    const lowerNeedle = needle.toLowerCase();
    const parts: ReactNode[] = [];
    let cursor = 0;
    let key = 0;
    let index = lowerLabel.indexOf(lowerNeedle);
    while (index !== -1) {
        if (index > cursor) {
            parts.push(
                <Box component="span" key={key++} sx={{ color: "text.secondary" }}>
                    {label.slice(cursor, index)}
                </Box>,
            );
        }
        parts.push(
            <Box
                component="span"
                key={key++}
                sx={{ color: "text.primary", fontWeight: 600 }}
            >
                {label.slice(index, index + needle.length)}
            </Box>,
        );
        cursor = index + needle.length;
        index = lowerLabel.indexOf(lowerNeedle, cursor);
    }
    if (cursor < label.length) {
        parts.push(
            <Box component="span" key={key++} sx={{ color: "text.secondary" }}>
                {label.slice(cursor)}
            </Box>,
        );
    }
    return parts;
}
