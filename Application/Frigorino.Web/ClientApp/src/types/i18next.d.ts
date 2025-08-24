import "i18next";

declare module "i18next" {
    interface CustomTypeOptions {
        defaultNS: "translation";
        resources: {
            translation: {
                common: Record<string, string>;
                auth: Record<string, string>;
                navigation: Record<string, string>;
                lists: Record<string, string>;
                household: Record<string, string>;
                inventory: Record<string, string>;
            };
        };
    }
}
