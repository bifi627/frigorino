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
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ListResponse } from "../../../lib/api";
import { useUpdateList } from "../useUpdateList";

interface EditListFormProps {
    householdId: number;
    list: ListResponse;
}

export const EditListForm = ({ householdId, list }: EditListFormProps) => {
    const { t } = useTranslation();
    const router = useRouter();
    const updateListMutation = useUpdateList();
    const [editedName, setEditedName] = useState(list.name || "");

    useEffect(() => {
        setEditedName(list.name || "");
    }, [list.name]);

    const isFormValid = editedName.trim().length > 0;
    const isPending = updateListMutation.isPending;

    const handleSave = () => {
        if (!list.id) return;
        updateListMutation.mutate(
            {
                householdId,
                listId: list.id,
                data: {
                    name: editedName.trim(),
                    description: list.description ?? null,
                },
            },
            {
                onSuccess: () => router.history.back(),
            },
        );
    };

    const handleCancel = () => router.history.back();

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <Stack spacing={3}>
                    <TextField
                        label={t("lists.listName")}
                        value={editedName}
                        onChange={(e) => setEditedName(e.target.value)}
                        fullWidth
                        required
                        error={editedName.trim().length === 0}
                        helperText={
                            editedName.trim().length === 0
                                ? t("lists.listNameRequired")
                                : ""
                        }
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
                            data-testid="list-edit-save-button"
                            sx={{ minWidth: 100, fontWeight: 600 }}
                        >
                            {isPending
                                ? t("common.saving")
                                : t("common.save")}
                        </Button>
                    </Box>
                </Stack>
            </CardContent>
        </Card>
    );
};
