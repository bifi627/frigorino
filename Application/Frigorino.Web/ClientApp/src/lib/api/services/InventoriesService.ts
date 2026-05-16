/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateInventoryRequest } from '../models/CreateInventoryRequest';
import type { InventoryResponse } from '../models/InventoryResponse';
import type { UpdateInventoryRequest } from '../models/UpdateInventoryRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class InventoriesService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @param requestBody
     * @returns InventoryResponse Created
     * @throws ApiError
     */
    public createInventory(
        householdId: number,
        requestBody: CreateInventoryRequest,
    ): CancelablePromise<InventoryResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/inventories',
            path: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @returns InventoryResponse OK
     * @throws ApiError
     */
    public getInventories(
        householdId: number,
    ): CancelablePromise<Array<InventoryResponse>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/inventories',
            path: {
                'householdId': householdId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @returns InventoryResponse OK
     * @throws ApiError
     */
    public getInventory(
        householdId: number,
        inventoryId: number,
    ): CancelablePromise<InventoryResponse> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/inventories/{inventoryId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @param requestBody
     * @returns InventoryResponse OK
     * @throws ApiError
     */
    public updateInventory(
        householdId: number,
        inventoryId: number,
        requestBody: UpdateInventoryRequest,
    ): CancelablePromise<InventoryResponse> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/inventories/{inventoryId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                403: `Forbidden`,
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @returns void
     * @throws ApiError
     */
    public deleteInventory(
        householdId: number,
        inventoryId: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/inventories/{inventoryId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
            errors: {
                403: `Forbidden`,
                404: `Not Found`,
            },
        });
    }
}
