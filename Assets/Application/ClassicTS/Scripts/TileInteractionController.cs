using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;

		private MapManager mapManager;
		private int dragIndex = -1;
		private int lastIndex = -1;

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			lastIndex = -1;
			gestureSystem.OnDragStart += OnDragStart;
			gestureSystem.OnDragging += OnDragging;
			gestureSystem.OnDragEnd += OnDragEnd;
		}

		private void OnDestroy()
		{
			if (gestureSystem != null)
			{
				gestureSystem.OnDragStart -= OnDragStart;
				gestureSystem.OnDragEnd -= OnDragEnd;
				gestureSystem.OnDragging -= OnDragging;
			}
		}

		private bool ValidateMove(int targetIndex)
		{
			if (targetIndex == dragIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.DockOrRoll;
		}

		private void OnDragStart(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
			var coord = new GridCoord(x, z);
			if (coord.X < 0 || coord.X >= mapManager.Width || coord.Z < 0 || coord.Z >= mapManager.Height) return;
			int tileIndex = coord.ToIndex(mapManager.Width);

			var properties = mapManager.GetTilePropertiesAt(tileIndex);
			if (properties == null || !properties.CanBeDragged) return;

			dragIndex = tileIndex;
			lastIndex = tileIndex;
		}

		private void OnDragging(List<Vector3> gestures)
		{
			if (dragIndex == -1 || lastIndex == -1) return;
			foreach (var gesture in gestures)
			{
				int dirBit = 0;
				if (gesture.z == 1) dirBit |= TileProperties.North;
				if (gesture.z ==-1) dirBit |= TileProperties.South;
				if (gesture.x == 1) dirBit |= TileProperties.East;
				if (gesture.x ==-1) dirBit |= TileProperties.West;

				if (dirBit != 0)
				{
					int newIndex = mapManager.GetAdjacentTile(lastIndex, dirBit);
					if (newIndex != -1 && ValidateMove(newIndex))
					{
						StartDragAtNewPosition(newIndex);
						lastIndex = newIndex;
					}
				}
				else
				{
					//remainder is partial drag position
					var flags = TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll;
					var minCoordX = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.West, flags);
					var maxCoordX = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.East, flags);
					var minCoordZ = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.South, flags);
					var maxCoordZ = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.North, flags);

					var currentPos = mapManager.GetTilePosition(dragIndex);
					var dragPos = currentPos + gesture;
					currentPos.x = Mathf.Clamp(dragPos.x, minCoordX.X, maxCoordX.X);
					currentPos.z = Mathf.Clamp(dragPos.z, minCoordZ.Z, maxCoordZ.Z);

					mapManager.Tiles[dragIndex].GameObject.transform.position = currentPos;

				}	
			}
		}

		private void StartDragAtNewPosition(int newIndex)
		{
			if (newIndex < 0 || newIndex >= mapManager.Tiles.Length || dragIndex == -1) return;
			if (!ValidateMove(newIndex)) return;

			var oldPosition = mapManager.GetTilePosition(dragIndex);
			var newPosition = mapManager.GetTilePosition(newIndex);

			// Swap entire TileData
			var temp = mapManager.Tiles[dragIndex];
			mapManager.Tiles[dragIndex] = mapManager.Tiles[newIndex];
			mapManager.Tiles[newIndex] = temp;

			// Update GameObject names and positions
			var draggedTile = mapManager.Tiles[newIndex].GameObject;
			var targetTile = mapManager.Tiles[dragIndex].GameObject;
			if (targetTile != null)
			{
				string tempName = draggedTile.name;
				draggedTile.name = targetTile.name;
				targetTile.name = tempName;
				targetTile.transform.position = oldPosition;
			}

			draggedTile.transform.position = newPosition;
			dragIndex = newIndex;
			lastIndex = newIndex;
		}

		private void OnDragEnd(GestureSystem.GestureMode mode, Vector3 finalPos)
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			var tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;

			// Check bounds for both X and Z directions
			var flags = TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll;
			if (mode == GestureSystem.GestureMode.DraggingX)
			{
				var minCoord = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.West, flags);
				var maxCoord = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.East, flags);
				tilePos.x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minCoord.X, maxCoord.X));
			}
			else
			{
				var minCoord = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.South, flags);
				var maxCoord = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.North, flags);
				tilePos.z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minCoord.Z, maxCoord.Z));
			}

			var targetCoord = new GridCoord((int)tilePos.x, (int)tilePos.z);
			int targetIndex = targetCoord.ToIndex(mapManager.Width);

			if (ValidateMove(targetIndex))
			{
				var oldCoord = mapManager.GetTileCoordinates(dragIndex);

				// Swap entire TileData
				var temp = mapManager.Tiles[dragIndex];
				mapManager.Tiles[dragIndex] = mapManager.Tiles[targetIndex];
				mapManager.Tiles[targetIndex] = temp;

				// Update GameObject names and positions
				var draggedTile = mapManager.Tiles[targetIndex].GameObject;
				var targetTile = mapManager.Tiles[dragIndex].GameObject;
				if (targetTile != null)
				{
					string tempName = draggedTile.name;
					draggedTile.name = targetTile.name;
					targetTile.name = tempName;
					targetTile.transform.position = oldCoord.ToPosition();
				}

				draggedTile.transform.position = targetCoord.ToPosition();
			}
			else
			{
				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);//snap back to original position
			}

			dragIndex = -1;
			lastIndex = -1;
		}
	}
}