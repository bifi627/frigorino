import { Delete } from "@mui/icons-material";
import { IconButton, Stack, TextField } from "@mui/material";
import type { ReactNode } from "react";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type { RecipeLinkResponse } from "../../../../lib/api";
import { useUpdateRecipeLink } from "../useUpdateRecipeLink";

const SAVE_DEBOUNCE_MS = 600;

// A valid http(s) URL — mirrors the server-side aggregate check so we can show an inline hint.
const isHttpUrl = (value: string): boolean => {
    const trimmed = value.trim();
    if (!trimmed) return false;
    try {
        const parsed = new URL(trimmed);
        return parsed.protocol === "http:" || parsed.protocol === "https:";
    } catch {
        return false;
    }
};

interface RecipeLinkRowProps {
    householdId: number;
    recipeId: number;
    link: RecipeLinkResponse;
    onDelete: () => void;
    dragHandle: ReactNode;
}

export const RecipeLinkRow = ({
    householdId,
    recipeId,
    link,
    onDelete,
    dragHandle,
}: RecipeLinkRowProps) => {
    const { t } = useTranslation();
    const updateLink = useUpdateRecipeLink();

    const [label, setLabel] = useState(link.label ?? "");
    const [url, setUrl] = useState(link.url);
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    // Latest field state, read by the debounced/blur flush without re-creating the timer.
    const latest = useRef({ label, url });
    useLayoutEffect(() => {
        latest.current = { label, url };
    });

    const { mutate } = updateLink;

    const save = useCallback(() => {
        // Skip the save when the URL is invalid — the server would 400; the inline error guides the
        // user. A blur/flush with a still-invalid URL simply leaves the last good value persisted.
        if (!isHttpUrl(latest.current.url)) return;
        mutate({
            path: { householdId, recipeId, linkId: link.id },
            body: {
                url: latest.current.url.trim(),
                label: latest.current.label.trim() || null,
            },
        });
    }, [mutate, householdId, recipeId, link.id]);

    const scheduleSave = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(save, SAVE_DEBOUNCE_MS);
    }, [save]);

    const flushSave = useCallback(() => {
        if (timerRef.current) {
            clearTimeout(timerRef.current);
            timerRef.current = null;
        }
        save();
    }, [save]);

    useEffect(
        () => () => {
            if (timerRef.current) clearTimeout(timerRef.current);
        },
        [],
    );

    const urlInvalid = url.trim().length > 0 && !isHttpUrl(url);

    return (
        <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "flex-start" }}
            data-testid={`recipe-link-row-${link.id}`}
        >
            {dragHandle}
            <Stack spacing={1} sx={{ flex: 1 }}>
                <TextField
                    label={t("recipes.linkLabel")}
                    value={label}
                    onChange={(e) => {
                        setLabel(e.target.value);
                        scheduleSave();
                    }}
                    onBlur={flushSave}
                    size="small"
                    fullWidth
                    placeholder={t("recipes.linkLabelPlaceholder")}
                    slotProps={{
                        htmlInput: {
                            maxLength: 255,
                            "data-testid": `recipe-link-${link.id}-label-input`,
                        },
                    }}
                />
                <TextField
                    label={t("recipes.linkUrl")}
                    value={url}
                    onChange={(e) => {
                        setUrl(e.target.value);
                        scheduleSave();
                    }}
                    onBlur={flushSave}
                    size="small"
                    fullWidth
                    error={urlInvalid}
                    helperText={
                        urlInvalid ? t("recipes.invalidUrl") : undefined
                    }
                    placeholder={t("recipes.linkUrlPlaceholder")}
                    slotProps={{
                        htmlInput: {
                            maxLength: 2048,
                            "data-testid": `recipe-link-${link.id}-url-input`,
                        },
                    }}
                />
            </Stack>
            <IconButton
                size="small"
                onClick={onDelete}
                data-testid={`recipe-link-${link.id}-delete`}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Stack>
    );
};
