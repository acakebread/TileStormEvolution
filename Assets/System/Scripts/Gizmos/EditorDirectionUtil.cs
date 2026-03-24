using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class EditorDirectionUtil
	{
		private static GameObject root;
		private static GameObject yawControls;

		private static float currentYawDegrees = 0f;

		private const float TOTAL_WIDTH = 2f;   // X axis span
		private const float TOTAL_DEPTH = 2f;   // Z axis span (for the new perpendicular arrows)
		private const float HALF_WIDTH = TOTAL_WIDTH * 0.5f;
		private const float HALF_DEPTH = TOTAL_DEPTH * 0.5f;

		private const float HIGHLIGHT_DURATION = 0.45f;
		private static readonly Color HIGHLIGHT_COLOR = new Color(1f, 0.3f, 0.3f, 1f);

		private static readonly Dictionary<GameObject, (Dictionary<Renderer, Color> originals, float startTime)>
			activeHighlights = new();

		public static float CurrentRotation => currentYawDegrees;

		public static void ShowAt(Vector3 worldPosition, Quaternion initialWorldRotation, Camera inputCamera)
		{
			Hide();

			root = new GameObject("YAW_DIRECTION_GIZMO");

			int editorLayer = LayerMask.NameToLayer("Editor");
			if (editorLayer == -1) editorLayer = 0;

			root.layer = editorLayer;

			yawControls = CreateYawControls(root.transform, editorLayer);

			root.transform.position = worldPosition;
			root.transform.rotation = Quaternion.identity;

			// Snap initial yaw
			Vector3 euler = initialWorldRotation.eulerAngles;
			currentYawDegrees = Mathf.Round(euler.y / 90f) * 90f;
			currentYawDegrees = Mathf.Repeat(currentYawDegrees, 360f);
		}

		public static void UpdateTransform(Vector3 worldPosition, Quaternion worldRotation)
		{
			if (root == null) return;

			root.transform.position = worldPosition;
			root.transform.rotation = Quaternion.identity;
		}

		public static bool HandleInput(Camera inputCamera, out Quaternion newWorldRotation)
		{
			newWorldRotation = Quaternion.Euler(0f, currentYawDegrees, 0f);

			if (root == null || inputCamera == null) return false;

			bool consumed = false;

			UpdateAllHighlights();

			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);

				if (TryStartYawDrag(ray, out GameObject clickedArrow))
				{
					consumed = true;
					newWorldRotation = Quaternion.Euler(0f, currentYawDegrees, 0f);

					if (clickedArrow != null)
						HighlightArrow(clickedArrow);
				}
			}

			return consumed;
		}

		public static void Hide()
		{
			activeHighlights.Clear();

			if (root != null)
				Object.Destroy(root);

			root = null;
			yawControls = null;
			currentYawDegrees = 0f;
		}

		public static void DestroyGizmo() => Hide();

		private static bool TryStartYawDrag(Ray ray, out GameObject clickedArrow)
		{
			clickedArrow = null;

			int layerMask = 1 << (LayerMask.NameToLayer("Editor") != -1 ? LayerMask.NameToLayer("Editor") : 0);

			if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
				return false;

			if (yawControls == null || !hit.transform.IsChildOf(yawControls.transform))
				return false;

			Transform t = hit.transform;
			while (t != null && t.parent != yawControls.transform)
				t = t.parent;

			if (t == null) return false;

			clickedArrow = t.gameObject;

			// ────────────────────────────────────────────────
			// Direction logic – after 180° rotation + prefab swap intent
			// ────────────────────────────────────────────────
			Vector3 localPos = clickedArrow.transform.localPosition;

			float delta = Mathf.Abs(localPos.x) > 0.1f ? +90f : -90f;

			currentYawDegrees = Mathf.Repeat(currentYawDegrees + delta, 360f);

			return true;
		}

		private static void HighlightArrow(GameObject arrowGo)
		{
			if (arrowGo == null) return;

			if (activeHighlights.TryGetValue(arrowGo, out var existing))
			{
				activeHighlights[arrowGo] = (existing.originals, Time.realtimeSinceStartup);
				return;
			}

			var originals = new Dictionary<Renderer, Color>();
			Renderer[] rends = arrowGo.GetComponentsInChildren<Renderer>(true);

			foreach (var r in rends)
				if (r.material != null)
					originals[r] = r.material.color;

			activeHighlights[arrowGo] = (originals, Time.realtimeSinceStartup);

			foreach (var r in rends)
				if (r.material != null)
					r.material.color = HIGHLIGHT_COLOR;
		}

		private static void UpdateAllHighlights()
		{
			float now = Time.realtimeSinceStartup;
			var toRemove = new List<GameObject>();

			foreach (var kvp in activeHighlights)
			{
				var arrow = kvp.Key;
				var (originals, start) = kvp.Value;

				float t = Mathf.Clamp01((now - start) / HIGHLIGHT_DURATION);

				foreach (var pair in originals)
				{
					if (pair.Key != null && pair.Key.material != null)
						pair.Key.material.color = Color.Lerp(HIGHLIGHT_COLOR, pair.Value, t);
				}

				if (t >= 1f)
					toRemove.Add(arrow);
			}

			foreach (var go in toRemove)
				activeHighlights.Remove(go);
		}

		private static GameObject CreateYawControls(Transform parent, int layer)
		{
			var container = new GameObject("YawControls");
			container.layer = layer;
			container.transform.SetParent(parent, false);

			// Apply 180° rotation to the whole controls container
			container.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
			container.transform.localPosition = Vector3.up * 0.2f;

			// ────────────────────────────────────────────────
			// Original X-axis arrows (now flipped by 180°)
			// ────────────────────────────────────────────────
			var cwPrefab = Resources.Load<GameObject>("Geometry/arrow_ccw");
			var ccwPrefab = Resources.Load<GameObject>("Geometry/arrow_cw");

			//var cwPrefab = Resources.Load<GameObject>("Geometry/ArrowCW");
			//var ccwPrefab = Resources.Load<GameObject>("Geometry/ArrowCCW");

			if (cwPrefab == null) Debug.LogError("Missing: Resources/Geometry/arrow_ccw");
			if (ccwPrefab == null) Debug.LogError("Missing: Resources/Geometry/arrow_cw");

			// After 180° container rotation + your "swap CCW to CW" intent:
			// Put CW prefab where CCW used to be (and vice versa)
			if (cwPrefab != null)
			{
				var inst = Object.Instantiate(cwPrefab, container.transform);
				inst.name = "NegativeX_CW_Arrow";           // now at +X after 180°
				inst.transform.localPosition = new Vector3(-HALF_WIDTH, 0f, 0f);
				inst.transform.localRotation = Quaternion.identity;
				SetLayerRecursively(inst, layer);
				AddCollider(inst);

				var inst180 = Object.Instantiate(cwPrefab, container.transform);
				inst180.name = "PositiveX_CW_Arrow";          // now at -X after 180°
				inst180.transform.localPosition = new Vector3(+HALF_WIDTH, 0f, 0f);
				inst180.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
				SetLayerRecursively(inst180, layer);
				AddCollider(inst180);
			}

			// ────────────────────────────────────────────────
			// New perpendicular arrows — both CCW, rotated 90° / 270°
			// ────────────────────────────────────────────────
			if (ccwPrefab != null) // reusing CCW prefab for both
			{
				// 90° → pointing along +Z
				var inst90 = Object.Instantiate(ccwPrefab, container.transform);
				inst90.name = "PositiveZ_CCW_Arrow";
				inst90.transform.localPosition = new Vector3(0f, 0f, +HALF_DEPTH);
				inst90.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
				SetLayerRecursively(inst90, layer);
				AddCollider(inst90);

				// 270° → pointing along -Z
				var inst270 = Object.Instantiate(ccwPrefab, container.transform);
				inst270.name = "NegativeZ_CCW_Arrow";
				inst270.transform.localPosition = new Vector3(0f, 0f, -HALF_DEPTH);
				inst270.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
				SetLayerRecursively(inst270, layer);
				AddCollider(inst270);
			}

			return container;
		}

		private static void AddCollider(GameObject go)
		{
			var mf = go.GetComponentInChildren<MeshFilter>(true);
			if (mf == null || mf.sharedMesh == null)
			{
				Debug.LogWarning($"No mesh on {go.name} → fallback BoxCollider");
				var bc = go.AddComponent<BoxCollider>();
				bc.size = new Vector3(1.5f, 1.2f, 3f);
				return;
			}

			var mc = go.AddComponent<MeshCollider>();
			mc.sharedMesh = mf.sharedMesh;
			mc.convex = true;
		}

		private static void SetLayerRecursively(GameObject go, int layer)
		{
			go.layer = layer;
			foreach (Transform child in go.transform)
				SetLayerRecursively(child.gameObject, layer);
		}
	}
}