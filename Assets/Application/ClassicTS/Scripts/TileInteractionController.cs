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
				if (gesture.z == -1) dirBit |= TileProperties.South;
				if (gesture.x == 1) dirBit |= TileProperties.East;
				if (gesture.x == -1) dirBit |= TileProperties.West;

				if (dirBit != 0)
				{
					int newIndex = mapManager.GetAdjacentTile(lastIndex, dirBit);
					if (newIndex != -1 && ValidateMove(newIndex))
					{
						SwapTiles(newIndex, true);
						gestureSystem.ConsumeGesture(gesture);
					}
				}
				else
				{
					// Remainder is partial drag position
					var x = gesture.x;
					var z = gesture.z;

					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z)) z = 0f;
					else x = 0f;

					var flags = TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll;
					var minCoordEW = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.West, flags);
					var maxCoordEW = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.East, flags);
					var minCoordNS = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.South, flags);
					var manCoordNS = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.North, flags);

					var currentPos = mapManager.GetTilePosition(dragIndex);
					var dragPos = currentPos + new Vector3(x, 0f, z);// gesture;
					currentPos.x = Mathf.Clamp(dragPos.x, minCoordEW.X, maxCoordEW.X);
					currentPos.z = Mathf.Clamp(dragPos.z, minCoordNS.Z, manCoordNS.Z);

					mapManager.Tiles[dragIndex].GameObject.transform.position = currentPos;
				}
			}
		}

		private void SwapTiles(int targetIndex, bool continueDragging)
		{
			if (targetIndex < 0 || targetIndex >= mapManager.Tiles.Length || dragIndex == -1) return;
			if (!ValidateMove(targetIndex)) return;

			var oldPosition = mapManager.GetTilePosition(dragIndex);
			var newPosition = mapManager.GetTilePosition(targetIndex);

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
				targetTile.transform.position = oldPosition;
			}

			draggedTile.transform.position = newPosition;
			if (continueDragging)
			{
				dragIndex = targetIndex;
				lastIndex = targetIndex;
			}
		}

		private void OnDragEnd()
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			var tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;

			// Check bounds for both X and Z directions
			var flags = TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll;
			var minCoordX = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.West, flags);
			var maxCoordX = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.East, flags);
			var minCoordZ = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.South, flags);
			var maxCoordZ = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.North, flags);

			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minCoordX.X, maxCoordX.X));
			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minCoordZ.Z, maxCoordZ.Z));
			var targetCoord = new GridCoord(x, z);
			int targetIndex = targetCoord.ToIndex(mapManager.Width);

			if (ValidateMove(targetIndex))
			{
				SwapTiles(targetIndex, false);
			}
			else
			{
				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);
			}

			dragIndex = -1;
			lastIndex = -1;
		}
	}
}