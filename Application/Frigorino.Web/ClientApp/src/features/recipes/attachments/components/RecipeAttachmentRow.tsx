import { BrokenImage, Delete } from "@mui/icons-material";
import { Box, IconButton, Skeleton, Stack, TextField } from "@mui/material";
import type { ReactNode } from "react";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useAttachmentImage } from "../useAttachmentImage";
import { useUpdateRecipeAttachment } from "../useUpdateRecipeAttachment";

const SAVE_DEBOUNCE_MS = 600;
const THUMB_SIZE = 56;

interface RecipeAttachmentRowProps {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onDelete: () => void;
    dragHandle: ReactNode;
}

export const RecipeAttachmentRow = ({
    householdId,
    recipeId,
    attachment,
    onDelete,
    dragHandle,
}: RecipeAttachmentRowProps) => {
    const { t } = useTranslation();
    const updateAttachment = useUpdateRecipeAttachment();

    const [caption, setCaption] = useState(attachment.caption ?? "");
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const latest = useRef({ caption });
    useLayoutEffect(() => {
        latest.current = { caption };
    });

    const { mutate } = updateAttachment;

    const save = useCallback(() => {
        mutate({
            path: { householdId, recipeId, attachmentId: attachment.id },
            body: { caption: latest.current.caption.trim() || null },
        });
    }, [mutate, householdId, recipeId, attachment.id]);

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

    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(householdId, recipeId, attachment.id, "thumbnail");

    return (
        <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "center" }}
            data-testid={`recipe-attachment-row-${attachment.id}`}
        >
            {dragHandle}
            <Box
                sx={{
                    width: THUMB_SIZE,
                    height: THUMB_SIZE,
                    flexShrink: 0,
                    borderRadius: 1,
                    overflow: "hidden",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                }}
            >
                {isLoading ? (
                    <Skeleton
                        variant="rectangular"
                        width={THUMB_SIZE}
                        height={THUMB_SIZE}
                    />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={attachment.caption ?? ""}
                        sx={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                        }}
                    />
                )}
            </Box>
            <TextField
                label={t("recipes.attachmentCaption")}
                value={caption}
                onChange={(e) => {
                    setCaption(e.target.value);
                    scheduleSave();
                }}
                onBlur={flushSave}
                size="small"
                fullWidth
                placeholder={t("recipes.attachmentCaptionPlaceholder")}
                slotProps={{
                    htmlInput: {
                        maxLength: 255,
                        "data-testid": `recipe-attachment-${attachment.id}-caption-input`,
                    },
                }}
            />
            <IconButton
                size="small"
                onClick={onDelete}
                data-testid={`recipe-attachment-${attachment.id}-delete`}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Stack>
    );
};
