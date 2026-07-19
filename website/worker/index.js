/** @typedef {{ ASSETS: Fetcher }} Env */

export default {
  /** @param {Request} request @param {Env} env */
  async fetch(request, env) {
    const asset = await env.ASSETS.fetch(request);

    if (asset.status !== 404 || request.method !== "GET") {
      return asset;
    }

    return env.ASSETS.fetch(new Request(new URL("/index.html", request.url), request));
  },
};
