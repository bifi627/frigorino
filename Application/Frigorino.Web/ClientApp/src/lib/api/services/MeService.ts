/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ActiveHouseholdResponse } from '../models/ActiveHouseholdResponse';
import type { SetActiveHouseholdRequest } from '../models/SetActiveHouseholdRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class MeService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns ActiveHouseholdResponse OK
     * @throws ApiError
     */
    public getActiveHousehold(): CancelablePromise<ActiveHouseholdResponse> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/me/active-household',
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param requestBody
     * @returns ActiveHouseholdResponse OK
     * @throws ApiError
     */
    public setActiveHousehold(
        requestBody: SetActiveHouseholdRequest,
    ): CancelablePromise<ActiveHouseholdResponse> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/me/active-household',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                403: `Forbidden`,
            },
        });
    }
}
