import { cp, mkdir, rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const output = resolve(root, "dist");
const client = resolve(output, "client");

await rm(output, { recursive: true, force: true });
await mkdir(resolve(output, "server"), { recursive: true });
await mkdir(client, { recursive: true });

for (const file of [
  "index.html", 
  "styles.css", 
  "script.js", 
  "assets", 
  "robots.txt", 
  "sitemap.xml", 
  "groq-setup.html", 
  "privacy.html", 
  "grammarly-alternative.html"
]) {
  await cp(resolve(root, file), resolve(client, file), { recursive: true });
}

await cp(resolve(root, "worker", "index.js"), resolve(output, "server", "index.js"));
