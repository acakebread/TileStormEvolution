using UnityEngine;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private MapManager mapManager;
		private int selectedTileDefIndex;

		public EditorControllerPaint(Camera camera, MapManager map, int tileDefIndex) : base(camera)
		{
			mapManager = map;
			selectedTileDefIndex = tileDefIndex;
		}

		public void SetTileDefIndex(int tileDefIndex) => selectedTileDefIndex = tileDefIndex;

		public void PlaceTileAtMousePosition()
		{
			if (PlaceholderEditorUI.Instance.IsGuiControlActive()) return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (-1 == mapIndex)
			{
				Debug.LogWarning("Mouse position is outside map bounds");
				return;
			}

			var x = mapIndex % mapManager.Width;
			var z = mapIndex / mapManager.Width;

			mapManager.UpdateTileAt(x, z, selectedTileDefIndex);
		}
	}
}