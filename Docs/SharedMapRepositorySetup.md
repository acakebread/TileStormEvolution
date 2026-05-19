# Shared Map Repository Setup

This feature uses a GitHub Pages repository as the public read layer, and the GitHub REST API as the publish layer.

That gives you the simplest player experience for WebGL:

- open the panel
- see the current map list
- click a map
- import it

Creators can also publish maps back to the same repository from inside the build if an upload token is configured.

## What the player sees

- Open the shared map panel.
- Wait for the repository list to load.
- Select a map.
- Click `Import Selected`, or click `Import Now` on the row.

## What the publisher does

- Build the map normally.
- Open the shared map panel in a build that has the upload token configured.
- Click `Publish Current`.

The app commits the exported map file into the repo, then updates `manifest.json` so the new map appears in the browser list.

## Recommended repository layout

Use a dedicated public repository for maps if possible.

Inside that repository:

- `manifest.json` at the repository root
- `maps/` folder containing exported map packages

Example manifest:

```json
{
  "repositoryName": "TileStorm Shared Maps",
  "generatedUtc": "2026-05-19T00:00:00Z",
  "entries": []
}
```

## Step 1: Create or choose the repository

If you already have a GitHub Pages site, you can reuse it.

For the cleanest setup, create a dedicated public repository such as:

- `yourname.github.io` for a user site
- or a separate public repo like `tilestorm-maps`

## Step 2: Enable GitHub Pages

1. Open the repository on GitHub.
2. Go to `Settings`.
3. Select `Pages`.
4. Under `Build and deployment`, choose `Deploy from a branch`.
5. Select the branch you want to publish from, usually `main`.
6. Select the folder, usually `/ (root)`.
7. Save.

GitHub Pages will then publish the static files that live in the repository.

## Step 3: Add the shared map files

Create these items in the repository:

- `manifest.json`
- `maps/` directory

If you do it through the GitHub web UI:

1. Open the repository.
2. Click `Add file` > `Create new file`.
3. Name the file `manifest.json`.
4. Paste the JSON example above.
5. Commit the file.
6. Create the `maps/` folder by adding a file inside it, for example `maps/.gitkeep`, then commit.

## Step 4: Create an upload token

The app publishes through the GitHub API, so you need a token with write access to the repository.

1. Open GitHub `Settings`.
2. Go to `Developer settings`.
3. Open `Personal access tokens`.
4. Choose `Fine-grained tokens`.
5. Click `Generate new token`.
6. Restrict the token to the one repository you want to use.
7. Grant these permissions:
   - `Contents`: Read and write
   - `Metadata`: Read-only
8. Generate the token and copy it somewhere safe.

For initial tests, this token can be stored in the build settings. For anything public-facing later, you would normally move this server-side or use a better publishing pipeline.

## Step 5: Configure TileStorm

In `ApplicationSettings`:

- `Map Repository Base Url`: the GitHub Pages URL for the repo
  - user site example: `https://yourname.github.io`
  - project site example: `https://yourname.github.io/tilestorm-maps`
- `Map Repository Upload Key`: your GitHub token

Optional fields:

- `Map Repository GitHub Repository`: only needed if the Pages URL is not enough to infer the repo
- `Map Repository GitHub Branch`: only needed if you publish from a branch other than `main`

If you are using a normal `github.io` Pages URL, the app can usually infer the repository automatically.

## Step 6: Test the public read path

Open these URLs in a browser:

- `https://your-pages-url/manifest.json`
- `https://your-pages-url/maps/<fileName>`

If the manifest loads and the map file downloads, the read side is ready.

## Step 7: Test publishing

In a build with the upload token configured:

1. Load a map.
2. Open the shared map panel.
3. Click `Publish Current`.
4. Wait for the repo commit to finish.
5. Refresh the browser list.

If everything is wired correctly, the new map should now appear in the manifest and become available to all players after the Pages site updates.

## CORS

GitHub’s REST API supports CORS requests from browser clients, so the WebGL build can talk to it directly.

The published Pages site itself is just static files, so the player browser can read `manifest.json` and the map packages directly from GitHub Pages.

## Troubleshooting

- `manifest.json` returns 404
  - Make sure the file exists in the publishing branch and that GitHub Pages is enabled for the correct source.

- Upload fails with authentication errors
  - Check that the token is valid and has `Contents: Read and write` on the correct repository.

- The new map does not appear in the browser
  - Confirm the app updated `manifest.json` in the repo.
  - Wait for GitHub Pages to rebuild, then refresh the shared panel.

- The repo URL is not inferred correctly
  - Fill in `Map Repository GitHub Repository` manually as `owner/repository`.

