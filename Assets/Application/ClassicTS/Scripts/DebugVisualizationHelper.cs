using UnityEngine;

namespace ClassicTilestorm
{
	public static class DebugVisualizationHelper
	{
		private class OriginalMaterialHolder : MonoBehaviour { public Material originalMaterial; }

		public static void HighlightStrip(IMap map, in TileStripHelper.TileStrip strip, bool highlight)
		{
			if (!PreviewSettings.ShowTileSelection) return;
			if (null == strip.Indices) return;

			foreach (var tileIndex in strip.Indices)
				HighlightTile(map?.GetTile(tileIndex).GameObject, highlight);

			if (TileStripHelper.SpareTile != null)
				HighlightTile(TileStripHelper.SpareTile, highlight);
		}

		private static void HighlightTile(GameObject tile, bool enable)
		{
			if (null == tile) return;

			var meshRenderer = tile.GetComponentInChildren<MeshRenderer>();
			if (meshRenderer == null) return;

			if (enable)
			{
				if (!tile.TryGetComponent<OriginalMaterialHolder>(out var holder))
				{
					holder = tile.AddComponent<OriginalMaterialHolder>();
					holder.originalMaterial = meshRenderer.material;
				}
				meshRenderer.material = new Material(meshRenderer.material) { color = Color.cyan };
			}
			else
			{
				if (tile.TryGetComponent<OriginalMaterialHolder>(out var holder) && null != holder.originalMaterial)
					meshRenderer.material = holder.originalMaterial;
			}
		}
	}
}
