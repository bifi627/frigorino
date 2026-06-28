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
            // Strip trailing sentence punctuation the greedy [^\s]+ swallows from text like
            // "Great recipe (https://site/r)." — else the URL 404s server-side.
            return match[0].replace(/[).,;!?'"]+$/, "");
        }
    }
    return undefined;
};
