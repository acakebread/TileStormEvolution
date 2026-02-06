using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class DefinitionIconRenderUtil
	{
		/// <summary>
		/// Renders a Definition to a Texture2D using a real Camera + your command-buffer system.
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

			// Temporary root object for cleanup
			var root = new GameObject("IconRenderTemp") { hideFlags = HideFlags.HideAndDontSave };

			try
			{
				// 1. Create RenderTexture
				var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
				{
					antiAliasing = 4,
					filterMode = FilterMode.Bilinear,
					autoGenerateMips = false,
					name = $"IconRT_{definition.name ?? "def"}"
				};
				rt.Create();

				// 2. Create disabled camera
				var camObj = new GameObject("IconCamera") { hideFlags = HideFlags.HideAndDontSave };
				camObj.transform.SetParent(root.transform, false);

				var cam = camObj.AddComponent<Camera>();
				cam.enabled = false;
				cam.clearFlags = CameraClearFlags.SolidColor;
				cam.backgroundColor = background ?? new Color(0, 0, 0, 0); //new Color(0.129f, 0.698f, 0.882f);
				cam.fieldOfView = 50f;
				cam.nearClipPlane = 0.03f;
				cam.farClipPlane = 50f;
				cam.targetTexture = rt;
				cam.aspect = 1f;
				cam.cullingMask = 0; // safe for manual draw calls

				// 3. Hook your command provider (same as editor)
				var scene = new CommandRenderScene();
				var hook = camObj.AddComponent<CommandCameraHook>();
				hook.Provider = scene;

				// 4. Optional ground plane
				CommandRenderModelData ground = null;
				if (includeGround)
				{
					var quadMesh = MeshUtils.GenerateQuadXZ(3f, 1f, "IconGround");
					var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
					{
						hideFlags = HideFlags.HideAndDontSave
					};
					groundMat.color = Color.white;
					groundMat.SetTexture("_BaseMap", Texture2D.whiteTexture); // or your checker/xor

					ground = new CommandRenderModelData(quadMesh, new[] { groundMat }, Matrix4x4.identity);
				}

				// 5. Model data
				var modelData = RenderModelFactory.Create(definition, Vector3.zero, Quaternion.identity, Vector3.one);
				if (modelData == null || modelData.meshInstances.Count == 0)
				{
					Debug.LogWarning($"No mesh instances created for definition: {definition.name}");
					return null;
				}

				// Adjust ground height based on model bottom
				if (ground != null)
				{
					float lowestY = modelData.bounds.min.y;
					ground = new CommandRenderModelData(
						ground.meshInstances[0].mesh,
						ground.meshInstances[0].materials,
						Matrix4x4.Translate(Vector3.up * (lowestY - 0.02f)));
				}

				// Combine models
				var allModels = ground != null
					? new[] { ground, modelData }
					: new[] { modelData };

				scene.SetModels(allModels);

				// 6. Set camera pose (simple isometric view)
				var rot = Quaternion.Euler(pitch, yaw, 0f);
				var center = modelData.bounds.center;
				var radius = modelData.bounds.extents.magnitude * 1.2f;
				var dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

				cam.transform.position = center - rot * Vector3.forward * dist;
				cam.transform.rotation = rot;

				// 7. Render
				cam.Render();

				// 8. Read back to Texture2D
				var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
				{
					filterMode = FilterMode.Bilinear,
					wrapMode = TextureWrapMode.Clamp,
					name = $"Icon_{definition.name ?? "unnamed"}"
				};

				var prevActive = RenderTexture.active;
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
				tex.Apply();
				RenderTexture.active = prevActive;

				return tex;
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Icon render failed for {definition?.name}: {ex.Message}\n{ex.StackTrace}");
				return null;
			}
			finally
			{
				//// Immediate cleanup
				//if (rt != null)
				//{
				//	rt.Release();
				//	Object.DestroyImmediate(rt);
				//}
				Object.DestroyImmediate(root);
			}
		}
	}
}