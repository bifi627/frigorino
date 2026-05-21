import { getAuth } from "firebase/auth";
import { client } from "../lib/api/client.gen";

client.setConfig({ baseUrl: "" });

// hey-api's `auth` option only fires when the operation declares a security scheme,
// and ASP.NET Core's AddOpenApi emits no `securitySchemes` / per-operation `security`,
// so we attach the Firebase bearer via a request interceptor instead — runs on every
// call regardless of spec metadata.
client.interceptors.request.use(async (request) => {
    const token = await getAuth().currentUser?.getIdToken();
    if (token) {
        request.headers.set("Authorization", `Bearer ${token}`);
    }
    return request;
});
