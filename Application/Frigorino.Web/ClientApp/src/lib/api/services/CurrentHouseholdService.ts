/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CurrentHouseholdResponse } from '../models/CurrentHouseholdResponse';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class CurrentHouseholdService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns CurrentHouseholdResponse OK
     * @throws ApiError
     */
    public getCurrentHousehold(): CancelablePromise<CurrentHouseholdResponse> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/currenthousehold',
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @returns CurrentHouseholdResponse OK
     * @throws ApiError
     */
    public setCurrentHousehold(
        householdId: number,
    ): CancelablePromise<CurrentHouseholdResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/currenthousehold/{householdId}',
            path: {
                'householdId': householdId,
            },
            errors: {
                403: `Forbidden`,
            },
        });
    }
}
