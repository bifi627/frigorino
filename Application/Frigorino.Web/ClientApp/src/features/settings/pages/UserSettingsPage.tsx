import {
    Alert,
    Card,
    CardContent,
    Container,
    FormControlLabel,
    MenuItem,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
    disablePush,
    enablePush,
    isIosNeedingInstall,
    pushSupported,
} from "../../../common/pushNotifications";
import { pageContainerSx } from "../../../theme";
import { useUpdateUserNotificationSettings } from "../useUpdateUserNotificationSettings";
import { useUpdateUserSettings } from "../useUpdateUserSettings";
import { useUserSettings } from "../useUserSettings";

const LANGUAGES = [
    { code: "en", label: "English" },
    { code: "de", label: "Deutsch" },
];

export function UserSettingsPage() {
    const { t, i18n } = useTranslation();
    const { data, isLoading } = useUserSettings();
    const updateSettings = useUpdateUserSettings();
    const updateNotifications = useUpdateUserNotificationSettings();
    const [enabled, setEnabled] = useState(false);
    const [leadDays, setLeadDays] = useState("3");
    const [supported, setSupported] = useState(true);
    const [iosHint, setIosHint] = useState(false);

    const currentLanguage = data?.language ?? i18n.language;

    useEffect(() => {
        if (data) {
            setEnabled(data.expiryNotificationsEnabled);
            setLeadDays(String(data.expiryLeadDays));
        }
    }, [data]);

    useEffect(() => {
        pushSupported().then(setSupported);
        setIosHint(isIosNeedingInstall());
    }, []);

    const handleLanguageChange = async (language: string) => {
        try {
            await updateSettings.mutateAsync({ body: { language } });
            await i18n.changeLanguage(language);
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    const persistNotifications = async (
        nextEnabled: boolean,
        nextLeadDays: number,
    ) => {
        try {
            await updateNotifications.mutateAsync({
                body: {
                    expiryNotificationsEnabled: nextEnabled,
                    expiryLeadDays: nextLeadDays,
                },
            });
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    const handleToggleNotifications = async (checked: boolean) => {
        if (checked) {
            const ok = await enablePush();
            if (!ok) {
                toast.error(t("settings.notificationsPermissionDenied"));
                return;
            }
        } else {
            await disablePush();
        }
        setEnabled(checked);
        await persistNotifications(checked, Number(leadDays));
    };

    const handleLeadDaysBlur = async () => {
        const days = Number(leadDays);
        if (!Number.isInteger(days) || days < 0 || days > 365) {
            return;
        }
        if (data && data.expiryLeadDays === days) {
            return;
        }
        await persistNotifications(enabled, days);
    };

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Typography variant="h5" sx={{ mb: { xs: 2, sm: 3 } }}>
                {t("settings.userSettings")}
            </Typography>

            <Card elevation={2}>
                <CardContent>
                    <TextField
                        select
                        fullWidth
                        size="small"
                        data-testid="settings-language-select"
                        label={t("settings.language")}
                        helperText={t("settings.languageHelp")}
                        value={currentLanguage}
                        disabled={isLoading || updateSettings.isPending}
                        onChange={(e) => handleLanguageChange(e.target.value)}
                        slotProps={{
                            htmlInput: {
                                "data-testid": "settings-language-value",
                            },
                        }}
                    >
                        {LANGUAGES.map((lang) => (
                            <MenuItem
                                key={lang.code}
                                value={lang.code}
                                data-testid={`settings-language-option-${lang.code}`}
                            >
                                {lang.label}
                            </MenuItem>
                        ))}
                    </TextField>
                </CardContent>
            </Card>

            <Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
                <CardContent>
                    <Typography variant="h6" sx={{ mb: 1 }}>
                        {t("settings.notifications")}
                    </Typography>

                    {iosHint && (
                        <Alert
                            severity="info"
                            sx={{ mb: 2 }}
                            data-testid="settings-ios-install-hint"
                        >
                            {t("settings.notificationsIosHint")}
                        </Alert>
                    )}

                    <FormControlLabel
                        control={
                            <Switch
                                data-testid="settings-notifications-switch"
                                checked={enabled}
                                disabled={
                                    !supported || updateNotifications.isPending
                                }
                                onChange={(e) =>
                                    handleToggleNotifications(e.target.checked)
                                }
                            />
                        }
                        label={t("settings.notificationsEnable")}
                    />

                    {enabled && (
                        <TextField
                            type="number"
                            fullWidth
                            size="small"
                            sx={{ mt: 1 }}
                            label={t("settings.notificationsLeadDays")}
                            helperText={t("settings.notificationsLeadHelp")}
                            value={leadDays}
                            disabled={updateNotifications.isPending}
                            onChange={(e) => setLeadDays(e.target.value)}
                            onBlur={handleLeadDaysBlur}
                            slotProps={{
                                htmlInput: {
                                    min: 0,
                                    max: 365,
                                    "data-testid":
                                        "settings-notifications-lead-input",
                                },
                            }}
                        />
                    )}
                </CardContent>
            </Card>
        </Container>
    );
}
