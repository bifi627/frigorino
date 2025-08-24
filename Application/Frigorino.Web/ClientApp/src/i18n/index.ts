import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import HttpBackend from "i18next-http-backend";
import { initReactI18next } from "react-i18next";

// Define default namespace for type safety
export const defaultNS = "translation";

i18n.use(HttpBackend)
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
        fallbackLng: "en",
        debug: process.env.NODE_ENV === "development",

        interpolation: {
            escapeValue: false, // React already does escaping
        },

        // React suspense configuration
        react: {
            useSuspense: true,
        },

        // Language detection
        detection: {
            order: ["localStorage", "navigator", "htmlTag"],
            lookupLocalStorage: "i18nextLng",
            caches: ["localStorage"],
        },

        // HTTP backend configuration
        backend: {
            loadPath: "/locales/{{lng}}/translation.json",
        },

        ns: ["translation"],
        defaultNS,
    });

export default i18n;
