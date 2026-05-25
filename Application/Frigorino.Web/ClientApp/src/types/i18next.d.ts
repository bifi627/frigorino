import "i18next";

declare module "i18next" {
    interface CustomTypeOptions {
        defaultNS: "translation";
        resources: {
            translation: {
                admin: Record<string, string>;
                common: Record<string, string>;
                auth: Record<string, string>;
                navigation: Record<string, string>;
                lists: Record<string, string>;
                household: Record<string, string>;
                inventory: Record<string, string>;
                dashboard: Record<string, string>;
            };
        };
    }
}
