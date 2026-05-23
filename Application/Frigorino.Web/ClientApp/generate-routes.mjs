// Regenerates src/routeTree.gen.ts using the same options as the vite plugin
// in vite.config.ts (target + autoCodeSplitting). Kept as a standalone entry so
// CI can regenerate and diff the committed tree without booting vite — the vite
// plugin only regenerates on route-file changes, so a dependency bump that
// changes generator behaviour would otherwise silently ship a stale tree.
import { Generator, getConfig } from "@tanstack/router-generator";

const root = process.cwd();
const config = getConfig({ target: "react", autoCodeSplitting: true }, root);
await new Generator({ config, root }).run();
