import {
    Alert,
    Card,
    CardContent,
    FormControlLabel,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useUserSettings } from "../../settings/useUserSettings";
import { useInventorySettings } from "../useInventorySettings";
import { useUpdateInventorySettings } from "../useUpdateInventorySettings";

interface Props {
    householdId: number;
    inventoryId: number;
    canManage: boolean;
}

export function InventorySettingsCard({
    householdId,
    inventoryId,
    canManage,
}: Props) {
    const { t } = useTranslation();
    const { data } = useInventorySettings(householdId, inventoryId);
    const { data: userSettings } = useUserSettings();
    const globalNotificationsEnabled =
        userSettings?.expiryNotificationsEnabled ?? false;
    const updateSettings = useUpdateInventorySettings();
    const [notificationsEnabled, setNotificationsEnabled] = useState(true);
    const [override, setOverride] = useState(false);
    const [value, setValue] = useState("7");

    useEffect(() => {
        if (data) {
            setNotificationsEnabled(data.expiryNotificationsEnabled);
            setOverride(data.expiryLeadDays !== null);
            if (data.expiryLeadDays !== null) {
                setValue(String(data.expiryLeadDays));
            }
        }
    }, [data]);

    const save = async (
        expiryNotificationsEnabled: boolean,
        leadDays: number | null,
    ) => {
        try {
            await updateSettings.mutateAsync({
                path: { householdId, inventoryId },
                body: { expiryNotificationsEnabled, expiryLeadDays: leadDays },
            });
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    const handleNotificationsToggle = async (checked: boolean) => {
        setNotificationsEnabled(checked);
        await save(checked, override ? Number(value) : null);
    };

    const handleToggle = async (checked: boolean) => {
        setOverride(checked);
        await save(notificationsEnabled, checked ? Number(value) : null);
    };

    const handleBlur = async () => {
        const days = Number(value);
        if (!override || !Number.isInteger(days) || days < 0) {
            return;
        }
        if (data && data.expiryLeadDays === days) {
            return;
        }
        await save(notificationsEnabled, days);
    };

    return (
        <Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
            <CardContent>
                <Typography variant="h6" sx={{ mb: 1 }}>
                    {t("settings.inventorySettings")}
                </Typography>
                {!globalNotificationsEnabled && (
                    <Alert
                        severity="info"
                        sx={{ mb: 2 }}
                        data-testid="inventory-notifications-global-off-hint"
                    >
                        {t("settings.inventoryNotificationsRequiresGlobal")}
                    </Alert>
                )}
                <FormControlLabel
                    control={
                        <Switch
                            data-testid="inventory-notifications-switch"
                            checked={notificationsEnabled}
                            disabled={!canManage || updateSettings.isPending}
                            onChange={(e) =>
                                handleNotificationsToggle(e.target.checked)
                            }
                        />
                    }
                    label={t("settings.inventoryNotificationsEnable")}
                />
                <FormControlLabel
                    control={
                        <Switch
                            data-testid="inventory-expiry-override-switch"
                            checked={override}
                            disabled={!canManage || updateSettings.isPending}
                            onChange={(e) => handleToggle(e.target.checked)}
                        />
                    }
                    label={t("settings.expiryLeadOverride")}
                />
                {override && (
                    <TextField
                        type="number"
                        fullWidth
                        size="small"
                        sx={{ mt: 1 }}
                        label={t("settings.expiryLeadDays")}
                        helperText={t("settings.expiryLeadHelp")}
                        value={value}
                        disabled={!canManage || updateSettings.isPending}
                        onChange={(e) => setValue(e.target.value)}
                        onBlur={handleBlur}
                        slotProps={{
                            htmlInput: {
                                min: 0,
                                max: 365,
                                "data-testid": "inventory-expiry-lead-input",
                            },
                        }}
                    />
                )}
            </CardContent>
        </Card>
    );
}
