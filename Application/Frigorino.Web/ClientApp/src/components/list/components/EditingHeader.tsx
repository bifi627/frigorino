import { Close, Edit } from "@mui/icons-material";
import { Box, Chip, IconButton, Typography } from "@mui/material";
import { memo } from "react";
import { useAddInputStyles } from "../hooks/useAddInputStyles";
import type { ListItem } from "../types";

interface EditingHeaderProps {
    editingItem: ListItem;
    onCancel: () => void;
}

export const EditingHeader = memo(
    ({ editingItem, onCancel }: EditingHeaderProps) => {
        const styles = useAddInputStyles({
            isEditing: true,
            hasDuplicate: false,
            hasText: false,
        });

        return (
            <Box sx={styles.editingHeaderStyles}>
                <Box sx={styles.editingHeaderContentStyles}>
                    <Edit fontSize="small" color="warning" />
                    <Typography variant="caption" color="warning.dark">
                        Bearbeiten
                    </Typography>
                    {editingItem.status && (
                        <Chip
                            label="Completed"
                            size="small"
                            color="success"
                            variant="outlined"
                        />
                    )}
                </Box>
                <IconButton
                    size="small"
                    onClick={onCancel}
                    sx={styles.editingHeaderButtonStyles}
                >
                    <Close fontSize="small" />
                </IconButton>
            </Box>
        );
    },
);

EditingHeader.displayName = "EditingHeader";
