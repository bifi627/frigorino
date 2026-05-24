import type { ActionFeature, ModifierFeature } from "./types";

export function defineModifier<const Id extends string, V>(
    feature: Omit<ModifierFeature<Id, V>, "kind">,
): ModifierFeature<Id, V> {
    return { ...feature, kind: "modifier" };
}

export function defineAction<const Id extends string, P>(
    feature: Omit<ActionFeature<Id, P>, "kind">,
): ActionFeature<Id, P> {
    return { ...feature, kind: "action" };
}
