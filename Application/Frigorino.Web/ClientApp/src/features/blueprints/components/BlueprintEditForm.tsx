import { Save } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    CardContent,
    Stack,
    TextField,
} from "@mui/material";
import { useRouter } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { SortBlueprintResponse } from "../../../lib/api";
import { useUpdateSortBlueprint } from "../useUpdateSortBlueprint";

interface BlueprintEditFormProps {
    householdId: number;
    blueprint: SortBlueprintResponse;
}

export const BlueprintEditForm = ({
    householdId,
    blueprint,
}: BlueprintEditFormProps) => {
    const { t } = useTranslation();
    const router = useRouter();
    const updateBlueprint = useUpdateSortBlueprint();
    // Seeded once on mount. The parent keys this form by blueprint.id, so switching blueprints
    // remounts and reseeds — no reset-on-prop effect (which would clobber in-progress edits).
    const [editedName, setEditedName] = useState(blueprint.name);

    const isFormValid = editedName.trim().length > 0;
    const isPending = updateBlueprint.isPending;

    const handleSave = () => {
        if (!isFormValid) {
            return;
        }
        // Title-only edit: Update takes both name and categories, so resend the current order.
        updateBlueprint.mutate(
            {
                path: { householdId, blueprintId: blueprint.id },
                body: {
                    name: editedName.trim(),
                    categories: blueprint.categories,
                },
            },
            {
                onSuccess: () => router.history.back(),
                onError: () => toast.error(t("blueprints.saveFailed")),
            },
        );
    };

    const handleCancel = () => router.history.back();

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <Stack spacing={3}>
                    <TextField
                        label={t("blueprints.nameLabel")}
                        value={editedName}
                        onChange={(e) => setEditedName(e.target.value)}
                        fullWidth
                        required
                        error={editedName.trim().length === 0}
                        helperText={
                            editedName.trim().length === 0
                                ? t("blueprints.nameRequired")
                                : ""
                        }
                        slotProps={{
                            htmlInput: {
                                "data-testid": "blueprint-name-input",
                            },
                        }}
                    />

                    <Box
                        sx={{
                            display: "flex",
                            gap: 2,
                            justifyContent: "flex-end",
                        }}
                    >
                        <Button
                            variant="outlined"
                            onClick={handleCancel}
                            disabled={isPending}
                            sx={{ minWidth: 100 }}
                        >
                            {t("common.cancel")}
                        </Button>
                        <Button
                            variant="contained"
                            onClick={handleSave}
                            disabled={isPending || !isFormValid}
                            startIcon={<Save />}
                            data-testid="blueprint-edit-save-button"
                            sx={{ minWidth: 100, fontWeight: 600 }}
                        >
                            {isPending ? t("common.saving") : t("common.save")}
                        </Button>
                    </Box>
                </Stack>
            </CardContent>
        </Card>
    );
};
