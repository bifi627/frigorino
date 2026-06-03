/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside a local presentational component. */
import { AddPhotoAlternate, Description, Image } from "@mui/icons-material";
import {
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
} from "@mui/material";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { defineAction } from "../defineFeature";
import type { ActionContext } from "../types";

// Payload the attach action emits up via onComplete (kind: "attach").
export interface AttachPayload {
    file: File;
}

const AttachTrigger = ({
    complete,
    disabled,
}: ActionContext<AttachPayload>) => {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<null | HTMLElement>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const handlePick = (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        // Reset so picking the same file again re-fires change.
        event.target.value = "";
        if (file) {
            complete({ file });
        }
    };

    return (
        <>
            <IconButton
                onClick={(e) => setAnchor(e.currentTarget)}
                disabled={disabled}
                aria-label={t("lists.attach")}
                data-testid="composer-attach-button"
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <AddPhotoAlternate fontSize="small" />
            </IconButton>

            <Menu
                anchorEl={anchor}
                open={Boolean(anchor)}
                onClose={() => setAnchor(null)}
            >
                <MenuItem
                    data-testid="composer-attach-photo"
                    onClick={() => {
                        setAnchor(null);
                        fileInputRef.current?.click();
                    }}
                >
                    <ListItemIcon>
                        <Image fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("lists.attachPhoto")}</ListItemText>
                </MenuItem>
                {/* Document arrives in sub-feature #3. */}
                <MenuItem disabled data-testid="composer-attach-document">
                    <ListItemIcon>
                        <Description fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("lists.attachDocument")}</ListItemText>
                </MenuItem>
            </Menu>

            <input
                ref={fileInputRef}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                capture="environment"
                hidden
                data-testid="composer-attach-file-input"
                onChange={handlePick}
            />
        </>
    );
};

export const attachComposerFeature = defineAction<"attach", AttachPayload>({
    id: "attach",
    renderTrigger: (ctx) => <AttachTrigger {...ctx} />,
});
