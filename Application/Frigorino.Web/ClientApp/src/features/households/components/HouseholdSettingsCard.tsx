import { Card, CardContent, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useHouseholdSettings } from "../useHouseholdSettings";
import { useUpdateHouseholdSettings } from "../useUpdateHouseholdSettings";

interface Props {
    householdId: number;
    canManage: boolean;
}

export function HouseholdSettingsCard({ householdId, canManage }: Props) {
    const { t } = useTranslation();
    const { data } = useHouseholdSettings(householdId);
    const updateSettings = useUpdateHouseholdSettings();
    const [value, setValue] = useState("");

    useEffect(() => {
        if (data) {
            setValue(String(data.checkedItemRetentionDays));
        }
    }, [data]);

    const commit = async () => {
        const days = Number(value);
        if (!Number.isInteger(days) || days < 1) {
            return;
        }
        if (data && days === data.checkedItemRetentionDays) {
            return;
        }
        try {
            await updateSettings.mutateAsync({
                path: { householdId },
                body: { checkedItemRetentionDays: days },
            });
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    return (
        <Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
            <CardContent>
                <Typography variant="h6" sx={{ mb: 2 }}>
                    {t("settings.householdSettings")}
                </Typography>
                <TextField
                    type="number"
                    fullWidth
                    size="small"
                    label={t("settings.checkedItemRetentionDays")}
                    helperText={
                        canManage
                            ? t("settings.checkedItemRetentionHelp")
                            : t("settings.readOnlyHint")
                    }
                    value={value}
                    disabled={!canManage || updateSettings.isPending}
                    onChange={(e) => setValue(e.target.value)}
                    onBlur={commit}
                    slotProps={{
                        htmlInput: {
                            min: 1,
                            max: 365,
                            "data-testid": "household-retention-input",
                        },
                    }}
                />
            </CardContent>
        </Card>
    );
}
