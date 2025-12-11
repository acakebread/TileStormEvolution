// EditorMarkerUtil.cs
using UnityEngine;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorMarkerUtil
	{
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

		// === SAFE SPHERE MESH ===
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

		// ===================================================================
		// NEW DECOUPLED API — world positions + optional colors
		// ===================================================================
		public static void ShowMarkers(Vector3[] worldPositions, Color[] colors = null, int selectedIndex = -1)
		{
			ClearMapMarkers();

			if (worldPositions == null || worldPositions.Length == 0 || SphereMesh == null) return;

			for (int i = 0; i < worldPositions.Length; i++)
			{
				Vector3 pos = worldPositions[i];

				var go = new GameObject($"GIZMO_MARKER_{i}");
				go.layer = LayerMask.NameToLayer("Editor");
				go.transform.position = pos + Vector3.up * 0.02f;
				go.transform.localScale = Vector3.one * 0.8f * 0.5f;

				var mf = go.AddComponent<MeshFilter>();
				var mr = go.AddComponent<MeshRenderer>();
				mf.sharedMesh = SphereMesh;

				mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				mr.receiveShadows = false;
				mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
				mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

				TryAddTriggerCollider(go);

				Color baseColor = (colors != null && i < colors.Length) ? colors[i] : new Color(0f, 0.7f, 1f, 0.7f);

				if (i == selectedIndex)
				{
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

		private static void TryAddTriggerCollider(GameObject go)
		{
			var colliderType = System.Type.GetType("UnityEngine.SphereCollider, UnityEngine");
			if (colliderType != null)
			{
				var col = go.AddComponent(colliderType) as Collider;
				if (col != null)
					col.isTrigger = true;
			}
		}

		//// ===================================================================
		//// TEMPORARY WRAPPER — preserves exact old behavior (including GetView check)
		//// ===================================================================
		//[System.Obsolete("Use EditorMarkerUtil.ShowMarkers(worldPositions, colors, selectedIndex) instead. Will be removed later.")]
		//public static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, MarkerType type = MarkerType.Undefined)
		//{
		//	if (tiles == null || tiles.Length == 0 || SphereMesh == null)
		//	{
		//		ClearMapMarkers();
		//		return;
		//	}

		//	var positions = new Vector3[tiles.Length];
		//	var colors = new Color[tiles.Length];

		//	for (int i = 0; i < tiles.Length; i++)
		//	{
		//		int tile = tiles[i];
		//		if (tile < 0 || tile >= mapManager.Count)
		//		{
		//			positions[i] = Vector3.zero;
		//			colors[i] = new Color(0f, 0.7f, 1f, 0.7f);
		//			continue;
		//		}

		//		positions[i] = mapManager.TileWorldPosition(tile);

		//		bool hasView = type == MarkerType.Waypoint && mapManager.GetView(tile) != null;
		//		colors[i] = hasView ? new Color(0f, 1f, 1f, 0.5f) : new Color(0f, 0.7f, 1f, 0.7f);
		//	}

		//	ShowMarkers(positions, colors, selectedIndex);
		//}

		//// Legacy aliases
		//[System.Obsolete("Use EditorMarkerUtil.ShowMarkers instead")]
		//public static void UpdateMarkerVisuals(IMapManager mapManager, int[] tiles, int selectedIndex = -1, MarkerType type = MarkerType.Undefined)
		//	=> UpdateMapMarkers(mapManager, tiles, selectedIndex, type);

		//[System.Obsolete("Use EditorMarkerUtil.ClearMapMarkers instead")]
		//public static void DestroyMarkerVisuals() => ClearMapMarkers();
	}
}