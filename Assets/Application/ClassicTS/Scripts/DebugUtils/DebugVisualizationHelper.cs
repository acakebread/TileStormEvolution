using UnityEngine;

namespace ClassicTilestorm
{
	public static class DebugVisualizationHelper
	{
		private class OriginalMaterialHolder : MonoBehaviour { public Material originalMaterial; }

		public static void HighlightStrip(IMapPlay map, in TileStrip strip, bool highlight)
		{
			if (!ApplicationSettings.ShowTileSelection) return;
			if (null == strip.Indices) return;

			foreach (var tileIndex in strip.Indices)
			{
				if (highlight) TintTile(map.GetTile(tileIndex).gameObject, Color.cyan);
				else ClearTileTint(map.GetTile(tileIndex).gameObject);
			}

			if (null != TileStripHelper.SpareTile)
			{
				if (highlight) TintTile(TileStripHelper.SpareTile, Color.cyan);
				else ClearTileTint(TileStripHelper.SpareTile);
			}
		}

		public static void TintTile(GameObject tile, Color tint)
		{
			if (null == tile) return;

			var meshRenderer = tile.GetComponentInChildren<MeshRenderer>();
			if (null == meshRenderer) return;

			if (!tile.TryGetComponent<OriginalMaterialHolder>(out var holder))
			{
				holder = tile.AddComponent<OriginalMaterialHolder>();
				holder.originalMaterial = meshRenderer.material;
			}

			meshRenderer.material = new Material(holder.originalMaterial ?? meshRenderer.material) { color = tint };
		}

		public static void ClearTileTint(GameObject tile)
		{
			if (null == tile) return;

			var meshRenderer = tile.GetComponentInChildren<MeshRenderer>();
			if (null == meshRenderer) return;

			if (tile.TryGetComponent<OriginalMaterialHolder>(out var holder) && null != holder.originalMaterial)
				meshRenderer.material = holder.originalMaterial;
		}

		public static void ClearMapTints(IMapPlay map)
		{
			if (map == null) return;

			for (var i = 0; i < map.Count; i++)
				ClearTileTint(map.GetTile(i).gameObject);
		}
	}
}
