import { getAuth } from "firebase/auth";
import { client } from "../lib/api/client.gen";

// Single client.setConfig replaces the old FrigorinoApiClient instance + OpenAPIConfig.TOKEN
// resolver. The `client` singleton is the one every generated SDK function imports
// internally — configuring it here propagates to all call sites at module load.
client.setConfig({
    baseUrl: "",
    auth: async () =>
        (await getAuth().currentUser?.getIdToken()) ?? "",
});
