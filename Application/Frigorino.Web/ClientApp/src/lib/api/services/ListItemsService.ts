/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateListItemRequest } from '../models/CreateListItemRequest';
import type { ListItemDto } from '../models/ListItemDto';
import type { ReorderItemRequest } from '../models/ReorderItemRequest';
import type { UpdateListItemRequest } from '../models/UpdateListItemRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class ListItemsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @param listId
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public getApiHouseholdListsListItems(
        householdId: number,
        listId: number,
    ): CancelablePromise<Array<ListItemDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/lists/{listId}/ListItems',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param requestBody
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public postApiHouseholdListsListItems(
        householdId: number,
        listId: number,
        requestBody?: CreateListItemRequest,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/lists/{listId}/ListItems',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public getApiHouseholdListsListItems1(
        householdId: number,
        listId: number,
        itemId: number,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/lists/{listId}/ListItems/{itemId}',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @param requestBody
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public putApiHouseholdListsListItems(
        householdId: number,
        listId: number,
        itemId: number,
        requestBody?: UpdateListItemRequest,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/lists/{listId}/ListItems/{itemId}',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiHouseholdListsListItems(
        householdId: number,
        listId: number,
        itemId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/lists/{listId}/ListItems/{itemId}',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public patchApiHouseholdListsListItemsToggleStatus(
        householdId: number,
        listId: number,
        itemId: number,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/household/{householdId}/lists/{listId}/ListItems/{itemId}/toggle-status',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @param requestBody
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public patchApiHouseholdListsListItemsReorder(
        householdId: number,
        listId: number,
        itemId: number,
        requestBody?: ReorderItemRequest,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/household/{householdId}/lists/{listId}/ListItems/{itemId}/reorder',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param listId
     * @returns any OK
     * @throws ApiError
     */
    public postApiHouseholdListsListItemsCompact(
        householdId: number,
        listId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/lists/{listId}/ListItems/compact',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
        });
    }
}
