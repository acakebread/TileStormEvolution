export default {
	async fetch(request, env, ctx) {
		const origin = new URL(request.url).origin;
		const url = new URL(request.url);

		if (request.method === "OPTIONS")
			return corsResponse(null, 204);

		if (url.pathname === "/" || url.pathname === "/index.json")
			return jsonResponse({
				name: "TileStorm Shared Map Repository",
				manifestUrl: `${origin}/manifest.json`,
				downloadPattern: `${origin}/maps/<fileName>`,
				uploadUrl: `${origin}/upload`
			});

		if (request.method === "GET" && url.pathname === "/manifest.json")
			return buildManifestResponse(env, origin);

		if (request.method === "GET" && url.pathname.startsWith("/maps/"))
			return downloadMap(env, url.pathname.slice("/maps/".length));

		if (request.method === "POST" && url.pathname === "/upload")
			return uploadMap(request, env, origin);

		return jsonResponse({ ok: false, message: "Not found." }, 404);
	}
};

async function buildManifestResponse(env, origin) {
	const listing = await env.MAP_BUCKET.list({ prefix: "maps/" });
	const entries = [];

	for (const object of listing.objects) {
		const head = await env.MAP_BUCKET.head(object.key);
		if (!head)
			continue;

		const fileName = object.key.startsWith("maps/")
			? object.key.slice("maps/".length)
			: object.key;
		const meta = head.customMetadata || {};
		const displayName = meta.mapName || fileName.replace(/\.[^.]+$/, "");

		entries.push({
			id: fileName,
			name: displayName,
			fileName,
			downloadUrl: `${origin}/maps/${encodeURIComponent(fileName)}`,
			contentType: head.httpMetadata?.contentType || "application/octet-stream",
			mapHash: meta.mapHash || "",
			description: meta.description || "",
			updatedUtc: head.uploaded ? head.uploaded.toISOString() : "",
			sizeBytes: head.size || object.size || 0
		});
	}

	entries.sort((a, b) => (b.updatedUtc || "").localeCompare(a.updatedUtc || "") || a.name.localeCompare(b.name));

	return jsonResponse({
		repositoryName: "TileStorm Shared Maps",
		generatedUtc: new Date().toISOString(),
		entries
	});
}

async function downloadMap(env, fileName) {
	fileName = safeFileName(fileName);
	if (!fileName)
		return jsonResponse({ ok: false, message: "Invalid file name." }, 400);

	const object = await env.MAP_BUCKET.get(`maps/${fileName}`);
	if (!object)
		return jsonResponse({ ok: false, message: "Map not found." }, 404);

	return new Response(object.body, {
		status: 200,
		headers: {
			"Content-Type": object.httpMetadata?.contentType || "application/octet-stream",
			"Content-Disposition": `attachment; filename="${fileName}"`,
			...corsHeaders()
		}
	});
}

async function uploadMap(request, env, origin) {
	const expectedKey = env.UPLOAD_KEY || "";
	if (expectedKey) {
		const provided = request.headers.get("X-TileStorm-Upload-Key") || "";
		if (provided !== expectedKey)
			return jsonResponse({ ok: false, message: "Unauthorized." }, 401);
	}

	const fileNameHeader = request.headers.get("X-TileStorm-FileName") || "";
	const mapNameHeader = request.headers.get("X-TileStorm-MapName") || "";
	const mapHashHeader = request.headers.get("X-TileStorm-MapHash") || "";
	const contentType = request.headers.get("Content-Type") || "application/octet-stream";
	const payload = await request.arrayBuffer();
	const safeName = safeFileName(fileNameHeader) || `map_${Date.now()}.bin`;

	await env.MAP_BUCKET.put(`maps/${safeName}`, payload, {
		httpMetadata: {
			contentType
		},
		customMetadata: {
			mapName: mapNameHeader,
			mapHash: mapHashHeader,
			description: ""
		}
	});

	return jsonResponse({
		ok: true,
		message: "Upload completed.",
		entry: {
			id: safeName,
			name: mapNameHeader || safeName,
			fileName: safeName,
			downloadUrl: `${origin}/maps/${encodeURIComponent(safeName)}`,
			contentType,
			mapHash: mapHashHeader,
			updatedUtc: new Date().toISOString(),
			sizeBytes: payload.byteLength
		}
	});
}

function safeFileName(name) {
	if (!name)
		return "";

	return name
		.split(/[\\/]/).pop()
		.replace(/[^a-zA-Z0-9._-]+/g, "_")
		.replace(/_+/g, "_")
		.replace(/^_+|_+$/g, "");
}

function jsonResponse(body, status = 200) {
	return new Response(JSON.stringify(body, null, 2), {
		status,
		headers: {
			"Content-Type": "application/json; charset=utf-8",
			...corsHeaders()
		}
	});
}

function corsResponse(body, status = 200) {
	return new Response(body, {
		status,
		headers: corsHeaders()
	});
}

function corsHeaders() {
	return {
		"Access-Control-Allow-Origin": "*",
		"Access-Control-Allow-Methods": "GET,POST,OPTIONS",
		"Access-Control-Allow-Headers": "Content-Type, X-TileStorm-Upload-Key, X-TileStorm-FileName, X-TileStorm-MapName, X-TileStorm-MapHash"
	};
}
