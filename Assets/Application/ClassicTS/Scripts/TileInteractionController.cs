using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private MapManager.TileStrip tileStrip;
		private int dragIndex = -1;
		private Vector3 startWorldPos;
		private const float gridSize = 1.0f;

		public void Start()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag += OnBeginDrag;
			gestureSystem.OnDrag += OnDrag;
			gestureSystem.OnEndDrag += OnEndDrag;
		}

		private void OnDestroy()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag -= OnBeginDrag;
			gestureSystem.OnDrag -= OnDrag;
			gestureSystem.OnEndDrag -= OnEndDrag;

			mapManager.HighlightStrip(tileStrip, false);
		}

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			tileStrip = default;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			Vector3 worldPos = mapManager.ScreenToWorld(screenPos);
			int x = Mathf.RoundToInt(worldPos.x);
			int z = Mathf.RoundToInt(worldPos.z);
			var coord = new GridCoord(x, z);
			if (coord.X < 0 || coord.X >= mapManager.Width || coord.Z < 0 || coord.Z >= mapManager.Height) return;
			int tileIndex = mapManager.ToIndex(coord);

			var properties = mapManager.GetTilePropertiesAt(tileIndex);
			if (properties == null || !properties.Interactive)
			{
				Debug.LogWarning($"Cannot drag tile at index {tileIndex}: {(properties == null ? "Empty" : "Not draggable")}");
				return;
			}

			dragIndex = tileIndex;
			startWorldPos = worldPos;
			tileStrip = mapManager.GetTileStrip(tileIndex, 0);
			mapManager.HighlightStrip(tileStrip, true);
		}

		private void OnDrag(Vector3 screenPos)
		{
			mapManager.HighlightStrip(tileStrip, false);

			if (dragIndex == -1) return;

			Vector3 currentPos = mapManager.ScreenToWorld(screenPos);
			Vector3 tempStartPos = startWorldPos;
			var gestureList = new List<Vector3>();

			while (true)
			{
				Vector3 delta = currentPos - tempStartPos;
				float absX = Mathf.Abs(delta.x);
				float absZ = Mathf.Abs(delta.z);

				if (absX > absZ && absX >= gridSize)
				{
					int direction = delta.x > 0 ? 1 : -1;
					gestureList.Add(new Vector3(direction, 0, 0));
					tempStartPos.x += direction * gridSize;
				}
				else if (absZ >= gridSize)
				{
					int direction = delta.z > 0 ? 1 : -1;
					gestureList.Add(new Vector3(0, 0, direction));
					tempStartPos.z += direction * gridSize;
				}
				else
				{
					gestureList.Add(delta);
					break;
				}
			}

			foreach (var gesture in gestureList)
			{
				if (tileStrip.TileIndices != null)
				{
					foreach (var tileIndex in tileStrip.TileIndices)
						mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
				}

				int dirBit = 0;
				if (gesture.z == 1) dirBit |= TileProperties.North;
				if (gesture.z == -1) dirBit |= TileProperties.South;
				if (gesture.x == 1) dirBit |= TileProperties.East;
				if (gesture.x == -1) dirBit |= TileProperties.West;

				if (dirBit != 0)
				{
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);
					if (mapManager.RollStrip(tileStrip))
						dragIndex = mapManager.GetAdjacentTile(dragIndex, dirBit);
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);
					startWorldPos += gesture;
				}
				else
				{
					var delta = Vector3.zero;
					tileStrip = new MapManager.TileStrip();
					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z))
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, gesture.x > 0f ? TileProperties.East : TileProperties.West);
						if (tileStrip.LastIsRollOrDock)
							delta = new Vector3(gesture.x, 0, 0);
					}
					else
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, gesture.z > 0f ? TileProperties.North : TileProperties.South);
						if (tileStrip.LastIsRollOrDock)
							delta = new Vector3(0, 0, gesture.z);
					}

					if (tileStrip.TileIndices != null)
					{
						foreach (var tileIndex in tileStrip.TileIndices)
							mapManager.Tiles[tileIndex].GameObject.transform.position += delta;
						
						mapManager.UpdateSpareTile(tileStrip, delta, true);// Update spare tile to fill gap at trailing edge
					}
				}
			}
			mapManager.HighlightStrip(tileStrip, true);
		}

		private void OnEndDrag()
		{
			if (dragIndex == -1) return;
			mapManager.HighlightStrip(tileStrip, false);
			
			mapManager.UpdateSpareTile(tileStrip, Vector3.zero, false);// Deactivate spare tile

			if (tileStrip.TileIndices != null)
			{
				if (tileStrip.LastIsRollOrDock)
				{
					var currentPos = mapManager.Tiles[dragIndex].GameObject.transform.position;
					int x = Mathf.RoundToInt(currentPos.x);
					int z = Mathf.RoundToInt(currentPos.z);
					var targetCoord = new GridCoord(x, z);
					int targetIndex = mapManager.ToIndex(targetCoord);

					if (targetIndex != dragIndex)
						mapManager.RollStrip(tileStrip);
				}
				foreach (var tileIndex in tileStrip.TileIndices)
					mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
			}

			dragIndex = -1;
			tileStrip = default;
		}
	}
}