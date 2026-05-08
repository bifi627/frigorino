/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateHouseholdRequest } from '../models/CreateHouseholdRequest';
import type { HouseholdResponse } from '../models/HouseholdResponse';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class HouseholdsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param requestBody
     * @returns HouseholdResponse Created
     * @throws ApiError
     */
    public createHousehold(
        requestBody: CreateHouseholdRequest,
    ): CancelablePromise<HouseholdResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
            },
        });
    }
    /**
     * @returns HouseholdResponse OK
     * @throws ApiError
     */
    public getUserHouseholds(): CancelablePromise<Array<HouseholdResponse>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household',
        });
    }
    /**
     * @param id
     * @returns void
     * @throws ApiError
     */
    public deleteHousehold(
        id: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{id}',
            path: {
                'id': id,
            },
            errors: {
                403: `Forbidden`,
                404: `Not Found`,
            },
        });
    }
}
