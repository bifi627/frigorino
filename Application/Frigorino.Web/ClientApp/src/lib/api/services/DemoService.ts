/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class DemoService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns string OK
     * @throws ApiError
     */
    public getApiDemo(): CancelablePromise<string> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Demo',
        });
    }
}
