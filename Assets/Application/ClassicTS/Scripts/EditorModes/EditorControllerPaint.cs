using UnityEngine;
using UnityEngine.EventSystems;

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

		public void SetTileDefIndex(int tileDefIndex)
		{
			selectedTileDefIndex = tileDefIndex;
		}

		public override void Update()
		{
			base.Update();
			if (!camera || !mapManager) return;
			// Removed tile placement logic from Update to prevent double placement
		}

		public void PlaceTileAtMousePosition()
		{
			if (GUIUtility.hotControl != 0) return;

			Vector3 worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			int mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex == -1)
			{
				Debug.LogWarning("Mouse position is outside map bounds");
				return;
			}

			int x = mapIndex % mapManager.Width;
			int z = mapIndex / mapManager.Width;

			mapManager.UpdateTileAt(x, z, selectedTileDefIndex);
			//Debug.Log($"Placed tile at ({x}, {z}) with tileDefIndex={selectedTileDefIndex}");
		}
	}
}