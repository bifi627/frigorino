import { getAuth } from "firebase/auth";
import { OpenAPI, type OpenAPIConfig } from "../lib/api";
import { FrigorinoApiClient } from "../lib/api/FrigorinoApiClient";

export const getApiConfig = (token = "") => {
    const apiConfig: OpenAPIConfig = { ...OpenAPI, TOKEN: token };
    return apiConfig;
};

export const getClientApiConfig = () => {
    const apiConfig: OpenAPIConfig = {
        ...OpenAPI,
        TOKEN: async () => (await getAuth().currentUser?.getIdToken()) ?? "",
    };
    return apiConfig;
};

export const ClientApi = new FrigorinoApiClient(getClientApiConfig());
