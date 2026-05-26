const ONBOARDING_SKIPPED_KEY = "frigorino-onboarding-skipped";

export const getOnboardingSkipped = (): boolean => {
    try {
        return sessionStorage.getItem(ONBOARDING_SKIPPED_KEY) === "true";
    } catch {
        return false;
    }
};

export const setOnboardingSkipped = (): void => {
    try {
        sessionStorage.setItem(ONBOARDING_SKIPPED_KEY, "true");
    } catch {
        // sessionStorage unavailable (e.g. privacy mode) — skip is best-effort.
    }
};
