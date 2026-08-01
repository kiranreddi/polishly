/** @typedef {{ ASSETS: Fetcher }} Env */

export default {
  /** @param {Request} request @param {Env} env */
  async fetch(request, env) {
    const asset = await env.ASSETS.fetch(request);

    if (asset.status !== 404 || request.method !== "GET") {
      return asset;
    }

    const url = new URL(request.url);
    if (!url.pathname.endsWith("/") && !url.pathname.split("/").pop()?.includes(".")) {
      const cleanUrl = new URL(`${url.pathname}.html`, request.url);
      const cleanAsset = await env.ASSETS.fetch(new Request(cleanUrl, request));
      if (cleanAsset.status !== 404) {
        return cleanAsset;
      }
    }

    return env.ASSETS.fetch(new Request(new URL("/index.html", request.url), request));
  },
};
