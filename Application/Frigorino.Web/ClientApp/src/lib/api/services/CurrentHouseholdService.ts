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
    public getApiCurrentHousehold(): CancelablePromise<CurrentHouseholdResponse> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/CurrentHousehold',
        });
    }
    /**
     * @param householdId
     * @returns CurrentHouseholdResponse OK
     * @throws ApiError
     */
    public postApiCurrentHousehold(
        householdId: number,
    ): CancelablePromise<CurrentHouseholdResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/CurrentHousehold/{householdId}',
            path: {
                'householdId': householdId,
            },
        });
    }
}
