using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private MapManager mapManager;
		private int dragIndex = -1;
		private int lastIndex = -1;
		private GestureSystem gestureSystem => GestureSystem.instance;

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			lastIndex = -1;
			gestureSystem.OnDragStarted += HandleDragStarted;
			gestureSystem.OnDragEnded += HandleDragEnded;
			gestureSystem.OnGesturesUpdated += HandleGesturesUpdated;
		}

		private void OnDestroy()
		{
			if (gestureSystem != null)
			{
				gestureSystem.OnDragStarted -= HandleDragStarted;
				gestureSystem.OnDragEnded -= HandleDragEnded;
				gestureSystem.OnGesturesUpdated -= HandleGesturesUpdated;
			}
		}

		private void Update()
		{
			if (gestureSystem.isDragging) UpdateTileVisualPosition();
		}

		private void HandleDragStarted(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
			if (x < 0 || x >= mapManager.Width || z < 0 || z >= mapManager.Height) return;
			int tileIndex = z * mapManager.Width + x;

			var properties = mapManager.GetTilePropertiesAt(tileIndex);
			if (properties == null || !properties.CanBeDragged) return;

			dragIndex = tileIndex;
			lastIndex = tileIndex;
		}

		private void HandleGesturesUpdated(List<(GestureSystem.GestureMode mode, int direction)> gestures)
		{
			if (dragIndex == -1 || lastIndex == -1) return;
			foreach (var gesture in gestures)
			{
				int dirBit = GetDirectionBit(gesture.mode, gesture.direction);
				int stride = GetStrideFromDirBit(dirBit);
				int newIndex = lastIndex + stride;
				if (ValidateMove(newIndex))
				{
					StartDragAtNewPosition(newIndex);
					gestureSystem.ConsumeGesture(gesture.mode, gesture.direction);
					lastIndex = newIndex;
				}
			}
			gestureSystem.ClearGestures();
		}

		private int GetDirectionBit(GestureSystem.GestureMode mode, int direction)
		{
			if (mode == GestureSystem.GestureMode.DraggingX)
				return direction > 0 ? TileProperties.East : TileProperties.West;
			return direction > 0 ? TileProperties.North : TileProperties.South;
		}

		private int GetStrideFromDirBit(int dirBit)
		{
			return dirBit switch
			{
				TileProperties.East => 1,
				TileProperties.West => -1,
				TileProperties.North => mapManager.Width,
				TileProperties.South => -mapManager.Width,
				_ => throw new System.ArgumentException($"Invalid dirBit: {dirBit}")
			};
		}

		private void UpdateTileVisualPosition()
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return;

			var currentPos = gestureSystem.GetCurrentPos();
			Vector3 newPos = mapManager.GetTilePosition(dragIndex);
			var mode = gestureSystem.GetCurrentMode();
			//int dirBit = mode == GestureSystem.GestureMode.DraggingX ? TileProperties.East : TileProperties.North;
			//var (minIndex, maxIndex) = GetDragBoundsIndexes(dragIndex, dirBit);

			int dirBitMin = mode == GestureSystem.GestureMode.DraggingX ? TileProperties.West : TileProperties.South;
			int dirBitMax = mode == GestureSystem.GestureMode.DraggingX ? TileProperties.East : TileProperties.North;
			var minIndex = GetDragBoundsIndexDirection(dragIndex, dirBitMin);
			var maxIndex = GetDragBoundsIndexDirection(dragIndex, dirBitMax);

			float minCoord = mode == GestureSystem.GestureMode.DraggingX ? mapManager.GetTileCoordinates(minIndex).x : mapManager.GetTileCoordinates(minIndex).z;
			float maxCoord = mode == GestureSystem.GestureMode.DraggingX ? mapManager.GetTileCoordinates(maxIndex).x : mapManager.GetTileCoordinates(maxIndex).z;

			if (mode == GestureSystem.GestureMode.DraggingX)
				newPos.x = Mathf.Clamp(currentPos.x, minCoord, maxCoord);
			else
				newPos.z = Mathf.Clamp(currentPos.z, minCoord, maxCoord);

			mapManager.Tiles[dragIndex].GameObject.transform.position = newPos;
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


		private void HandleDragEnded(Vector3 finalPos)
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			// Check bounds for both X and Z directions
			var minIndexX = GetDragBoundsIndexDirection(dragIndex, TileProperties.West);
			var maxIndexX = GetDragBoundsIndexDirection(dragIndex, TileProperties.East);
			var minIndexZ = GetDragBoundsIndexDirection(dragIndex, TileProperties.South);
			var maxIndexZ = GetDragBoundsIndexDirection(dragIndex, TileProperties.North);

			float minX = mapManager.GetTileCoordinates(minIndexX).x;
			float maxX = mapManager.GetTileCoordinates(maxIndexX).x;
			float minZ = mapManager.GetTileCoordinates(minIndexZ).z;
			float maxZ = mapManager.GetTileCoordinates(maxIndexZ).z;

			Vector3 tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;
			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
			int targetIndex = z * mapManager.Width + x;

			if (ValidateMove(targetIndex))
			{
				var (oldX, oldZ) = mapManager.GetTileCoordinates(dragIndex);

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
					targetTile.transform.position = new Vector3(oldX, 0f, oldZ);
				}

				draggedTile.transform.position = new Vector3(x, 0f, z);
			}
			else
			{
				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);
			}

			ResetDrag();
		}

		//private void HandleDragEnded(Vector3 finalPos)
		//{
		//	if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

		//	// Check bounds for both X and Z directions
		//	var (minIndexX, maxIndexX) = GetDragBoundsIndexes(dragIndex, TileProperties.East);
		//	var (minIndexZ, maxIndexZ) = GetDragBoundsIndexes(dragIndex, TileProperties.North);
		//	float minX = mapManager.GetTileCoordinates(minIndexX).x;
		//	float maxX = mapManager.GetTileCoordinates(maxIndexX).x;
		//	float minZ = mapManager.GetTileCoordinates(minIndexZ).z;
		//	float maxZ = mapManager.GetTileCoordinates(maxIndexZ).z;

		//	Vector3 tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;
		//	int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
		//	int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
		//	int targetIndex = z * mapManager.Width + x;

		//	if (ValidateMove(targetIndex))
		//	{
		//		var (oldX, oldZ) = mapManager.GetTileCoordinates(dragIndex);

		//		// Swap entire TileData
		//		var temp = mapManager.Tiles[dragIndex];
		//		mapManager.Tiles[dragIndex] = mapManager.Tiles[targetIndex];
		//		mapManager.Tiles[targetIndex] = temp;

		//		// Update GameObject names and positions
		//		var draggedTile = mapManager.Tiles[targetIndex].GameObject;
		//		var targetTile = mapManager.Tiles[dragIndex].GameObject;
		//		if (targetTile != null)
		//		{
		//			string tempName = draggedTile.name;
		//			draggedTile.name = targetTile.name;
		//			targetTile.name = tempName;
		//			targetTile.transform.position = new Vector3(oldX, 0f, oldZ);
		//		}

		//		draggedTile.transform.position = new Vector3(x, 0f, z);
		//	}
		//	else
		//	{
		//		mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);
		//	}

		//	ResetDrag();
		//}

		private bool ValidateMove(int targetIndex)
		{
			if (targetIndex == dragIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.DockOrRoll;
		}

		private void ResetDrag()
		{
			dragIndex = -1;
			lastIndex = -1;
		}

		private int GetDragBoundsIndexDirection(int index, int dirBit)
		{
			var (x, z) = mapManager.GetTileCoordinates(index);
			var dz = (dirBit & TileProperties.North) - ((dirBit & TileProperties.South) >> 1); // North (+1), South (-1)
			var dx = ((dirBit & TileProperties.East) >> 2) - ((dirBit & TileProperties.West) >> 3); // East (+1), West (-1)
			x += dx;
			z += dz;
			while (x >= 0 && x < mapManager.Width && z >= 0 && z < mapManager.Height)
			{
				var nextIndex = z * mapManager.Width + x;
				var props = mapManager.GetTilePropertiesAt(nextIndex);
				if (props == null || !props.DockOrRoll) break;
				index = nextIndex;
				x += dx;
				z += dz;
			}
			return index;
		}

		//private (int minIndex, int maxIndex) GetDragBoundsIndexes(int srcIndex, int dirBit)
		//{
		//	return (WalkMap(srcIndex, TileProperties.GetOppositeDirection(dirBit)), WalkMap(srcIndex, dirBit));

		//	int WalkMap(int index, int dirBit)
		//	{
		//		var (x, z) = mapManager.GetTileCoordinates(index);
		//		var dz = (dirBit & TileProperties.North) - ((dirBit & TileProperties.South) >> 1); // North (+1), South (-1)
		//		var dx = ((dirBit & TileProperties.East) >> 2) - ((dirBit & TileProperties.West) >> 3); // East (+1), West (-1)
		//		x += dx;
		//		z += dz;
		//		while (x >= 0 && x < mapManager.Width && z >= 0 && z < mapManager.Height)
		//		{
		//			var nextIndex = z * mapManager.Width + x;
		//			var props = mapManager.GetTilePropertiesAt(nextIndex);
		//			if (props == null || !props.DockOrRoll) break;
		//			index = nextIndex;
		//			x += dx;
		//			z += dz;
		//		}
		//		return index;
		//	}
		//}
	}
}

//using UnityEngine;
//using System.Collections.Generic;

//namespace GamePreviewNamespace
//{
//	public class TileInteractionController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private int dragIndex = -1;
//		private int lastIndex = -1;
//		private GestureSystem gestureSystem => GestureSystem.instance;

//		public void Initialize(MapManager manager)
//		{
//			mapManager = manager;
//			dragIndex = -1;
//			lastIndex = -1;
//			gestureSystem.OnDragStarted += HandleDragStarted;
//			gestureSystem.OnDragEnded += HandleDragEnded;
//			gestureSystem.OnGesturesUpdated += HandleGesturesUpdated;
//		}

//		private void OnDestroy()
//		{
//			if (gestureSystem != null)
//			{
//				gestureSystem.OnDragStarted -= HandleDragStarted;
//				gestureSystem.OnDragEnded -= HandleDragEnded;
//				gestureSystem.OnGesturesUpdated -= HandleGesturesUpdated;
//			}
//		}

//		private void Update()
//		{
//			if (gestureSystem.isDragging) UpdateTileVisualPosition();
//		}

//		private void HandleDragStarted(Vector3 hitPos)
//		{
//			int x = Mathf.RoundToInt(hitPos.x);
//			int z = Mathf.RoundToInt(hitPos.z);
//			if (x < 0 || x >= mapManager.Width || z < 0 || z >= mapManager.Height) return;
//			int tileIndex = z * mapManager.Width + x;

//			var properties = mapManager.GetTilePropertiesAt(tileIndex);
//			if (properties == null || !properties.CanBeDragged) return;

//			dragIndex = tileIndex;
//			lastIndex = tileIndex;
//		}

//		private void HandleGesturesUpdated(List<(GestureSystem.GestureMode mode, int direction)> gestures)
//		{
//			if (dragIndex == -1 || lastIndex == -1) return;
//			foreach (var gesture in gestures)
//			{
//				int stride = gesture.mode == GestureSystem.GestureMode.DraggingX ? 1 : mapManager.Width;
//				int newIndex = lastIndex + stride * gesture.direction;
//				if (ValidateMove(newIndex))
//				{
//					StartDragAtNewPosition(newIndex);
//					gestureSystem.ConsumeGesture(gesture.mode, gesture.direction);
//					lastIndex = newIndex;
//				}
//			}
//			gestureSystem.ClearGestures();
//		}

//		private void UpdateTileVisualPosition()
//		{
//			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

//			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//			if (!mapPlane.Raycast(ray, out float distance)) return;

//			var currentPos = gestureSystem.GetCurrentPos();
//			Vector3 newPos = mapManager.GetTilePosition(dragIndex);
//			int stride = gestureSystem.GetCurrentMode() == GestureSystem.GestureMode.DraggingX ? 1 : mapManager.Width;

//			var (minIndex, maxIndex) = GetDragBoundsIndexes(dragIndex, stride);
//			float minCoord = stride == 1 ? mapManager.GetTileCoordinates(minIndex).x : mapManager.GetTileCoordinates(minIndex).z;
//			float maxCoord = stride == 1 ? mapManager.GetTileCoordinates(maxIndex).x : mapManager.GetTileCoordinates(maxIndex).z;

//			if (stride == 1)
//				newPos.x = Mathf.Clamp(currentPos.x, minCoord, maxCoord);
//			else
//				newPos.z = Mathf.Clamp(currentPos.z, minCoord, maxCoord);

//			mapManager.Tiles[dragIndex].GameObject.transform.position = newPos;
//		}

//		private void StartDragAtNewPosition(int newIndex)
//		{
//			if (newIndex < 0 || newIndex >= mapManager.Tiles.Length || dragIndex == -1) return;
//			if (!ValidateMove(newIndex)) return;

//			var oldPosition = mapManager.GetTilePosition(dragIndex);
//			var newPosition = mapManager.GetTilePosition(newIndex);

//			// Swap entire TileData
//			var temp = mapManager.Tiles[dragIndex];
//			mapManager.Tiles[dragIndex] = mapManager.Tiles[newIndex];
//			mapManager.Tiles[newIndex] = temp;

//			// Update GameObject names and positions
//			var draggedTile = mapManager.Tiles[newIndex].GameObject;
//			var targetTile = mapManager.Tiles[dragIndex].GameObject;
//			if (targetTile != null)
//			{
//				string tempName = draggedTile.name;
//				draggedTile.name = targetTile.name;
//				targetTile.name = tempName;
//				targetTile.transform.position = oldPosition;
//			}

//			draggedTile.transform.position = newPosition;
//			dragIndex = newIndex;
//			lastIndex = newIndex;
//		}

//		private void HandleDragEnded(Vector3 finalPos)
//		{
//			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

//			var (minIndexX, maxIndexX) = GetDragBoundsIndexes(dragIndex, 1);
//			var (minIndexZ, maxIndexZ) = GetDragBoundsIndexes(dragIndex, mapManager.Width);
//			float minX = mapManager.GetTileCoordinates(minIndexX).x;
//			float maxX = mapManager.GetTileCoordinates(maxIndexX).x;
//			float minZ = mapManager.GetTileCoordinates(minIndexZ).z;
//			float maxZ = mapManager.GetTileCoordinates(maxIndexZ).z;

//			Vector3 tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;
//			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
//			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
//			int targetIndex = z * mapManager.Width + x;

//			if (ValidateMove(targetIndex))
//			{
//				var (oldX, oldZ) = mapManager.GetTileCoordinates(dragIndex);

//				// Swap entire TileData
//				var temp = mapManager.Tiles[dragIndex];
//				mapManager.Tiles[dragIndex] = mapManager.Tiles[targetIndex];
//				mapManager.Tiles[targetIndex] = temp;

//				// Update GameObject names and positions
//				var draggedTile = mapManager.Tiles[targetIndex].GameObject;
//				var targetTile = mapManager.Tiles[dragIndex].GameObject;
//				if (targetTile != null)
//				{
//					string tempName = draggedTile.name;
//					draggedTile.name = targetTile.name;
//					targetTile.name = tempName;
//					targetTile.transform.position = new Vector3(oldX, 0f, oldZ);
//				}

//				draggedTile.transform.position = new Vector3(x, 0f, z);
//			}
//			else
//			{
//				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);
//			}

//			ResetDrag();
//		}

//		private bool ValidateMove(int targetIndex)
//		{
//			if (targetIndex == dragIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
//			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
//			return targetProps != null && targetProps.DockOrRoll;
//		}

//		private void ResetDrag()
//		{
//			dragIndex = -1;
//			lastIndex = -1;
//		}

//		private (int minIndex, int maxIndex) GetDragBoundsIndexes(int srcIndex, int stride)
//		{
//			return (WalkMap(srcIndex, -stride), WalkMap(srcIndex, stride));

//			int WalkMap(int index, int stride)
//			{
//				int dx = stride % mapManager.Width; // 1 or -1 for X, 0 for Z
//				int dz = stride / mapManager.Width; // 1 or -1 for Z, 0 for X
//				var (x, z) = mapManager.GetTileCoordinates(index);
//				while (x >= 0 && x < mapManager.Width && z >= 0 && z < mapManager.Height)
//				{
//					int nextIndex = index + stride;
//					if (nextIndex < 0 || nextIndex >= mapManager.Tiles.Length) break;
//					var props = mapManager.GetTilePropertiesAt(nextIndex);
//					if (props == null || !props.DockOrRoll) break;
//					index = nextIndex;
//					x += dx;
//					z += dz;
//				}
//				return index;
//			}
//		}
//	}
//}
