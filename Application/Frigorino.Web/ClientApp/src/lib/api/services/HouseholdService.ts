/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateHouseholdRequest } from '../models/CreateHouseholdRequest';
import type { HouseholdDto } from '../models/HouseholdDto';
import type { UpdateHouseholdRequest } from '../models/UpdateHouseholdRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class HouseholdService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns HouseholdDto OK
     * @throws ApiError
     */
    public getApiHousehold(): CancelablePromise<Array<HouseholdDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Household',
        });
    }
    /**
     * @param requestBody
     * @returns HouseholdDto OK
     * @throws ApiError
     */
    public postApiHousehold(
        requestBody?: CreateHouseholdRequest,
    ): CancelablePromise<HouseholdDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Household',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param id
     * @returns HouseholdDto OK
     * @throws ApiError
     */
    public getApiHousehold1(
        id: number,
    ): CancelablePromise<HouseholdDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Household/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @param id
     * @param requestBody
     * @returns HouseholdDto OK
     * @throws ApiError
     */
    public putApiHousehold(
        id: number,
        requestBody?: UpdateHouseholdRequest,
    ): CancelablePromise<HouseholdDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/Household/{id}',
            path: {
                'id': id,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param id
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiHousehold(
        id: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/Household/{id}',
            path: {
                'id': id,
            },
        });
    }
}
