using UnityEngine;
using System;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class DefinitionIconRenderUtil
	{
		/// <summary>
		/// Renders a single Definition to a Texture2D using a real Camera + command-buffer system.
		/// Returns null if model is invalid or no meshes are generated.
		/// </summary>
		public static Texture2D GenerateIcon(
			Definition definition,
			int size = 128,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f,
			bool includeGround = false)
		{
			if (definition == null || string.IsNullOrEmpty(definition.model))
			{
				Debug.LogWarning("No definition or model name provided.");
				return null;
			}
			
			var root = new GameObject("IconRenderTemp") { hideFlags = HideFlags.HideAndDontSave };

			try
			{
				var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
				{
					antiAliasing = 4,
					filterMode = FilterMode.Bilinear,
					autoGenerateMips = false,
					name = $"IconRT_{definition.name ?? "def"}"
				};
				rt.Create();

				var camObj = new GameObject("IconCamera") { hideFlags = HideFlags.HideAndDontSave };
				camObj.transform.SetParent(root.transform, false);

				var cam = camObj.AddComponent<Camera>();
				cam.enabled = false;
				cam.clearFlags = CameraClearFlags.SolidColor;
				cam.backgroundColor = background ?? new Color(0, 0, 0, 0);
				cam.fieldOfView = 50f;
				cam.nearClipPlane = 0.03f;
				cam.farClipPlane = 50f;
				cam.targetTexture = rt;
				cam.aspect = 1f;
				cam.cullingMask = 0;

				var scene = new CommandRenderScene();
				var hook = camObj.AddComponent<CommandCameraHook>();
				hook.Provider = scene;

				// ─── Optional ground plane ───────────────────────────────────────
				CommandRenderModelData ground = null;
				if (includeGround)
				{
					var quadMesh = MeshUtils.GenerateQuadXZ(3f, 1f, "IconGround");
					var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
					{
						hideFlags = HideFlags.HideAndDontSave,
						color = Color.white
					};
					groundMat.SetTexture("_BaseMap", Texture2D.whiteTexture);

					ground = new CommandRenderModelData(quadMesh, new[] { groundMat }, Matrix4x4.identity);
				}

				var modelData = RenderModelFactory.Create(definition, Vector3.zero, Quaternion.identity, Vector3.one);
				if (modelData == null || modelData.meshInstances.Count == 0)
				{
					Debug.LogWarning($"No mesh instances for definition: {definition.name}");
					return null;
				}

				if (ground != null)
				{
					float lowestY = modelData.bounds.min.y;
					ground = new CommandRenderModelData(
						ground.meshInstances[0].mesh,
						ground.meshInstances[0].materials,
						Matrix4x4.Translate(Vector3.up * (lowestY - 0.02f)));
				}

				var allModels = ground != null ? new[] { ground, modelData } : new[] { modelData };
				scene.SetModels(allModels);

				// Isometric camera positioning
				var rot = Quaternion.Euler(pitch, yaw, 0f);
				var center = modelData.bounds.center;
				var radius = modelData.bounds.extents.magnitude * 1.2f;
				var dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

				cam.transform.position = center - rot * Vector3.forward * dist;
				cam.transform.rotation = rot;

				cam.Render();

				var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
				{
					filterMode = FilterMode.Bilinear,
					wrapMode = TextureWrapMode.Clamp,
					name = $"Icon_{definition.name ?? "unnamed"}"
				};

				var prev = RenderTexture.active;
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
				tex.Apply();
				RenderTexture.active = prev;

				rt.Release();           // ← important
				return tex;
			}
			catch (Exception ex)
			{
				Debug.LogError($"Icon render failed for {definition?.name}: {ex}");
				return null;
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(root);
			}
		}

		public static IconAtlas CreateIconAtlas(
			int iconSize = 64,
			int columns = 16,
			bool includeGround = false,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f)
		{
			return new IconAtlas(
				iconSize,
				columns,
				ResourceManager.Definitions,
				includeGround,
				background,
				yaw,
				pitch);
		}

		//// ────────────────────────────────────────────────────────────────
		////           NEW - Atlas generation moved here
		//// ────────────────────────────────────────────────────────────────

		//public static Texture2D GenerateIconAtlas(
		//	int iconSize = 64,
		//	int columns = 16,
		//	bool includeGround = false,
		//	Color? background = null,
		//	float yaw = 35f,
		//	float pitch = 30f)
		//{
		//	var defs = ResourceManager.Definitions;
		//	if (defs == null || defs.Count == 0)
		//	{
		//		Debug.LogWarning("No definitions available for atlas generation.");
		//		return null;
		//	}

		//	// Remove null / invalid entries early
		//	var validDefs = defs.Where(d => d != null && !string.IsNullOrEmpty(d.model)).ToList();
		//	if (validDefs.Count == 0) return null;

		//	int rows = Mathf.CeilToInt((float)validDefs.Count / columns);

		//	int atlasWidth = columns * iconSize;
		//	int atlasHeight = rows * iconSize;

		//	var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false)
		//	{
		//		filterMode = FilterMode.Point,     // crisp pixel icons
		//		wrapMode = TextureWrapMode.Clamp,
		//		name = "DefinitionIconAtlas"
		//	};

		//	// Clear to transparent
		//	Color[] pixels = new Color[atlasWidth * atlasHeight];
		//	Array.Fill(pixels, new Color(0, 0, 0, 0));
		//	atlas.SetPixels(pixels);

		//	for (int i = 0; i < validDefs.Count; i++)
		//	{
		//		var def = validDefs[i];

		//		var icon = GenerateIcon(
		//			def,
		//			size: iconSize,
		//			background: background,
		//			yaw: yaw,
		//			pitch: pitch,
		//			includeGround: includeGround);

		//		if (icon == null) continue;

		//		int col = i % columns;
		//		int row = i / columns;

		//		// Bottom-left origin → flip Y
		//		int x = col * iconSize;
		//		int y = (rows - 1 - row) * iconSize;

		//		atlas.SetPixels(x, y, iconSize, iconSize, icon.GetPixels());
		//	}

		//	atlas.Apply(true, false);
		//	Debug.Log($"Generated icon atlas: {atlasWidth}×{atlasHeight}, {validDefs.Count} icons");

		//	return atlas;
		//}
	}
}
