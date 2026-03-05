using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class EditorDirectionUtil
	{
		private static GameObject root;
		private static GameObject yawControls;

		private static float currentYawDegrees = 0f;

		// Total size of the combined control area (centered at gizmo origin)
		private const float TOTAL_WIDTH = 2f;
		private const float TOTAL_DEPTH = 2f;

		// Derived
		private const float HALF_WIDTH = TOTAL_WIDTH * 0.5f;

		// Highlight feedback
		private const float HIGHLIGHT_DURATION = 0.45f;
		private static readonly Color HIGHLIGHT_COLOR = new Color(1f, 0.3f, 0.3f, 1f);

		// Fix for rapid clicks: single shared update + cached originals
		private static readonly Dictionary<GameObject, (Dictionary<Renderer, Color> originals, float startTime)> activeHighlights =
			new Dictionary<GameObject, (Dictionary<Renderer, Color>, float)>();

		public static float CurrentRotation => currentYawDegrees;

		public static void ShowAt(
			Vector3 worldPosition,
			Quaternion initialWorldRotation,
			Camera editorCamera)
		{
			if (!Application.isPlaying) return;
			Hide();

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
				if (TryStartYawDrag(ray, out GameObject clickedArrow))
				{
					inputConsumed = true;
					newWorldRotation = Quaternion.Euler(0f, currentYawDegrees, 0f);

					if (clickedArrow != null)
						HighlightArrow(clickedArrow);
				}
			}

			return inputConsumed;
		}

		public static void Hide()
		{
			activeHighlights.Clear();
			EditorApplication.update -= UpdateAllHighlights;

			if (root != null)
				Object.DestroyImmediate(root);

			root = yawControls = null;
			currentYawDegrees = 0f;
		}

		private static bool TryStartYawDrag(Ray ray, out GameObject clickedArrow)
		{
			clickedArrow = null;

			if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, 1 << LayerMask.NameToLayer("Editor")))
				return false;

			if (!hit.transform.IsChildOf(yawControls.transform))
				return false;

			// Find the arrow root (works even if you hit a child mesh)
			clickedArrow = hit.transform.root.gameObject;
			if (clickedArrow == null || !clickedArrow.name.Contains("_Arrow"))
				clickedArrow = hit.transform.gameObject;

			float localX = clickedArrow.transform.localPosition.x;
			bool isCW = localX > 0;

			float delta = isCW ? -90f : +90f;

			Undo.RecordObject(root, "Snap Direction 90°");

			currentYawDegrees += delta;
			currentYawDegrees = Mathf.Repeat(currentYawDegrees, 360f);

			return true;
		}

		private static void HighlightArrow(GameObject arrowGo)
		{
			if (arrowGo == null) return;

			// Rapid click? Just restart the timer (keeps true original colours)
			if (activeHighlights.ContainsKey(arrowGo))
			{
				var entry = activeHighlights[arrowGo];
				activeHighlights[arrowGo] = (entry.originals, (float)EditorApplication.timeSinceStartup);
				return;
			}

			// First time → capture true original colours
			var originals = new Dictionary<Renderer, Color>();
			Renderer[] renderers = arrowGo.GetComponentsInChildren<Renderer>(true);
			foreach (var rend in renderers)
			{
				if (rend.material != null)
					originals[rend] = rend.material.color;
			}

			activeHighlights[arrowGo] = (originals, (float)EditorApplication.timeSinceStartup);

			// Apply red immediately
			foreach (var rend in renderers)
			{
				if (rend.material != null)
					rend.material.color = HIGHLIGHT_COLOR;
			}

			// Start the single shared update loop (only once)
			if (activeHighlights.Count == 1)
				EditorApplication.update += UpdateAllHighlights;
		}

		private static void UpdateAllHighlights()
		{
			float now = (float)Time.realtimeSinceStartup;
			var toRemove = new List<GameObject>();

			foreach (var kvp in activeHighlights)
			{
				GameObject arrow = kvp.Key;
				var (originals, startTime) = kvp.Value;

				float elapsed = now - startTime;
				float t = Mathf.Clamp01(elapsed / HIGHLIGHT_DURATION);

				Renderer[] renderers = arrow.GetComponentsInChildren<Renderer>(true);

				foreach (var pair in originals)
				{
					Renderer rend = pair.Key;
					if (rend != null && rend.material != null)
						rend.material.color = Color.Lerp(HIGHLIGHT_COLOR, pair.Value, t);
				}

				if (t >= 1f)
					toRemove.Add(arrow);
			}

			foreach (var arrow in toRemove)
				activeHighlights.Remove(arrow);

			if (activeHighlights.Count == 0)
				EditorApplication.update -= UpdateAllHighlights;
		}

		private static GameObject CreateYawControls(Transform parent)
		{
			var container = new GameObject("YawControls");
			container.layer = LayerMask.NameToLayer("Editor");
			container.transform.SetParent(parent, false);

			var cwPrefab = Resources.Load<GameObject>("Geometry/ArrowCW");
			var ccwPrefab = Resources.Load<GameObject>("Geometry/ArrowCCW");

			if (cwPrefab == null) Debug.LogError("Failed to load Resources/Geometry/ArrowCW");
			if (ccwPrefab == null) Debug.LogError("Failed to load Resources/Geometry/ArrowCCW");

			if (cwPrefab != null)
			{
				var cwGo = Object.Instantiate(cwPrefab, container.transform);
				cwGo.name = "CW_Arrow";
				SetLayerRecursively(cwGo, LayerMask.NameToLayer("Editor"));

				cwGo.transform.localPosition = new Vector3(-HALF_WIDTH, 0f, 0f);

				AddMeshCollider(cwGo);
				ForceColliderUpdate(cwGo);
			}

			if (ccwPrefab != null)
			{
				var ccwGo = Object.Instantiate(ccwPrefab, container.transform);
				ccwGo.name = "CCW_Arrow";
				SetLayerRecursively(ccwGo, LayerMask.NameToLayer("Editor"));

				ccwGo.transform.localPosition = new Vector3(+HALF_WIDTH, 0f, 0f);

				AddMeshCollider(ccwGo);
				ForceColliderUpdate(ccwGo);
			}

			return container;
		}

		private static void AddMeshCollider(GameObject go)
		{
			var meshFilter = go.GetComponentInChildren<MeshFilter>(true);
			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				Debug.LogWarning($"No readable MeshFilter on {go.name}. Adding fallback box collider.");
				var fallbackCol = go.AddComponent<BoxCollider>();
				fallbackCol.size = new Vector3(1.5f, 1f, 3f);
				return;
			}

			var meshCol = go.AddComponent<MeshCollider>();
			meshCol.sharedMesh = meshFilter.sharedMesh;
			meshCol.convex = true;
		}

		private static void ForceColliderUpdate(GameObject go)
		{
			Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
			foreach (var col in colliders)
			{
				if (col != null)
				{
					col.enabled = false;
					col.enabled = true;
				}
			}
		}

		private static void SetLayerRecursively(GameObject go, int layer)
		{
			go.layer = layer;
			foreach (Transform child in go.transform)
				SetLayerRecursively(child.gameObject, layer);
		}

		public static void DestroyGizmo() => Hide();
	}
}