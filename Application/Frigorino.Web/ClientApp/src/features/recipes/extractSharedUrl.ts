// Chrome on Android usually drops the shared page URL into `text` (often noisy, e.g.
// "Great recipe https://…"), not `url`. Scan url → text → title for the first http(s) URL.
const HTTP_URL = /\bhttps?:\/\/[^\s]+/i;

export const extractSharedUrl = (params: {
    url?: string;
    text?: string;
    title?: string;
}): string | undefined => {
    for (const candidate of [params.url, params.text, params.title]) {
        if (!candidate) {
            continue;
        }
        const match = candidate.match(HTTP_URL);
        if (match) {
            return match[0];
        }
    }
    return undefined;
};
