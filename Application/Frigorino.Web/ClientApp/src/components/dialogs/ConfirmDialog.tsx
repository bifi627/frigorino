import {
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
} from "@mui/material";
import type { ReactNode } from "react";

interface ConfirmDialogProps {
    open: boolean;
    onClose: () => void;
    onConfirm: () => void;
    title: ReactNode;
    description?: ReactNode;
    children?: ReactNode;
    confirmLabel: string;
    confirmLabelPending?: string;
    cancelLabel: string;
    confirmColor?: "error" | "primary" | "warning";
    confirmDisabled?: boolean;
    isPending?: boolean;
    confirmTestId?: string;
    cancelTestId?: string;
    maxWidth?: "xs" | "sm" | "md";
}

export const ConfirmDialog = ({
    open,
    onClose,
    onConfirm,
    title,
    description,
    children,
    confirmLabel,
    confirmLabelPending,
    cancelLabel,
    confirmColor = "error",
    confirmDisabled = false,
    isPending = false,
    confirmTestId,
    cancelTestId,
    maxWidth = "sm",
}: ConfirmDialogProps) => {
    return (
        <Dialog open={open} onClose={onClose} maxWidth={maxWidth} fullWidth>
            <DialogTitle sx={{ pb: 1, fontWeight: 600 }}>{title}</DialogTitle>
            <DialogContent>
                {description && (
                    <DialogContentText sx={{ mb: children ? 2 : 0 }}>
                        {description}
                    </DialogContentText>
                )}
                {children}
            </DialogContent>
            <DialogActions sx={{ p: 3, pt: 1 }}>
                <Button
                    data-testid={cancelTestId}
                    onClick={onClose}
                    disabled={isPending}
                >
                    {cancelLabel}
                </Button>
                <Button
                    data-testid={confirmTestId}
                    onClick={onConfirm}
                    color={confirmColor}
                    variant="contained"
                    disabled={isPending || confirmDisabled}
                    sx={{ fontWeight: 600, minWidth: 120 }}
                >
                    {isPending && confirmLabelPending
                        ? confirmLabelPending
                        : confirmLabel}
                </Button>
            </DialogActions>
        </Dialog>
    );
};
