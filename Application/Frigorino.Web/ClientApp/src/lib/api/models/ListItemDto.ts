/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ClassificationCategory } from './ClassificationCategory';
export type ListItemDto = {
    id?: number;
    listId?: number;
    text?: string | null;
    quantity?: string | null;
    status?: boolean;
    sortOrder?: number;
    createdAt?: string;
    updatedAt?: string;
    category?: ClassificationCategory;
    expirationDuration?: number | null;
    hintEstimation?: string | null;
    hintCategory?: string | null;
};

