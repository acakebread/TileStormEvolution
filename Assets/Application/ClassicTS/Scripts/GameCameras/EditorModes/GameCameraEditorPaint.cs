using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class GameCameraEditorPaint : GameCameraEditorMovement
	{
		private MapManager mapManager;
		private int selectedTileDefIndex;

		public GameCameraEditorPaint(Camera camera, MapManager map, int tileDefIndex) : base(camera)
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

			// Check if a GUI control is active
			bool isGuiControlActive = GUIUtility.hotControl != 0;

			// Handle mouse button down
			if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject() && !isGuiControlActive)
			{
				PlaceTileAtMousePosition();
			}
		}

		public void PlaceTileAtMousePosition()
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

			mapManager.UpdateTileAt(x, z, selectedTileDefIndex);
			Debug.Log($"Placed tile at ({x}, {z}) with tileDefIndex={selectedTileDefIndex}");
		}
	}
}