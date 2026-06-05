import { Replay, Send } from "@mui/icons-material";
import { IconButton } from "@mui/material";
import { useTranslation } from "react-i18next";

interface SendButtonProps {
    onClick: () => void;
    disabled: boolean;
    editing: boolean;
    /** Resolvable duplicate (e.g. re-add a completed list item) — shows a restore icon. */
    restore: boolean;
    title?: string;
}

export const SendButton = ({
    onClick,
    disabled,
    editing,
    restore,
    title,
}: SendButtonProps) => {
    const { t } = useTranslation();
    // Red is reserved for expiry status elsewhere; the composer never uses it.
    // Restore and edit both read as the amber "active" accent; plain add is primary.
    const color = editing || restore ? "warning" : "primary";
    const Icon = restore ? Replay : Send;
    const label = restore
        ? t("common.restore")
        : editing
          ? t("common.update")
          : t("common.add");
    return (
        <IconButton
            data-testid="autocomplete-input-submit-button"
            onClick={onClick}
            disabled={disabled}
            color={color}
            title={title ?? label}
            sx={{
                minWidth: 44,
                minHeight: 44,
                bgcolor: disabled ? "transparent" : `${color}.main`,
                color: disabled ? "action.disabled" : "common.white",
                "&:hover": {
                    bgcolor: disabled ? "transparent" : `${color}.dark`,
                },
                "&:disabled": {
                    bgcolor: "transparent",
                    color: "action.disabled",
                },
                transition: "all 0.2s ease",
            }}
        >
            <Icon />
        </IconButton>
    );
};

SendButton.displayName = "SendButton";
