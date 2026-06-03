/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { Clear, StickyNote2 } from "@mui/icons-material";
import { Box, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

// Client mirror of the backend ListItem.CommentMaxLength const (not exported to TS).
// Keep in sync if the backend cap changes.
export const COMMENT_MAX_LENGTH = 500;

export const isCommentValid = (value: string): boolean =>
    value.trim().length <= COMMENT_MAX_LENGTH;

const CommentToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <IconButton
            onClick={toggleOpen}
            aria-label={t("lists.comment")}
            sx={{
                minWidth: 44,
                minHeight: 44,
                color: value.trim() || open ? "primary.main" : "inherit",
            }}
        >
            <StickyNote2 fontSize="small" />
        </IconButton>
    );
};

const CommentChip = ({ value, toggleOpen }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <Chip
            clickable
            onClick={toggleOpen}
            aria-label={`${t("common.edit")} ${t("lists.comment")}`}
            size="small"
            icon={<StickyNote2 fontSize="small" />}
            label={value.trim()}
            sx={{
                minHeight: 32,
                maxWidth: 220,
                "& .MuiChip-label": {
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                },
            }}
        />
    );
};

const CommentPanel = ({ value, setValue, disabled }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    const invalid = !isCommentValid(value);
    return (
        <Box
            sx={{ display: "flex", gap: 1, alignItems: "flex-start", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                multiline
                minRows={1}
                maxRows={4}
                variant="outlined"
                placeholder={t("lists.commentPlaceholder")}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                disabled={disabled}
                error={invalid}
                size="small"
                slotProps={{
                    htmlInput: {
                        maxLength: COMMENT_MAX_LENGTH,
                        "data-testid": "composer-comment",
                    },
                }}
            />
            <IconButton
                onClick={() => setValue("")}
                disabled={disabled || value.trim() === ""}
                title={t("common.clear")}
                aria-label={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const commentComposerFeature = defineModifier({
    id: "comment",
    initial: "",
    isEmpty: (value) => value.trim() === "",
    isValid: isCommentValid,
    renderToggle: (slot) => <CommentToggle {...slot} />,
    renderPanel: (slot) => <CommentPanel {...slot} />,
    renderChip: (slot) => <CommentChip {...slot} />,
});
