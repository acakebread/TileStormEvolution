using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class GameCameraEditorPaint : GameCameraEditorMovement
	{
		private MapManager mapManager;
		private int selectedMapDefIndex; // Index into mapDefs

		public GameCameraEditorPaint(Camera camera, MapManager map, int mapDefIndex) : base(camera)
		{
			mapManager = map;
			selectedMapDefIndex = mapDefIndex;
		}

		public void SetTileDefIndex(int mapDefIndex)
		{
			selectedMapDefIndex = mapDefIndex;
		}

		public override void Update()
		{
			base.Update();
			if (!camera || !mapManager) return;

			// Check if a GUI control is active
			bool isGuiControlActive = GUIUtility.hotControl != 0;

			// Handle mouse button down
			if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject() && !isGuiControlActive)
			{
				PlaceTileAtMousePosition();
			}
		}

		private void PlaceTileAtMousePosition()
		{
			if (GUIUtility.hotControl != 0) return;

			Vector3 worldPos = MapManager.ScreenToWorld(Input.mousePosition);
			int mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex == -1)
			{
				Debug.LogWarning("Mouse position is outside map bounds");
				return;
			}

			int x = mapIndex % mapManager.Width;
			int z = mapIndex / mapManager.Width;

			mapManager.UpdateTileAt(x, z, selectedMapDefIndex);
			Debug.Log($"Placed tile at ({x}, {z}) with mapDefs index={selectedMapDefIndex}");
		}
	}
}