using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private TileMovementHandler movementHandler;
		private int dragIndex = -1;

		public void Start()
		{
			if (gestureSystem == null) return;
			gestureSystem.OnDragStart += OnDragStart;
			gestureSystem.OnDragging += OnDragging;
			gestureSystem.OnDragEnd += OnDragEnd;
		}

		private void OnDestroy()
		{
			if (gestureSystem == null) return;
			gestureSystem.OnDragStart -= OnDragStart;
			gestureSystem.OnDragEnd -= OnDragEnd;
			gestureSystem.OnDragging -= OnDragging;
		}

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			movementHandler = new TileMovementHandler(mapManager);
			dragIndex = -1;
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
		}

		private void OnDragging(List<Vector3> gestures)
		{
			if (dragIndex == -1) return;
			foreach (var gesture in gestures)
			{
				int dirBit = 0;
				if (gesture.z == 1) dirBit |= TileProperties.North;
				if (gesture.z == -1) dirBit |= TileProperties.South;
				if (gesture.x == 1) dirBit |= TileProperties.East;
				if (gesture.x == -1) dirBit |= TileProperties.West;

				if (dirBit != 0)
				{
					int newIndex = mapManager.GetAdjacentTile(dragIndex, dirBit);
					if (newIndex != -1 && movementHandler.ValidateMove(dragIndex, newIndex))
					{
						movementHandler.SwapTiles(dragIndex, newIndex);
						dragIndex = newIndex;
						gestureSystem.ConsumeGesture(gesture);
					}
				}
				else
				{
					var x = gesture.x;
					var z = gesture.z;
					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z)) z = 0f;
					else x = 0f;

					var currentPos = mapManager.GetTilePosition(dragIndex);
					var offset = new Vector3(x, 0f, z);
					var newPos = movementHandler.ApplyDragOffset(dragIndex, currentPos, offset);
					mapManager.Tiles[dragIndex].GameObject.transform.position = newPos;
				}
			}
		}

		private void OnDragEnd()
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			var tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;
			if (movementHandler.TrySnapToGrid(dragIndex, tilePos, out int targetIndex))
			{
				movementHandler.SwapTiles(dragIndex, targetIndex, true);
			}
			else
			{
				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);
			}

			dragIndex = -1;
		}
	}
}