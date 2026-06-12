import {
    Dialog,
    DialogContent,
    DialogTitle,
    List,
    ListItemButton,
    ListItemText,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useApplyBlueprint } from "../useApplyBlueprint";
import { useSortBlueprints } from "../useSortBlueprints";

interface Props {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
}

export function ApplyBlueprintDialog({ open, onClose, householdId, listId }: Props) {
    const { t } = useTranslation();
    const { data: blueprints } = useSortBlueprints(householdId, open && householdId > 0);
    const apply = useApplyBlueprint();

    const handlePick = async (blueprintId: number) => {
        try {
            await apply.mutateAsync({
                path: { householdId, listId },
                body: { blueprintId },
            });
            toast.success(t("blueprints.applied"));
            onClose();
        } catch {
            toast.error(t("blueprints.applyFailed"));
        }
    };

    return (
        <Dialog
            open={open}
            onClose={apply.isPending ? undefined : onClose}
            maxWidth="xs"
            fullWidth
        >
            <DialogTitle>{t("blueprints.pickBlueprint")}</DialogTitle>
            <DialogContent>
                {(blueprints ?? []).length === 0 ? (
                    <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
                        {t("blueprints.noBlueprintsToApply")}
                    </Typography>
                ) : (
                    <List data-testid="apply-blueprint-list">
                        {(blueprints ?? []).map((blueprint) => (
                            <ListItemButton
                                key={blueprint.id}
                                disabled={apply.isPending}
                                onClick={() => handlePick(blueprint.id)}
                                data-testid={`apply-blueprint-${blueprint.id}`}
                            >
                                <ListItemText primary={blueprint.name} />
                            </ListItemButton>
                        ))}
                    </List>
                )}
            </DialogContent>
        </Dialog>
    );
}
