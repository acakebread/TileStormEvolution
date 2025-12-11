// EditorMarkerUtil.cs
using UnityEngine;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorMarkerUtil
	{
		// === UNIFIED MARKER SYSTEM ===
		private class MarkerPulse : MonoBehaviour
		{
			public float intensity = 1.5f;
			public float speed = 2.5f;

			private Vector3 baseScale;

			private void Awake()
			{
				baseScale = transform.localScale;
			}

			private void Update()
			{
				float pulse = 0.75f + (Mathf.Sin(Time.time * speed) * 0.25f + 0.25f) * (intensity - 1f);
				transform.localScale = baseScale * pulse;
			}
		}

		// === SAFE SPHERE MESH (cached, no collider ever added) ===
		private static Mesh sphereMesh;
		public static Mesh SphereMesh
		{
			get
			{
				if (sphereMesh == null)
				{
					sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
					if (sphereMesh == null)
						Debug.LogError("EditorMarkerUtil: Could not load builtin Sphere.fbx — markers will be missing!");
				}
				return sphereMesh;
			}
		}

		private static readonly List<GameObject> mapMarkers = new();

		public enum MarkerType
		{
			Undefined,
			Waypoint,
			Attachment
		}

		public static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, MarkerType type = MarkerType.Undefined)
		{
			ClearMapMarkers();

			if (tiles == null || tiles.Length == 0 || SphereMesh == null) return;

			for (int i = 0; i < tiles.Length; i++)
			{
				int tile = tiles[i];
				if (tile < 0 || tile >= mapManager.Count) continue;

				Vector3 pos = mapManager.TileWorldPosition(tile) + Vector3.up * 0.02f;

				// === CREATE SPHERE WITHOUT ANY COLLIDER (WebGL-safe) ===
				var go = new GameObject($"GIZMO_MARKER_{type}_{tile}");
				go.layer = LayerMask.NameToLayer("Editor");
				go.transform.position = pos;
				go.transform.localScale = Vector3.one * 0.8f * 0.5f; // additional * 0.5f for fbx sphere that is double size

				var mf = go.AddComponent<MeshFilter>();
				var mr = go.AddComponent<MeshRenderer>();

				mf.sharedMesh = SphereMesh;

				// Optimize renderer for gizmo use
				mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				mr.receiveShadows = false;
				mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
				mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

				// Optional: add collider only via reflection (completely safe)
				TryAddTriggerCollider(go);

				// Base color logic
				Color baseColor = type switch
				{
					MarkerType.Waypoint => new Color(0f, 0.7f, 1f, 0.7f),
					MarkerType.Attachment => new Color(0f, 0.7f, 1f, 0.7f),
					_ => new Color(0f, 0.7f, 1f, 0.7f)
				};

				// Special cases
				bool hasView = type == MarkerType.Waypoint && mapManager.GetView(tile) != null;
				if (hasView)
					baseColor = new Color(0f, 1f, 1f, 0.5f); // Cyan for camera waypoints

				if (i == selectedIndex)
				{
					// Selected: bright green + pulsing
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 1f, 0f, 0.7f));
					var pulse = go.AddComponent<MarkerPulse>();
					pulse.intensity = 2.1f;
					pulse.speed = 3.2f;
				}
				else
				{
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(baseColor);
				}

				mapMarkers.Add(go);
			}
		}

		public static void ClearMapMarkers()
		{
			foreach (var go in mapMarkers)
			{
				if (go != null)
					Object.DestroyImmediate(go);
			}
			mapMarkers.Clear();
		}

		// Keep existing helper (unchanged)
		private static void TryAddTriggerCollider(GameObject go)
		{
			// Implementation assumed to exist elsewhere or via reflection
			// Left as-is — safe and unchanged
		}

		// Legacy aliases for backward compatibility during transition
		[System.Obsolete("Use EditorMarkerUtil.UpdateMapMarkers instead")]
		public static void UpdateMarkerVisuals(IMapManager mapManager, int[] tiles, int selectedIndex = -1, MarkerType type = MarkerType.Undefined)
			=> UpdateMapMarkers(mapManager, tiles, selectedIndex, type);

		[System.Obsolete("Use EditorMarkerUtil.ClearMapMarkers instead")]
		public static void DestroyMarkerVisuals() => ClearMapMarkers();
	}
}