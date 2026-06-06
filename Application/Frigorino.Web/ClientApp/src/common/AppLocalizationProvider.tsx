import { AdapterDateFns } from "@mui/x-date-pickers/AdapterDateFns";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { de, enUS } from "date-fns/locale";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

// Map the active i18next language (normalized to "de"/"en" by load: "languageOnly") to a
// date-fns locale. The locale drives the DatePicker field's mask + display format, so German
// users type/see dd.MM.yyyy and everyone else MM/dd/yyyy. Re-renders on language switch
// because useTranslation subscribes to i18next.
export const AppLocalizationProvider = ({
    children,
}: {
    children: ReactNode;
}) => {
    const { i18n } = useTranslation();
    const adapterLocale = i18n.language.startsWith("de") ? de : enUS;
    return (
        <LocalizationProvider
            dateAdapter={AdapterDateFns}
            adapterLocale={adapterLocale}
        >
            {children}
        </LocalizationProvider>
    );
};
