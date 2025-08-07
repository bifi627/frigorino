/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateInventoryRequest } from '../models/CreateInventoryRequest';
import type { InventoryDto } from '../models/InventoryDto';
import type { UpdateInventoryRequest } from '../models/UpdateInventoryRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class InventoriesService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @returns InventoryDto OK
     * @throws ApiError
     */
    public getApiHouseholdInventories(
        householdId: number,
    ): CancelablePromise<Array<InventoryDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/Inventories',
            path: {
                'householdId': householdId,
            },
        });
    }
    /**
     * @param householdId
     * @param requestBody
     * @returns InventoryDto OK
     * @throws ApiError
     */
    public postApiHouseholdInventories(
        householdId: number,
        requestBody?: CreateInventoryRequest,
    ): CancelablePromise<InventoryDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/Inventories',
            path: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @returns InventoryDto OK
     * @throws ApiError
     */
    public getApiHouseholdInventories1(
        householdId: number,
        inventoryId: number,
    ): CancelablePromise<InventoryDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/Inventories/{inventoryId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @param requestBody
     * @returns InventoryDto OK
     * @throws ApiError
     */
    public putApiHouseholdInventories(
        householdId: number,
        inventoryId: number,
        requestBody?: UpdateInventoryRequest,
    ): CancelablePromise<InventoryDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/Inventories/{inventoryId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiHouseholdInventories(
        householdId: number,
        inventoryId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/Inventories/{inventoryId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
        });
    }
}
