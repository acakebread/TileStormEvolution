using MassiveHadronLtd;
using System;
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class MapUtils
	{
		public static Variant NextVariantOnMap(IMapEdit map, Vector3 worldPos, Variant variant, bool cycleHeight = false)
		{
			var current = map.GetVariantAt(worldPos);
			if (current.hash == variant.hash)
			{
				float[] angles = { 0f, 90f, 180f, 270f };
				float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

				int dIdx = System.Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;
				int aIdx = System.Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;

				aIdx = (aIdx + 1) % angles.Length;
				if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

				variant.delta = cycleHeight ? new Vector3(current.delta.x, deltas[dIdx], current.delta.z) : current.delta;
				variant.angle = angles[aIdx];
			}

			return variant;
		}

		public static void RebuildMarkers(IMapEdit iMap, ISelectable[] selection)
		{
			var tiles = iMap?.GetAttachments()?.Select(a => a.tile)?.Distinct()?.ToArray() ?? Array.Empty<int>();
			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];
			var isWaypointMode = selection != null && selection.Length == 1 && selection[0] is Waypoint;

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = iMap.TileRenderPosition(tile);
				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile) ? new(0f, 1f, 1f, 0.5f) : new(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0 && selection[0] is MapAttachment ma) ? ma.tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);
			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		public static (int minX, int minZ, int maxX, int maxZ) GetContentBounds(Map map)
		{
			if (map.tiles == null || map.tiles.Length == 0 || map.width <= 0 || map.height <= 0)
				return (0, 0, -1, -1);

			int minX = map.width;
			int minZ = map.height;
			int maxX = -1;
			int maxZ = -1;

			for (int i = 0; i < map.tiles.Length; i++)
			{
				int t = map.tiles[i];
				if (t < 0) continue;

				int hash = (t < map.variants.Length) ? map.variants[t].hash : 0;
				if (hash == 0) continue;

				var def = ResourceManager.GetDefinition(hash);
				if (def == null || def.IsDefault()) continue;

				int x = i % map.width;
				int z = i / map.width;

				minX = Math.Min(minX, x);
				maxX = Math.Max(maxX, x);
				minZ = Math.Min(minZ, z);
				maxZ = Math.Max(maxZ, z);
			}

			return maxX >= 0 ? (minX, minZ, maxX, maxZ) : (0, 0, -1, -1);
		}

		public static GameObject BuildPreviewGeometry(Map map, Transform previewParent, int layer)
		{
			if (map.width <= 0 || map.height <= 0 || map.tiles == null || map.variants == null)
				return null;

			// CRITICAL: Work on a CLONE so we don't corrupt the original map's runtime state
			var previewMap = map.Clone();

			var previewRoot = new GameObject($"Preview_{map.name ?? "Map"}");
			previewRoot.transform.SetParent(previewParent, false);
			previewRoot.transform.localPosition = Vector3.zero;

			var originalParent = previewMap.parent;
			previewMap.parent = previewRoot.transform;

			try
			{
				previewMap.Preset();
				if (false == previewMap.InitialiseGraph())
				{
					Debug.LogWarning("Preview graph creation failed on clone");
					UnityEngine.Object.DestroyImmediate(previewRoot);
					return null;
				}

				previewMap.RefreshAttachments(previewMap.GetAttachments());

				PreviewRenderLayers.SetLayerRecursively(previewRoot, PreviewRenderLayers.LAYER_PREVIEW);
				PreviewRenderLayers.SetPreviewLayersToChildren(previewRoot.transform);

				var particleControllers = previewRoot.GetComponentsInChildren<ParticleController>(true);
				foreach (var particleController in particleControllers)
					particleController.gameObject.layer = PreviewRenderLayers.previewTransparentLayer;

				var lights = previewRoot.GetComponentsInChildren<Light>(true);
				foreach (var light in lights)
					PreviewRenderLayers.SetPreviewLayers(light, false);

				return previewRoot;
			}
			catch (Exception e)
			{
				Debug.LogError($"Preview build failed: {e.Message}");
				UnityEngine.Object.DestroyImmediate(previewRoot);
				return null;
			}
			finally
			{
				// Restore original parent on clone (not needed, but clean)
				previewMap.parent = originalParent;
			}
		}
	}
}