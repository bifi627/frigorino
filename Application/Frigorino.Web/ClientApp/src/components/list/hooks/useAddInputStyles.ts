import type { SxProps, Theme } from "@mui/material";

interface StyleState {
    isEditing: boolean;
    hasDuplicate: boolean;
    hasText: boolean;
    existingItemStatus?: boolean;
}

export const useAddInputStyles = (state: StyleState) => {
    const { isEditing, hasDuplicate, hasText, existingItemStatus } = state;

    const containerStyles: SxProps<Theme> = {
        width: "100%",
        p: 1,
        bgcolor: "background.paper",
        borderRadius: 2,
        border: "1px solid",
        borderColor: isEditing ? "warning.main" : "primary.200",
        cursor: "text",
        transition: "all 0.3s ease",
        "&:hover": {
            borderColor: isEditing ? "warning.dark" : "primary.main",
            boxShadow: 3,
        },
        "&:focus-within": {
            borderColor: isEditing ? "warning.dark" : "primary.main",
            boxShadow: 3,
        },
    };

    const editingHeaderStyles: SxProps<Theme> = {
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        px: 0.5,
        py: 0.25,
        backgroundColor: "warning.50",
        borderRadius: 1,
        mb: 0.5,
    };

    const editingHeaderContentStyles: SxProps<Theme> = {
        display: "flex",
        alignItems: "center",
        gap: 0.5,
    };

    const editingHeaderButtonStyles: SxProps<Theme> = {
        color: "warning.dark",
        "&:hover": { backgroundColor: "warning.100" },
    };

    const discardButtonStyles: SxProps<Theme> = {
        color: "text.secondary",
        backgroundColor: "action.hover",
        "&:hover": {
            color: "error.main",
            backgroundColor: "error.50",
        },
        transition: "all 0.2s ease",
    };

    const inputContainerStyles: SxProps<Theme> = {
        "& .MuiOutlinedInput-notchedOutline": {
            border: "none",
        },
        "& .MuiInputBase-input": {
            py: 1,
        },
    };

    const textFieldStyles: SxProps<Theme> = {
        "& .MuiOutlinedInput-root": {
            borderRadius: 2,
        },
        mb: isEditing ? 1 : 0,
    };

    const autocompleteStyles: SxProps<Theme> = {
        "& .MuiAutocomplete-popupIndicator": {
            display: "none",
        },
        "& .MuiAutocomplete-clearIndicator": {
            display: "none",
        },
    };

    const getActionButtonColor = () => {
        if (hasDuplicate && existingItemStatus) return "success";
        if (hasDuplicate) return "error";
        if (isEditing) return "warning";
        return "primary";
    };

    const getActionButtonBackgroundColor = () => {
        if (!hasText) return "transparent";
        
        const color = getActionButtonColor();
        return `${color}.main`;
    };

    const getActionButtonHoverBackgroundColor = () => {
        if (!hasText) return "transparent";
        
        const color = getActionButtonColor();
        return `${color}.dark`;
    };

    const actionButtonStyles: SxProps<Theme> = {
        bgcolor: getActionButtonBackgroundColor(),
        color: hasText ? "white" : "action.disabled",
        "&:hover": {
            bgcolor: getActionButtonHoverBackgroundColor(),
        },
        "&:disabled": {
            bgcolor: "transparent",
            color: "action.disabled",
        },
        transition: "all 0.2s ease",
    };

    const getActionButtonTitle = (existingItemText?: string) => {
        if (hasDuplicate && existingItemStatus) {
            return `Uncheck "${existingItemText}"`;
        }
        if (hasDuplicate) return "Item already exists";
        if (isEditing) return "Update item";
        return "Add item";
    };

    return {
        containerStyles,
        editingHeaderStyles,
        editingHeaderContentStyles,
        editingHeaderButtonStyles,
        discardButtonStyles,
        inputContainerStyles,
        textFieldStyles,
        autocompleteStyles,
        actionButtonStyles,
        getActionButtonColor,
        getActionButtonTitle,
    };
};