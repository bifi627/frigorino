import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { ProductCatalogPage } from "../../features/products/pages/ProductCatalogPage";

export const Route = createFileRoute("/household/products")({
    beforeLoad: requireAuth,
    component: ProductCatalogPage,
});
