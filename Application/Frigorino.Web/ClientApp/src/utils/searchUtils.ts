/**
 * Case-insensitive substring match for the in-view item search.
 * An empty or whitespace-only query matches everything (no filter).
 */
export const matchesQuery = (text: string, query: string): boolean => {
    const trimmed = query.trim().toLowerCase();
    if (trimmed.length === 0) {
        return true;
    }
    return text.toLowerCase().includes(trimmed);
};
