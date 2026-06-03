/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside a local presentational component. */
import {
    AddPhotoAlternate,
    Description,
    Image,
    PhotoCamera,
} from "@mui/icons-material";
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

    // Drive the single hidden input two ways: with `capture` set, mobile opens the camera; with it
    // cleared, the file/gallery picker. Toggling the attribute imperatively right before click()
    // avoids Android Chrome's quirk where a no-capture input with specific MIME types shows only the
    // file browser and never offers the camera.
    const openPicker = (useCamera: boolean) => {
        setAnchor(null);
        const input = fileInputRef.current;
        if (!input) {
            return;
        }
        if (useCamera) {
            input.setAttribute("capture", "environment");
        } else {
            input.removeAttribute("capture");
        }
        input.click();
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
                    data-testid="composer-attach-camera"
                    onClick={() => openPicker(true)}
                >
                    <ListItemIcon>
                        <PhotoCamera fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("lists.takePhoto")}</ListItemText>
                </MenuItem>
                <MenuItem
                    data-testid="composer-attach-photo"
                    onClick={() => openPicker(false)}
                >
                    <ListItemIcon>
                        <Image fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("lists.choosePhoto")}</ListItemText>
                </MenuItem>
                {/* Document arrives in sub-feature #3. */}
                <MenuItem disabled data-testid="composer-attach-document">
                    <ListItemIcon>
                        <Description fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("lists.attachDocument")}</ListItemText>
                </MenuItem>
            </Menu>

            {/* `capture` is set/cleared imperatively in openPicker() before .click(); no static
                attribute here so the default (file/gallery picker) applies. */}
            <input
                ref={fileInputRef}
                type="file"
                accept="image/jpeg,image/png,image/webp"
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
