import { cp, mkdir, rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const output = resolve(root, "dist");

await rm(output, { recursive: true, force: true });
await mkdir(resolve(output, "server"), { recursive: true });

for (const file of ["index.html", "styles.css", "script.js", "assets"]) {
  await cp(resolve(root, file), resolve(output, file), { recursive: true });
}

await cp(resolve(root, "worker", "index.js"), resolve(output, "server", "index.js"));
