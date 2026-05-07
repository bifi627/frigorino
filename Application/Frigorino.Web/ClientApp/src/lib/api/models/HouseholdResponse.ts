/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { HouseholdRole } from './HouseholdRole';
export type HouseholdResponse = {
    id: number;
    name: string;
    description: string | null;
    createdAt: string;
    updatedAt: string;
    createdByUserId: string;
    currentUserRole: HouseholdRole;
};

