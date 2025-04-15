using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject draggedTile;
		private Vector3 originalPos;
		private int dragIndex = -1;
		private Vector3 dragOffset;
		private bool isDragging;
		private float minX, maxX, minZ, maxZ;
		private int startGridX, startGridZ;
		private GestureSystem gestureSystem;

		public void Initialize(MapManager manager, GestureSystem gesture)
		{
			mapManager = manager;
			gestureSystem = gesture;
			isDragging = false;
			dragIndex = -1;
			draggedTile = null;
			gestureSystem.OnDragStarted += HandleDragStarted;
			gestureSystem.OnDragEnded += HandleDragEnded;
			Debug.Log("TileInteractionController: Initialized");
		}

		private void OnDestroy()
		{
			if (gestureSystem != null)
			{
				gestureSystem.OnDragStarted -= HandleDragStarted;
				gestureSystem.OnDragEnded -= HandleDragEnded;
			}
		}

		private void Update()
		{
			if (isDragging)
			{
				UpdateTilePosition();
			}
		}

		private void HandleDragStarted(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
			int tileIndex = z * mapManager.Width + x;

			Debug.Log($"TileInteractionController: Drag started at ({x},{z}), index={tileIndex}");

			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length || mapManager.Tiles[tileIndex] == null)
			{
				Debug.LogWarning("TileInteractionController: Invalid tile index or null tile");
				return;
			}

			var properties = mapManager.Tiles[tileIndex].GetComponent<TileProperties>();
			if (properties == null || !properties.CanBeDragged)
			{
				Debug.LogWarning("TileInteractionController: Tile cannot be dragged");
				return;
			}

			isDragging = true;
			dragIndex = tileIndex;
			draggedTile = mapManager.Tiles[tileIndex];
			originalPos = draggedTile.transform.position;
			dragOffset = hitPos - originalPos;
			GetDragBounds(tileIndex);
			startGridX = x;
			startGridZ = z;
			Debug.Log($"TileInteractionController: Dragging tile at {originalPos}, offset={dragOffset}");
		}

		private void UpdateTilePosition()
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance))
			{
				Debug.LogWarning("TileInteractionController: Raycast failed");
				return;
			}

			Vector3 currentPos = ray.GetPoint(distance);
			Vector3 newPos = originalPos;
			bool isValidMove = true;

			GestureSystem.GestureMode mode = gestureSystem.GetCurrentMode();
			if (mode == GestureSystem.GestureMode.DraggingX)
			{
				newPos.x = Mathf.Clamp(currentPos.x - dragOffset.x, minX, maxX);
				newPos.z = originalPos.z;
				dragOffset.z = 0f; // Ignore z-offset
			}
			else if (mode == GestureSystem.GestureMode.DraggingZ)
			{
				newPos.z = Mathf.Clamp(currentPos.z - dragOffset.z, minZ, maxZ);
				newPos.x = originalPos.x;
				dragOffset.x = 0f; // Ignore x-offset
			}

			// Validate target grid
			int targetGridX = Mathf.RoundToInt(newPos.x);
			int targetGridZ = Mathf.RoundToInt(newPos.z);
			int targetIndex = targetGridZ * mapManager.Width + targetGridX;

			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
				{
					isValidMove = false;
					newPos = originalPos;
				}
			}

			if (isValidMove)
			{
				draggedTile.transform.position = newPos;
			}
			else
			{
				draggedTile.transform.position = originalPos;
			}

			// Grid reset
			Vector3 tilePos = draggedTile.transform.position;
			int gridX = Mathf.RoundToInt(tilePos.x);
			int gridZ = Mathf.RoundToInt(tilePos.z);
			if ((gridX != startGridX || gridZ != startGridZ) &&
				Mathf.Abs(tilePos.x - gridX) < 0.1f && Mathf.Abs(tilePos.z - gridZ) < 0.1f)
			{
				gestureSystem.SignalDeadZone();
				StartDragAtNewPosition(currentPos, gridX, gridZ);
			}

			Debug.Log($"TileInteractionController: Tile moved to ({newPos.x},{newPos.z}), mode={mode}, offset={dragOffset}");
		}

		private void StartDragAtNewPosition(Vector3 hitPos, int gridX, int gridZ)
		{
			int tileIndex = gridZ * mapManager.Width + gridX;
			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length)
			{
				Debug.LogWarning("TileInteractionController: Invalid new tile index");
				return;
			}

			// Validate move
			int targetIndex = tileIndex;
			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
				{
					Debug.Log("TileInteractionController: Invalid swap, reverting");
					return;
				}
			}

			// Swap
			Vector3 newTilePos = new Vector3(gridX, 0f, gridZ);
			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
				{
					mapManager.Tiles[targetIndex] = draggedTile;
					mapManager.Tiles[dragIndex] = targetTile;

					UpdateTileName(draggedTile, gridX, gridZ);

					if (targetTile != null)
					{
						int origX = dragIndex % mapManager.Width;
						int origZ = dragIndex / mapManager.Width;
						targetTile.transform.position = new Vector3(origX, 0f, origZ);
						UpdateTileName(targetTile, origX, origZ);
					}
				}
				else
				{
					Debug.Log("TileInteractionController: Swap failed, invalid target");
					return;
				}
			}

			// Update drag state
			var properties = draggedTile.GetComponent<TileProperties>();
			if (properties == null || !properties.CanBeDragged)
			{
				Debug.LogWarning("TileInteractionController: Dragged tile invalid after swap");
				return;
			}

			dragIndex = tileIndex;
			originalPos = newTilePos;
			dragOffset = hitPos - newTilePos;
			GetDragBounds(dragIndex);
			startGridX = gridX;
			startGridZ = gridZ;
			Debug.Log($"TileInteractionController: New drag at ({gridX},{gridZ}), offset={dragOffset}");
		}

		private void HandleDragEnded(Vector3 finalPos)
		{
			if (!isDragging) return;

			Vector3 tilePos = draggedTile.transform.position;
			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
			int targetIndex = z * mapManager.Width + x;

			Debug.Log($"TileInteractionController: Drag ended at ({x},{z})");

			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
				{
					mapManager.Tiles[targetIndex] = draggedTile;
					mapManager.Tiles[dragIndex] = targetTile;

					draggedTile.transform.position = new Vector3(x, 0f, z);
					UpdateTileName(draggedTile, x, z);

					if (targetTile != null)
					{
						int origX = dragIndex % mapManager.Width;
						int origZ = dragIndex / mapManager.Width;
						targetTile.transform.position = new Vector3(origX, 0f, origZ);
						UpdateTileName(targetTile, origX, origZ);
					}
				}
				else
				{
					draggedTile.transform.position = originalPos;
				}
			}
			else
			{
				draggedTile.transform.position = originalPos;
			}

			ResetDrag();
		}

		private void GetDragBounds(int tileIndex)
		{
			int x = tileIndex % mapManager.Width;
			int z = tileIndex / mapManager.Width;

			minX = 0f;
			maxX = mapManager.Width - 1;
			minZ = 0f;
			maxZ = mapManager.Height - 1;

			for (int i = x - 1; i >= -1; i--)
			{
				if (i == -1) { minX = 0f; break; }
				int checkIndex = z * mapManager.Width + i;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { minX = i + 1; break; }
			}

			for (int i = x + 1; i <= mapManager.Width; i++)
			{
				if (i == mapManager.Width) { maxX = mapManager.Width - 1; break; }
				int checkIndex = z * mapManager.Width + i;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { maxX = i - 1; break; }
			}

			for (int j = z - 1; j >= -1; j--)
			{
				if (j == -1) { minZ = 0f; break; }
				int checkIndex = j * mapManager.Width + x;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { minZ = j + 1; break; }
			}

			for (int j = z + 1; j <= mapManager.Height; j++)
			{
				if (j == mapManager.Height) { maxZ = mapManager.Height - 1; break; }
				int checkIndex = j * mapManager.Width + x;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { maxZ = j - 1; break; }
			}

			Debug.Log($"TileInteractionController: Bounds set to minX={minX}, maxX={maxX}, minZ={minZ}, maxZ={maxZ}");
		}

		private void UpdateTileName(GameObject tile, int x, int z)
		{
			string[] nameParts = tile.name.Split('_');
			if (nameParts.Length >= 3)
				tile.name = $"{nameParts[0]}_{x}_{z}";
		}

		private void ResetDrag()
		{
			isDragging = false;
			dragIndex = -1;
			draggedTile = null;
			dragOffset = Vector3.zero;
			Debug.Log("TileInteractionController: Drag reset");
		}
	}
}


//using UnityEngine;

//namespace GamePreviewNamespace
//{
//	public class TileInteractionController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private GameObject draggedTile;
//		private Vector3 originalPos;
//		private int dragIndex = -1;
//		private Vector3 dragOffset;
//		private bool isDragging;
//		private bool isAxisLocked;
//		private bool isXAxis;
//		private float minX, maxX, minZ, maxZ;
//		private int startGridX, startGridZ;
//		private Vector3 initialMousePos;

//		public void Initialize(MapManager manager)
//		{
//			mapManager = manager;
//			isDragging = false;
//			dragIndex = -1;
//			draggedTile = null;
//			isAxisLocked = false;
//			isXAxis = false;
//			initialMousePos = Vector3.zero;
//		}

//		public void UpdateInteractions()
//		{
//			if (Input.GetMouseButtonDown(0)) StartDrag();
//			else if (isDragging && Input.GetMouseButton(0)) UpdateDrag();
//			else if (isDragging && Input.GetMouseButtonUp(0)) EndDrag();
//		}

//		private void StartDrag()
//		{
//			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//			if (!mapPlane.Raycast(ray, out float distance)) return;

//			Vector3 hitPos = ray.GetPoint(distance);
//			int x = Mathf.RoundToInt(hitPos.x);
//			int z = Mathf.RoundToInt(hitPos.z);
//			int tileIndex = z * mapManager.Width + x;

//			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length || mapManager.Tiles[tileIndex] == null) return;

//			var properties = mapManager.Tiles[tileIndex].GetComponent<TileProperties>();
//			if (properties == null || !properties.CanBeDragged) return;

//			isDragging = true;
//			dragIndex = tileIndex;
//			draggedTile = mapManager.Tiles[tileIndex];
//			originalPos = draggedTile.transform.position;
//			dragOffset = hitPos - originalPos;
//			initialMousePos = hitPos;
//			GetDragBounds(tileIndex);
//			isAxisLocked = false;
//			isXAxis = false;
//			startGridX = x;
//			startGridZ = z;
//		}

//		private void UpdateDrag()
//		{
//			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//			if (!mapPlane.Raycast(ray, out float distance)) return;

//			Vector3 currentPos = ray.GetPoint(distance);
//			Vector3 delta = currentPos - initialMousePos;

//			// Axis locking
//			if (!isAxisLocked)
//			{
//				float absX = Mathf.Abs(delta.x);
//				float absZ = Mathf.Abs(delta.z);
//				if (absX > absZ && absX > 0.1f)
//				{
//					isAxisLocked = true;
//					isXAxis = true;
//					dragOffset.z = 0f; // Ignore z-offset
//				}
//				else if (absZ > 0.1f)
//				{
//					isAxisLocked = true;
//					isXAxis = false;
//					dragOffset.x = 0f; // Ignore x-offset
//				}
//			}

//			// Compute new position
//			Vector3 newPos = originalPos;
//			float mouseRelX = currentPos.x - originalPos.x;
//			float initialRelX = initialMousePos.x - originalPos.x;

//			if (isAxisLocked)
//			{
//				if (isXAxis)
//				{
//					if (initialRelX <= 0f) // Left of center (e.g., x=2.7)
//					{
//						if (delta.x < -0.1f) // Drag left
//							newPos.x = Mathf.Clamp(currentPos.x, minX, originalPos.x);
//						else if (mouseRelX > 0f) // Drag right past center
//							newPos.x = Mathf.Clamp(currentPos.x, originalPos.x, maxX);
//						else
//							newPos.x = originalPos.x;
//					}
//					else // Right (e.g., x=3.3)
//					{
//						if (delta.x > 0.1f) // Drag right
//							newPos.x = Mathf.Clamp(currentPos.x, originalPos.x, maxX);
//						else if (mouseRelX < 0f) // Drag left past center
//							newPos.x = Mathf.Clamp(currentPos.x, minX, originalPos.x);
//						else
//							newPos.x = originalPos.x;
//					}
//					newPos.z = originalPos.z;
//				}
//				else
//				{
//					float mouseRelZ = currentPos.z - originalPos.z;
//					float initialRelZ = initialMousePos.z - originalPos.z;
//					if (initialRelZ <= 0f)
//					{
//						if (delta.z < -0.1f)
//							newPos.z = Mathf.Clamp(currentPos.z, minZ, originalPos.z);
//						else if (mouseRelZ > 0f)
//							newPos.z = Mathf.Clamp(currentPos.z, originalPos.z, maxZ);
//						else
//							newPos.z = originalPos.z;
//					}
//					else
//					{
//						if (delta.z > 0.1f)
//							newPos.z = Mathf.Clamp(currentPos.z, originalPos.z, maxZ);
//						else if (mouseRelZ < 0f)
//							newPos.z = Mathf.Clamp(currentPos.z, minZ, originalPos.z);
//						else
//							newPos.z = originalPos.z;
//					}
//					newPos.x = originalPos.x;
//				}
//			}
//			else
//			{
//				// Free movement, restrict until axis locked
//				newPos = originalPos;
//			}

//			// Validate target grid
//			int targetGridX = Mathf.RoundToInt(newPos.x);
//			int targetGridZ = Mathf.RoundToInt(newPos.z);
//			int targetIndex = targetGridZ * mapManager.Width + targetGridX;
//			bool isValidMove = true;

//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
//				{
//					isValidMove = false;
//					if (isAxisLocked)
//					{
//						if (isXAxis)
//							newPos.x = originalPos.x;
//						else
//							newPos.z = originalPos.z;
//					}
//				}
//			}

//			if (isValidMove)
//			{
//				draggedTile.transform.position = newPos;
//			}
//			else
//			{
//				draggedTile.transform.position = originalPos;
//			}

//			// Grid reset
//			Vector3 tilePos = draggedTile.transform.position;
//			int gridX = Mathf.RoundToInt(tilePos.x);
//			int gridZ = Mathf.RoundToInt(tilePos.z);
//			if ((gridX != startGridX || gridZ != startGridZ) &&
//				Mathf.Abs(tilePos.x - gridX) < 0.1f && Mathf.Abs(tilePos.z - gridZ) < 0.1f)
//			{
//				StartDragAtNewPosition(currentPos, gridX, gridZ);
//			}
//		}

//		private void StartDragAtNewPosition(Vector3 hitPos, int gridX, int gridZ)
//		{
//			int tileIndex = gridZ * mapManager.Width + gridX;
//			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length) return;

//			// Validate move
//			int targetIndex = tileIndex;
//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
//				{
//					return; // Invalid move
//				}
//			}

//			// Swap
//			Vector3 newTilePos = new Vector3(gridX, 0f, gridZ);
//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
//				{
//					mapManager.Tiles[targetIndex] = draggedTile;
//					mapManager.Tiles[dragIndex] = targetTile;

//					UpdateTileName(draggedTile, gridX, gridZ);

//					if (targetTile != null)
//					{
//						int origX = dragIndex % mapManager.Width;
//						int origZ = dragIndex / mapManager.Width;
//						targetTile.transform.position = new Vector3(origX, 0f, origZ);
//						UpdateTileName(targetTile, origX, origZ);
//					}
//				}
//				else
//				{
//					return;
//				}
//			}

//			// Update drag state
//			var properties = draggedTile.GetComponent<TileProperties>();
//			if (properties == null || !properties.CanBeDragged) return;

//			dragIndex = tileIndex;
//			originalPos = newTilePos;
//			if (isAxisLocked)
//			{
//				if (isXAxis)
//				{
//					dragOffset = new Vector3(hitPos.x - newTilePos.x, 0f, 0f);
//				}
//				else
//				{
//					dragOffset = new Vector3(0f, 0f, hitPos.z - newTilePos.z);
//				}
//			}
//			else
//			{
//				dragOffset = hitPos - newTilePos;
//			}
//			initialMousePos = hitPos;
//			GetDragBounds(dragIndex);
//			isAxisLocked = false;
//			isXAxis = false;
//			startGridX = gridX;
//			startGridZ = gridZ;
//		}

//		private void EndDrag()
//		{
//			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//			if (!mapPlane.Raycast(ray, out float distance))
//			{
//				ResetDrag();
//				return;
//			}

//			Vector3 tilePos = draggedTile.transform.position;
//			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
//			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
//			int targetIndex = z * mapManager.Width + x;

//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
//				{
//					mapManager.Tiles[targetIndex] = draggedTile;
//					mapManager.Tiles[dragIndex] = targetTile;

//					draggedTile.transform.position = new Vector3(x, 0f, z);
//					UpdateTileName(draggedTile, x, z);

//					if (targetTile != null)
//					{
//						int origX = dragIndex % mapManager.Width;
//						int origZ = dragIndex / mapManager.Width;
//						targetTile.transform.position = new Vector3(origX, 0f, origZ);
//						UpdateTileName(targetTile, origX, origZ);
//					}
//				}
//				else
//				{
//					draggedTile.transform.position = originalPos;
//				}
//			}
//			else
//			{
//				draggedTile.transform.position = originalPos;
//			}

//			ResetDrag();
//		}

//		private void GetDragBounds(int tileIndex)
//		{
//			int x = tileIndex % mapManager.Width;
//			int z = tileIndex / mapManager.Width;

//			minX = 0f;
//			maxX = mapManager.Width - 1;
//			minZ = 0f;
//			maxZ = mapManager.Height - 1;

//			for (int i = x - 1; i >= -1; i--)
//			{
//				if (i == -1) { minX = 0f; break; }
//				int checkIndex = z * mapManager.Width + i;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { minX = i + 1; break; }
//			}

//			for (int i = x + 1; i <= mapManager.Width; i++)
//			{
//				if (i == mapManager.Width) { maxX = mapManager.Width - 1; break; }
//				int checkIndex = z * mapManager.Width + i;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { maxX = i - 1; break; }
//			}

//			for (int j = z - 1; j >= -1; j--)
//			{
//				if (j == -1) { minZ = 0f; break; }
//				int checkIndex = j * mapManager.Width + x;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { minZ = j + 1; break; }
//			}

//			for (int j = z + 1; j <= mapManager.Height; j++)
//			{
//				if (j == mapManager.Height) { maxZ = mapManager.Height - 1; break; }
//				int checkIndex = j * mapManager.Width + x;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { maxZ = j - 1; break; }
//			}
//		}

//		private void UpdateTileName(GameObject tile, int x, int z)
//		{
//			string[] nameParts = tile.name.Split('_');
//			if (nameParts.Length >= 3)
//				tile.name = $"{nameParts[0]}_{x}_{z}";
//		}

//		private void ResetDrag()
//		{
//			isDragging = false;
//			dragIndex = -1;
//			draggedTile = null;
//			isAxisLocked = false;
//			isXAxis = false;
//			dragOffset = Vector3.zero;
//			initialMousePos = Vector3.zero;
//		}
//	}
//}