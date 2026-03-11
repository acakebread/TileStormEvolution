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
	}
}