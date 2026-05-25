import { Send } from "@mui/icons-material";
import { IconButton } from "@mui/material";
import { useTranslation } from "react-i18next";

interface SendButtonProps {
    onClick: () => void;
    disabled: boolean;
    editing: boolean;
    duplicate: boolean;
    title?: string;
}

export const SendButton = ({
    onClick,
    disabled,
    editing,
    duplicate,
    title,
}: SendButtonProps) => {
    const { t } = useTranslation();
    const color = duplicate ? "error" : editing ? "warning" : "primary";
    return (
        <IconButton
            data-testid="autocomplete-input-submit-button"
            onClick={onClick}
            disabled={disabled}
            color={color}
            title={title ?? (editing ? t("common.update") : t("common.add"))}
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
            <Send />
        </IconButton>
    );
};

SendButton.displayName = "SendButton";
