# Shared Map Repository Setup

This repository feature uses a small HTTPS API so the WebGL build can browse and import shared maps directly.

## What the player sees

- Open the shared map panel.
- Wait for the repository list to load.
- Select a map.
- Click `Import Selected`, or click `Import Now` on the row.

## What the publisher does

- Build the map normally.
- Open the shared map panel in a build that has the upload key configured.
- Click `Publish Current`.

## Recommended backend shape

The app expects these endpoints:

- `GET /manifest.json`
- `GET /maps/<fileName>`
- `POST /upload`

The sample worker in `Backend/cloudflare-worker.js` implements exactly that shape.

## Cloudflare R2 + Worker approach

Use this if you want a small hosted repository with very little infrastructure:

1. Create a Cloudflare account.
2. Create an R2 bucket for the map files.
3. Create a Worker and attach the sample script from `Backend/cloudflare-worker.js`.
4. Bind the R2 bucket into the Worker as `MAP_BUCKET`.
5. Add a secret named `UPLOAD_KEY`.
6. Deploy the Worker and give it a public route, for example `https://maps.example.com/*`.
7. Confirm `https://maps.example.com/manifest.json` returns JSON.
8. Confirm `https://maps.example.com/maps/<file>` downloads a package.

## Build settings to fill in

In `ApplicationSettings`:

- `Map Repository Base Url`: `https://maps.example.com`
- `Map Repository Upload Key`: the same key you stored as the Worker secret

The WebGL build only needs the base URL to browse and download. The upload key is only needed if you want the build to publish maps too.

## File format

The repository can store the atomic export files your app already generates:

- `.json`
- `.zip`

The manifest simply points at those files.

## CORS

The backend must allow browser requests from the WebGL origin. The sample worker sends permissive CORS headers so the browser can read the manifest and download files.

