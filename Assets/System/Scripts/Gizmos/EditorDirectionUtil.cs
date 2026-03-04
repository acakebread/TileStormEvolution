using UnityEngine;
using UnityEditor;

namespace MassiveHadronLtd
{
	public static class EditorDirectionUtil
	{
		private static GameObject root;
		private static GameObject yawControls;

		private static float currentYawDegrees = 0f;

		// Total size of the combined control area (centered at gizmo origin)
		private const float TOTAL_WIDTH = 2f;     // x: -1 to +1
		private const float TOTAL_DEPTH = 2f;     // z: -1 to +1

		// Derived
		private const float HALF_WIDTH = TOTAL_WIDTH * 0.5f;   // = 1f
		private const float FULL_DEPTH = TOTAL_DEPTH;          // = 2f

		// Lazy-loaded internal textures
		private static Texture2D ccwTexInternal;
		private static Texture2D cwTexInternal;
		private static bool texturesInitialized = false;

		public static void ShowAt(
			Vector3 worldPosition,
			Quaternion initialWorldRotation,
			Camera editorCamera)
		{
			if (!Application.isPlaying) return;
			Hide();

			LazyLoadTextures();

			root = new GameObject("YAW_DIRECTION_GIZMO");
			root.layer = LayerMask.NameToLayer("Editor");

			yawControls = CreateYawControls(root.transform);

			root.transform.position = worldPosition;
			root.transform.rotation = Quaternion.identity;

			Vector3 euler = initialWorldRotation.eulerAngles;
			currentYawDegrees = Mathf.Round(euler.y / 90f) * 90f;
			currentYawDegrees = Mathf.Repeat(currentYawDegrees, 360f);
		}

		public static void UpdateTransform(
			Vector3 worldPosition,
			Quaternion worldRotation,
			Camera editorCamera)
		{
			if (!Application.isPlaying) return;

			if (root == null)
			{
				ShowAt(worldPosition, worldRotation, editorCamera);
				return;
			}

			root.transform.position = worldPosition;
			root.transform.rotation = Quaternion.identity;
		}

		public static bool HandleInput(Camera editorCamera, out Quaternion newWorldRotation)
		{
			newWorldRotation = Quaternion.Euler(0f, currentYawDegrees, 0f);

			if (root == null || editorCamera == null || !Application.isPlaying)
				return false;

			bool inputConsumed = false;

			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
				if (TryStartYawDrag(ray))
				{
					inputConsumed = true;
					newWorldRotation = Quaternion.Euler(0f, currentYawDegrees, 0f);
				}
			}

			return inputConsumed;
		}

		public static void Hide()
		{
			if (root != null)
				Object.DestroyImmediate(root);

			root = yawControls = null;
			currentYawDegrees = 0f;
		}

		private static void LazyLoadTextures()
		{
			if (texturesInitialized) return;

			ccwTexInternal = Resources.Load<Texture2D>("Textures/CCW_Arrow");
			cwTexInternal = Resources.Load<Texture2D>("Textures/CW_Arrow");

			if (ccwTexInternal == null) Debug.LogWarning("Could not load Resources/Textures/CCW_Arrow — using fallback color.");
			if (cwTexInternal == null) Debug.LogWarning("Could not load Resources/Textures/CW_Arrow — using fallback color.");

			texturesInitialized = true;
		}

		private static bool TryStartYawDrag(Ray ray)
		{
			if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, 1 << LayerMask.NameToLayer("Editor")))
				return false;

			if (!hit.transform.IsChildOf(yawControls.transform))
				return false;

			float localX = hit.transform.localPosition.x;
			bool isCW = localX > 0;

			float delta = isCW ? -90f : +90f;

			Undo.RecordObject(root, "Snap Direction 90°");

			currentYawDegrees += delta;
			currentYawDegrees = Mathf.Repeat(currentYawDegrees, 360f);

			return true;
		}

		private static GameObject CreateYawControls(Transform parent)
		{
			var container = new GameObject("YawControls");
			container.layer = LayerMask.NameToLayer("Editor");
			container.transform.SetParent(parent, false);

			var fallbackMat = new Material(
				Shader.Find("Universal Render Pipeline/Unlit") ??
				Shader.Find("Unlit/Color") ??
				Shader.Find("Legacy Shaders/Transparent/Diffuse")
			);
			fallbackMat.color = new Color(0.92f, 0.92f, 0.96f, 0.55f);

			// Left half: CW
			CreateHalfControl(container.transform, "CW", cwTexInternal, fallbackMat, xCenter: -0.5f);

			// Right half: CCW, explicitly placed and sized
			CreateHalfControl(container.transform, "CCW", ccwTexInternal, fallbackMat, xCenter: +0.5f);

			return container;
		}

		private static void CreateHalfControl(Transform parent, string namePrefix, Texture2D customTex, Material fallbackMat, float xCenter)
		{
			var go = new GameObject($"{namePrefix}_Control");
			go.layer = LayerMask.NameToLayer("Editor");
			go.transform.SetParent(parent, false);

			// Center the control GameObject at the middle of its half
			go.transform.localPosition = new Vector3(xCenter, 0f, 0f);

			// Create quad as child
			var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
			quad.name = "Quad";
			quad.layer = LayerMask.NameToLayer("Editor");
			quad.transform.SetParent(go.transform, false);

			// Reset local transform for clarity
			quad.transform.localPosition = Vector3.zero;
			quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lay flat on XZ

			// Scale: width = 1 (x), depth = 2 (z)
			quad.transform.localScale = new Vector3(1f, 2f, 1f);

			var mr = quad.GetComponent<MeshRenderer>();

			Material mat;
			if (customTex != null)
			{
				// Prefer transparent-capable Unlit
				Shader transparentShader = Shader.Find("Universal Render Pipeline/Unlit Transparent") ??  // if you have a custom one
										   Shader.Find("Universal Render Pipeline/Unlit") ??
										   Shader.Find("Unlit/Transparent") ??
										   Shader.Find("Legacy Shaders/Transparent/Diffuse");

				mat = new Material(transparentShader);

				mat.mainTexture = customTex;
				mat.color = Color.white;

				// IMPORTANT: Force transparent rendering
				mat.SetFloat("_Surface", 1f);          // 0 = Opaque, 1 = Transparent
				mat.SetFloat("_Blend", 0f);            // 0 = Alpha Blend (most common for textures)
				mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
				mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
				mat.SetInt("_ZWrite", 0);              // usually off for transparent
				mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
			}
			else
			{
				mat = new Material(fallbackMat);
			}

			mr.material = mat;

			// Collider matches visual size + margin
			var col = go.AddComponent<BoxCollider>();
			col.size = new Vector3(1.08f, 0.12f, 2.08f);
			col.center = Vector3.zero;
		}

		public static void DestroyGizmo() => Hide();
	}
}