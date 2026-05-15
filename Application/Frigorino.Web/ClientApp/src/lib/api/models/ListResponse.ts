/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ListCreatorResponse } from './ListCreatorResponse';
export type ListResponse = {
    id: number;
    name: string;
    description: string | null;
    householdId: number;
    createdAt: string;
    updatedAt: string;
    createdByUser: ListCreatorResponse;
    uncheckedCount: number;
    checkedCount: number;
};

