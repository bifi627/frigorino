import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useUserSettings } from "./useUserSettings";

// Applies the server-persisted language once it loads, so the user's stored choice wins over
// browser detection. No-op when the user has no stored language (null) or it already matches.
export const useApplyPersistedLanguage = () => {
    const { i18n } = useTranslation();
    const { data } = useUserSettings();

    useEffect(() => {
        const lang = data?.language;
        if (lang && i18n.language !== lang) {
            void i18n.changeLanguage(lang);
        }
    }, [data?.language, i18n]);
};
