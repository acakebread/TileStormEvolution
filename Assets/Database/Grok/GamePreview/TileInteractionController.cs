using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private MapManager mapManager;
		private bool isDragging;
		private int dragTileIndex;
		private GameObject draggedTileObj;
		private Vector3 draggedTileOriginalPos;
		private Vector3 dragStartPos;
		private Vector3 dragOffset;
		private bool isAxisLocked;
		private bool isXAxis; // true for x-axis (left/right), false for z-axis (up/down)
		private float minX, maxX, minZ, maxZ;

		public void Initialize(MapManager manager, float moveSpeed, float threshold)
		{
			mapManager = manager;
			isDragging = false;
			dragTileIndex = -1;
			draggedTileObj = null;
			isAxisLocked = false;
			isXAxis = false;
			minX = maxX = minZ = maxZ = 0f;
			dragOffset = Vector3.zero;
			Debug.Log($"Initialized TileInteractionController: Width={mapManager.Width}, Height={mapManager.Height}");
		}

		public void UpdateInteractions()
		{
			HandleInput();
		}

		private void HandleInput()
		{
			if (Input.GetMouseButtonDown(0))
			{
				// Select tile
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				Debug.Log($"Mouse down: mousePos=({Input.mousePosition.x},{Input.mousePosition.y})");

				Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
				float distance;
				if (mapPlane.Raycast(ray, out distance))
				{
					Vector3 hitPos = ray.GetPoint(distance);
					Debug.Log($"Mouse down hitPos=({hitPos.x},{hitPos.z})");
					int x = Mathf.RoundToInt(hitPos.x);
					int z = Mathf.RoundToInt(hitPos.z);
					int tileIndex = z * mapManager.Width + x;

					if (tileIndex >= 0 && tileIndex < mapManager.Width * mapManager.Height && mapManager.Tiles[tileIndex] != null)
					{
						var properties = mapManager.Tiles[tileIndex].GetComponent<TileProperties>();
						if (properties != null && properties.CanBeDragged) // !bDock && bSlide
						{
							isDragging = true;
							dragTileIndex = tileIndex;
							draggedTileObj = mapManager.Tiles[tileIndex];
							draggedTileOriginalPos = draggedTileObj.transform.position;
							dragStartPos = hitPos;
							dragOffset = hitPos - draggedTileOriginalPos;
							isAxisLocked = false;
							isXAxis = false;
							GetDragBounds(tileIndex, out minX, out maxX, out minZ, out maxZ);
							Debug.Log($"Selected tile: index={tileIndex}, pos=({x},{z}), name={draggedTileObj.name}, bDock={properties.tileDef.bDock}, bSlide={properties.tileDef.bSlide}, offset=({dragOffset.x},{dragOffset.z}), bounds=[x:{minX},{maxX}, z:{minZ},{maxZ}]");
						}
						else
						{
							Debug.Log($"Tile not draggable at index={tileIndex}, pos=({x},{z}), bDock={properties?.tileDef.bDock}, bSlide={properties?.tileDef.bSlide}");
						}
					}
					else
					{
						Debug.Log($"No tile at index={tileIndex}, pos=({x},{z})");
					}
				}
				else
				{
					Debug.Log("Raycast failed to hit map plane");
				}
			}

			if (isDragging && Input.GetMouseButton(0))
			{
				// Drag tile
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				Debug.Log($"Dragging: mousePos=({Input.mousePosition.x},{Input.mousePosition.y})");

				Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
				float distance;
				if (mapPlane.Raycast(ray, out distance))
				{
					Vector3 currentPos = ray.GetPoint(distance);
					Debug.Log($"Dragging hitPos=({currentPos.x},{currentPos.z})");
					Vector3 delta = currentPos - dragStartPos;

					// Determine drag axis
					if (!isAxisLocked)
					{
						float absX = Mathf.Abs(delta.x);
						float absZ = Mathf.Abs(delta.z);
						if (absX > absZ && absX > 0.02f)
						{
							isAxisLocked = true;
							isXAxis = true;
							Debug.Log($"Locked to x-axis: delta=({delta.x},{delta.z})");
						}
						else if (absZ > 0.02f)
						{
							isAxisLocked = true;
							isXAxis = false;
							Debug.Log($"Locked to z-axis: delta=({delta.x},{delta.z})");
						}
					}

					// Compute clamped position, apply offset
					Vector3 newPos = draggedTileOriginalPos;
					Vector3 adjustedPos = currentPos - dragOffset;
					if (isAxisLocked)
					{
						if (isXAxis)
						{
							newPos.x = Mathf.Clamp(adjustedPos.x, minX, maxX);
							newPos.z = draggedTileOriginalPos.z;
						}
						else
						{
							newPos.z = Mathf.Clamp(adjustedPos.z, minZ, maxZ);
							newPos.x = draggedTileOriginalPos.x;
						}
					}
					else
					{
						newPos.x = Mathf.Clamp(adjustedPos.x, minX, maxX);
						newPos.z = Mathf.Clamp(adjustedPos.z, minZ, maxZ);
					}

					if (draggedTileObj != null)
					{
						draggedTileObj.transform.position = newPos;
						Debug.Log($"Dragging tile {dragTileIndex} to ({newPos.x},{newPos.z}), clamped=[x:{minX},{maxX}, z:{minZ},{maxZ}]");
					}
				}
				else
				{
					Debug.Log("Drag raycast failed to hit map plane");
				}
			}

			if (isDragging && Input.GetMouseButtonUp(0))
			{
				// Swap tiles
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				Debug.Log($"Mouse up: mousePos=({Input.mousePosition.x},{Input.mousePosition.y})");

				Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
				float distance;
				if (mapPlane.Raycast(ray, out distance))
				{
					Vector3 hitPos = ray.GetPoint(distance);
					Debug.Log($"Mouse up hitPos=({hitPos.x},{hitPos.z})");
					Vector3 tilePos = draggedTileObj.transform.position;
					Debug.Log($"Tile pos on release=({tilePos.x},{tilePos.z})");
					int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
					int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
					int targetIndex = z * mapManager.Width + x;

					if (targetIndex >= 0 && targetIndex < mapManager.Width * mapManager.Height && targetIndex != dragTileIndex)
					{
						GameObject targetTileObj = mapManager.Tiles[targetIndex];
						// Optional: Verify target is bDock=True or null (though bounds should ensure validity)
						var targetProps = targetTileObj?.GetComponent<TileProperties>();
						if (targetTileObj == null || (targetProps != null && targetProps.tileDef.bDock))
						{
							mapManager.Tiles[targetIndex] = draggedTileObj;
							mapManager.Tiles[dragTileIndex] = targetTileObj;

							if (draggedTileObj != null)
							{
								draggedTileObj.transform.position = new Vector3(x, 0f, z);
								string[] nameParts = draggedTileObj.name.Split('_');
								if (nameParts.Length >= 3)
								{
									draggedTileObj.name = $"{nameParts[0]}_{x}_{z}";
								}
							}
							if (targetTileObj != null)
							{
								int origX = dragTileIndex % mapManager.Width;
								int origZ = dragTileIndex / mapManager.Width;
								targetTileObj.transform.position = new Vector3(origX, 0f, origZ);
								string[] nameParts = targetTileObj.name.Split('_');
								if (nameParts.Length >= 3)
								{
									targetTileObj.name = $"{nameParts[0]}_{origX}_{origZ}";
								}
							}

							Debug.Log($"Swapped tile {dragTileIndex} ({dragTileIndex % mapManager.Width},{dragTileIndex / mapManager.Width}) with tile {targetIndex} ({x},{z})");
						}
						else
						{
							if (draggedTileObj != null)
							{
								draggedTileObj.transform.position = draggedTileOriginalPos;
							}
							Debug.Log($"No swap: targetIndex={targetIndex}, invalid target (bDock={targetProps?.tileDef.bDock})");
						}
					}
					else
					{
						if (draggedTileObj != null)
						{
							draggedTileObj.transform.position = draggedTileOriginalPos;
						}
						Debug.Log($"No swap: targetIndex={targetIndex}, valid={targetIndex >= 0 && targetIndex < mapManager.Width * mapManager.Height}, sameTile={targetIndex == dragTileIndex}");
					}
				}
				else
				{
					if (draggedTileObj != null)
					{
						draggedTileObj.transform.position = draggedTileOriginalPos;
					}
					Debug.Log("Mouse up raycast failed to hit map plane");
				}

				isDragging = false;
				dragTileIndex = -1;
				draggedTileObj = null;
				isAxisLocked = false;
				isXAxis = false;
				dragOffset = Vector3.zero;
				Debug.Log("End drag");
			}
		}

		private void GetDragBounds(int tileIndex, out float minX, out float maxX, out float minZ, out float maxZ)
		{
			int x = tileIndex % mapManager.Width;
			int z = tileIndex / mapManager.Width;

			// Default bounds are map edges
			minX = 0f;
			maxX = mapManager.Width - 1;
			minZ = 0f;
			maxZ = mapManager.Height - 1;

			// Check left (x-)
			for (int i = x - 1; i >= -1; i--)
			{
				if (i == -1)
				{
					minX = 0f;
					break;
				}
				int checkIndex = z * mapManager.Width + i;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) // null or bDock=False
				{
					minX = i + 1;
					break;
				}
			}

			// Check right (x+)
			for (int i = x + 1; i <= mapManager.Width; i++)
			{
				if (i == mapManager.Width)
				{
					maxX = mapManager.Width - 1;
					break;
				}
				int checkIndex = z * mapManager.Width + i;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) // null or bDock=False
				{
					maxX = i - 1;
					break;
				}
			}

			// Check down (z-)
			for (int j = z - 1; j >= -1; j--)
			{
				if (j == -1)
				{
					minZ = 0f;
					break;
				}
				int checkIndex = j * mapManager.Width + x;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) // null or bDock=False
				{
					minZ = j + 1;
					break;
				}
			}

			// Check up (z+)
			for (int j = z + 1; j <= mapManager.Height; j++)
			{
				if (j == mapManager.Height)
				{
					maxZ = mapManager.Height - 1;
					break;
				}
				int checkIndex = j * mapManager.Width + x;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) // null or bDock=False
				{
					maxZ = j - 1;
					break;
				}
			}

			Debug.Log($"Drag bounds for tile {tileIndex} ({x},{z}): [x:{minX},{maxX}, z:{minZ},{maxZ}]");
		}
	}
}




//using System;
//using UnityEngine;
//using System.Linq;
//using System.Collections.Generic;

//namespace GamePreviewNamespace
//{
//	public class TileInteractionController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private float tileMoveSpeed;
//		private float dragThreshold;
//		private Vector3 dragStartPos;
//		private bool isDragging;
//		private int dragTileIndex;
//		private int dragStride;
//		private GameObject draggedTileObj;
//		private Vector3 draggedTileOriginalPos;
//		private List<(int tileIndex, Vector3 newPos, Vector3 startPos, float timer, GameObject tileObj)> pendingMoves;
//		private Vector3 lastValidHitPos;

//		public void Initialize(MapManager manager, float moveSpeed, float threshold)
//		{
//			mapManager = manager;
//			tileMoveSpeed = moveSpeed;
//			dragThreshold = 0.3f;
//			isDragging = false;
//			dragStride = 0;
//			draggedTileObj = null;
//			pendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
//			lastValidHitPos = Vector3.zero;
//		}

//		public void UpdateInteractions()
//		{
//			HandleInput();
//			UpdatePendingMoves();
//		}

//		private void HandleInput()
//		{
//			if (Input.GetMouseButtonDown(0))
//			{
//				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//				Debug.Log($"Mouse down: mousePos=({Input.mousePosition.x},{Input.mousePosition.y})");
//				RaycastHit hit;
//				if (Physics.Raycast(ray, out hit))
//				{
//					Vector3 hitPos = hit.point;
//					int x = Mathf.FloorToInt(hitPos.x + 0.5f);
//					int z = Mathf.FloorToInt(hitPos.z + 0.5f);
//					int tileIndex = z * mapManager.Width + x;
//					Debug.Log($"Raycast hit: hitPos=({hitPos.x},{hitPos.z}), calculated=({x},{z}), tileIndex={tileIndex}");

//					if (tileIndex >= 0 && tileIndex < mapManager.Width * mapManager.Height)
//					{
//						var properties = mapManager.Tiles[tileIndex]?.GetComponent<TileProperties>();
//						if (properties != null && properties.hasNav && (properties.tileDef.bSlide || properties.tileDef.bRoll))
//						{
//							Debug.Log($"Mouse down at ({x},{z}), tileIndex={tileIndex}, szType={properties.tileDef.szType}, bSlide={properties.tileDef.bSlide}, bRoll={properties.tileDef.bRoll}, bDock={properties.tileDef.bDock}");
//							isDragging = true;
//							dragStartPos = hitPos;
//							lastValidHitPos = hitPos;
//							dragTileIndex = tileIndex;
//							dragStride = 0;
//							draggedTileObj = mapManager.Tiles[tileIndex];
//							if (draggedTileObj != null)
//							{
//								draggedTileOriginalPos = draggedTileObj.transform.position;
//							}
//							pendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
//							Debug.Log($"Start drag: tile={tileIndex} ({x},{z})");
//						}
//						else
//						{
//							Debug.Log($"Tile not directly interactable at tileIndex={tileIndex}, hasNav={properties?.hasNav}, bSlide={properties?.tileDef.bSlide}, bRoll={properties?.tileDef.bRoll}");
//						}
//					}
//					else
//					{
//						Debug.Log($"Invalid tileIndex={tileIndex}, map size={mapManager.Width * mapManager.Height}");
//					}
//				}
//				else
//				{
//					Debug.Log("Raycast missed on mouse down");
//				}
//			}

//			if (isDragging && Input.GetMouseButton(0))
//			{
//				Debug.Log($"Dragging: mousePos=({Input.mousePosition.x},{Input.mousePosition.y})");
//				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//				Vector3 currentPos;
//				RaycastHit hit;
//				if (Physics.Raycast(ray, out hit))
//				{
//					currentPos = hit.point;
//					Debug.Log($"Raycast hit during drag: hitPos=({currentPos.x},{currentPos.z})");
//				}
//				else
//				{
//					// Fallback to plane projection
//					Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//					float distance;
//					if (mapPlane.Raycast(ray, out distance))
//					{
//						currentPos = ray.GetPoint(distance);
//						Debug.Log($"Raycast missed, plane projection: currentPos=({currentPos.x},{currentPos.z})");
//					}
//					else
//					{
//						currentPos = lastValidHitPos;
//						Debug.Log($"Plane projection failed, using lastValidHitPos=({currentPos.x},{currentPos.z})");
//					}
//				}

//				// Update lastValidHitPos only if position changed significantly
//				if (Vector3.Distance(currentPos, lastValidHitPos) > 0.01f)
//				{
//					lastValidHitPos = currentPos;
//				}

//				Vector3 delta = currentPos - dragStartPos;
//				Debug.Log($"Dragging: delta=({delta.x},{delta.z}), dragTileIndex={dragTileIndex}");

//				int newStride = 0;
//				float absX = Mathf.Abs(delta.x);
//				float absZ = Mathf.Abs(delta.z);
//				if (absX > absZ && absX > 0.02f)
//				{
//					newStride = delta.x > 0 ? 1 : -1;
//				}
//				else if (absZ > 0.02f)
//				{
//					newStride = delta.z > 0 ? mapManager.Width : -mapManager.Width;
//				}

//				if (newStride != 0 && CanSlideTiles(dragTileIndex, newStride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap))
//				{
//					if (newStride != dragStride)
//					{
//						dragStride = newStride;
//						pendingMoves.Clear();
//						Debug.Log($"Direction set: stride={dragStride} ({(dragStride == 1 ? "East" : dragStride == -1 ? "West" : dragStride == mapManager.Width ? "North" : "South")})");
//					}

//					Debug.Log($"Chain formed: tiles=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//					pendingMoves.Clear();
//					float distance = dragStride == 1 || dragStride == -1 ? delta.x : delta.z;
//					Vector3 direction = dragStride == 1 ? Vector3.right :
//									   dragStride == -1 ? Vector3.left :
//									   dragStride == mapManager.Width ? Vector3.forward :
//									   Vector3.back;

//					for (int i = 0; i < tilesToMove.Count; i++)
//					{
//						int tile = tilesToMove[i];
//						int nextTile = isRollGroup && willWrap ? GetWrappedTile(tilesToMove, tile, dragStride) : tile + dragStride;
//						Vector3 newPos = new Vector3(nextTile % mapManager.Width, 0f, nextTile / mapManager.Width);
//						Vector3 startPos = new Vector3(tile % mapManager.Width, 0f, tile / mapManager.Width);
//						GameObject tileObj = tile == dragTileIndex ? draggedTileObj : mapManager.Tiles[tile];
//						if (tileObj != null)
//						{
//							pendingMoves.Add((tile, newPos, startPos, 0f, tileObj));
//						}
//					}

//					foreach (var move in pendingMoves)
//					{
//						if (move.tileObj != null)
//						{
//							float visualDistance = Mathf.Clamp(distance, -1f, 1f);
//							move.tileObj.transform.position = move.startPos + direction * visualDistance;
//						}
//					}
//				}
//				else if (newStride != 0)
//				{
//					int[] possibleStrides = { 1, -1, mapManager.Width, -mapManager.Width };
//					foreach (int altStride in possibleStrides)
//					{
//						if (altStride != newStride && CanSlideTiles(dragTileIndex, altStride, out tilesToMove, out isRollGroup, out willWrap))
//						{
//							newStride = altStride;
//							dragStride = newStride;
//							pendingMoves.Clear();
//							Debug.Log($"Alternative direction set: stride={dragStride} ({(dragStride == 1 ? "East" : dragStride == -1 ? "West" : dragStride == mapManager.Width ? "North" : "South")})");

//							float distance = dragStride == 1 || dragStride == -1 ? delta.x : delta.z;
//							Vector3 direction = dragStride == 1 ? Vector3.right :
//											   dragStride == -1 ? Vector3.left :
//											   dragStride == mapManager.Width ? Vector3.forward :
//											   Vector3.back;

//							for (int i = 0; i < tilesToMove.Count; i++)
//							{
//								int tile = tilesToMove[i];
//								int nextTile = isRollGroup && willWrap ? GetWrappedTile(tilesToMove, tile, dragStride) : tile + dragStride;
//								Vector3 newPos = new Vector3(nextTile % mapManager.Width, 0f, nextTile / mapManager.Width);
//								Vector3 startPos = new Vector3(tile % mapManager.Width, 0f, tile / mapManager.Width);
//								GameObject tileObj = tile == dragTileIndex ? draggedTileObj : mapManager.Tiles[tile];
//								if (tileObj != null)
//								{
//									pendingMoves.Add((tile, newPos, startPos, 0f, tileObj));
//								}
//							}

//							foreach (var move in pendingMoves)
//							{
//								if (move.tileObj != null)
//								{
//									float visualDistance = Mathf.Clamp(distance, -1f, 1f);
//									move.tileObj.transform.position = move.startPos + direction * visualDistance;
//								}
//							}
//							break;
//						}
//					}

//					if (pendingMoves.Count == 0)
//					{
//						Debug.Log($"Direction rejected: stride={newStride}, reason=Invalid move");
//						if (draggedTileObj != null)
//							draggedTileObj.transform.position = draggedTileOriginalPos;
//						pendingMoves.Clear();
//					}
//				}
//				else
//				{
//					Debug.Log($"Direction rejected: stride=0, reason=No direction");
//					if (draggedTileObj != null)
//					{
//						// Slight movement for feedback
//						Vector3 visualDelta = delta.normalized * Mathf.Min(delta.magnitude, 0.2f);
//						draggedTileObj.transform.position = draggedTileOriginalPos + visualDelta;
//					}
//					pendingMoves.Clear();
//				}
//			}

//			if (isDragging && Input.GetMouseButtonUp(0))
//			{
//				Debug.Log($"Mouse up: mousePos=({Input.mousePosition.x},{Input.mousePosition.y})");
//				bool commitMove = false;
//				float distance = 0f;
//				if (draggedTileObj != null && dragStride != 0)
//				{
//					Vector3 delta = draggedTileObj.transform.position - draggedTileOriginalPos;
//					distance = dragStride == 1 || dragStride == -1 ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z);
//					commitMove = distance >= dragThreshold;
//					Debug.Log($"Mouse up: distance={distance}, commitMove={commitMove}, threshold={dragThreshold}");
//				}

//				if (commitMove && dragStride != 0 && CanSlideTiles(dragTileIndex, dragStride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap))
//				{
//					Debug.Log($"Committing move: tiles=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//					SlideTiles(dragTileIndex, dragStride, tilesToMove, isRollGroup, willWrap);
//				}
//				else
//				{
//					Debug.Log($"Drag cancelled: commitMove={commitMove}, stride={dragStride}");
//					foreach (var move in pendingMoves)
//					{
//						if (move.tileObj != null)
//						{
//							move.tileObj.transform.position = move.startPos;
//						}
//					}
//				}

//				if (draggedTileObj != null)
//				{
//					draggedTileObj.transform.position = draggedTileOriginalPos;
//				}

//				isDragging = false;
//				dragStride = 0;
//				draggedTileObj = null;
//				pendingMoves.Clear();
//				Debug.Log("End drag");
//			}
//		}

//		private void UpdatePendingMoves()
//		{
//			if (pendingMoves == null)
//				return;

//			List<(int tileIndex, Vector3 newPos, Vector3 startPos, float timer, GameObject)> newPendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
//			foreach (var move in pendingMoves)
//			{
//				if (move.tileObj != null)
//				{
//					float newTimer = move.timer + Time.deltaTime * tileMoveSpeed;
//					float t = Mathf.Clamp01(newTimer);
//					move.tileObj.transform.position = Vector3.Lerp(move.startPos, move.newPos, t);
//					if (t < 1f)
//					{
//						newPendingMoves.Add((move.tileIndex, move.newPos, move.startPos, newTimer, move.tileObj));
//					}
//				}
//			}
//			pendingMoves = newPendingMoves;
//		}

//		private bool CanSlideTiles(int startTile, int stride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap)
//		{
//			tilesToMove = new List<int>();
//			isRollGroup = false;
//			willWrap = false;

//			if (startTile < 0 || startTile >= mapManager.Width * mapManager.Height)
//			{
//				Debug.LogError($"CanSlideTiles: Invalid startTile={startTile}");
//				return false;
//			}

//			var startProperties = mapManager.Tiles[startTile]?.GetComponent<TileProperties>();
//			if (startProperties == null || (!startProperties.tileDef.bSlide && !startProperties.tileDef.bRoll))
//			{
//				Debug.Log($"CanSlideTiles: Invalid start tile {startTile}, bSlide={startProperties?.tileDef.bSlide}, bRoll={startProperties?.tileDef.bRoll}");
//				return false;
//			}

//			int currentTile = startTile;
//			tilesToMove.Add(currentTile);
//			bool isStartRoll = startProperties.tileDef.bRoll;
//			isRollGroup = isStartRoll;

//			int currentX = currentTile % mapManager.Width;
//			int currentZ = currentTile / mapManager.Width;
//			bool withinBounds = stride == 1 ? (currentX < mapManager.Width - 1) :
//							   stride == -1 ? (currentX > 0) :
//							   stride == mapManager.Width ? (currentZ < mapManager.Height - 1) :
//							   stride == -mapManager.Width ? (currentZ > 0) : false;

//			if (!withinBounds)
//			{
//				if (isStartRoll)
//				{
//					willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
//					Debug.Log($"CanSlideTiles: Out of bounds, willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//					return willWrap;
//				}
//				Debug.Log($"CanSlideTiles: Start tile out of bounds: ({currentX},{currentZ}), stride={stride}");
//				return false;
//			}

//			int nextTile = currentTile + stride;
//			while (withinBounds && nextTile >= 0 && nextTile < mapManager.Width * mapManager.Height)
//			{
//				var nextProperties = mapManager.Tiles[nextTile]?.GetComponent<TileProperties>();
//				if (nextProperties == null)
//				{
//					Debug.Log($"CanSlideTiles: Found gap at {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width}), tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//					return true;
//				}

//				if (!nextProperties.tileDef.bSlide && !nextProperties.tileDef.bRoll)
//				{
//					if (isRollGroup)
//					{
//						willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
//						Debug.Log($"CanSlideTiles: Hit fixed tile at {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width}), willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//						return willWrap;
//					}
//					Debug.Log($"CanSlideTiles: Blocked by fixed tile {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width}), szType={nextProperties.tileDef.szType}");
//					return false;
//				}

//				tilesToMove.Add(nextTile);
//				isRollGroup = isRollGroup || nextProperties.tileDef.bRoll;
//				currentTile = nextTile;
//				currentX = currentTile % mapManager.Width;
//				currentZ = currentTile / mapManager.Width;
//				nextTile = currentTile + stride;
//				withinBounds = stride == 1 ? (currentX < mapManager.Width - 1) :
//							  stride == -1 ? (currentX > 0) :
//							  stride == mapManager.Width ? (currentZ < mapManager.Height - 1) :
//							  stride == -mapManager.Width ? (currentZ > 0) : false;
//			}

//			if (isRollGroup)
//			{
//				willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
//				Debug.Log($"CanSlideTiles: End of chain, willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//				return willWrap;
//			}

//			Debug.Log($"CanSlideTiles: No gap or valid wrap found, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
//			return false;
//		}

//		private bool CheckRollWrap(int startTile, int stride, out List<int> tilesToMove)
//		{
//			tilesToMove = new List<int>();
//			int currentTile = startTile;
//			var startDef = mapManager.Tiles[startTile]?.GetComponent<TileProperties>();
//			if (startDef == null || !startDef.tileDef.bRoll)
//				return false;

//			tilesToMove.Add(currentTile);
//			bool withinBounds = stride == 1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
//							   stride == -1 ? (currentTile % mapManager.Width > 0) :
//							   stride == mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) :
//							   stride == -mapManager.Width ? (currentTile / mapManager.Width > 0) : false;

//			int nextTile = currentTile + stride;
//			while (withinBounds && nextTile >= 0 && nextTile < mapManager.Width * mapManager.Height)
//			{
//				var nextDef = mapManager.Tiles[nextTile]?.GetComponent<TileProperties>();
//				if (nextDef == null || !nextDef.tileDef.bRoll)
//					break;
//				tilesToMove.Add(nextTile);
//				currentTile = nextTile;
//				nextTile = currentTile + stride;
//				withinBounds = stride == 1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
//							  stride == -1 ? (currentTile % mapManager.Width > 0) :
//							  stride == mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) :
//							  stride == -mapManager.Width ? (currentTile / mapManager.Width > 0) : false;
//			}

//			currentTile = startTile;
//			int prevTile = currentTile - stride;
//			withinBounds = stride == 1 ? (currentTile % mapManager.Width > 0) :
//						  stride == -1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
//						  stride == mapManager.Width ? (currentTile / mapManager.Width > 0) :
//						  stride == -mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) : false;

//			while (withinBounds && prevTile >= 0 && prevTile < mapManager.Width * mapManager.Height)
//			{
//				var prevDef = mapManager.Tiles[prevTile]?.GetComponent<TileProperties>();
//				if (prevDef == null || !prevDef.tileDef.bRoll)
//					break;
//				tilesToMove.Insert(0, prevTile);
//				currentTile = prevTile;
//				prevTile = currentTile - stride;
//				withinBounds = stride == 1 ? (currentTile % mapManager.Width > 0) :
//							  stride == -1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
//							  stride == mapManager.Width ? (currentTile / mapManager.Width > 0) :
//							  stride == -mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) : false;
//			}

//			int firstTile = tilesToMove[0];
//			int lastTile = tilesToMove[tilesToMove.Count - 1];
//			int beforeFirst = stride > 0 ? firstTile - stride : lastTile - stride;
//			int afterLast = stride > 0 ? lastTile + stride : firstTile + stride;

//			bool beforeValid = beforeFirst >= 0 && beforeFirst < mapManager.Width * mapManager.Height;
//			bool afterValid = afterLast >= 0 && afterLast < mapManager.Width * mapManager.Height;

//			var beforeDef = beforeValid ? mapManager.Tiles[beforeFirst]?.GetComponent<TileProperties>() : null;
//			var afterDef = afterValid ? mapManager.Tiles[afterLast]?.GetComponent<TileProperties>() : null;

//			bool isBounded = (beforeValid && beforeDef != null && !beforeDef.tileDef.bSlide && !beforeDef.tileDef.bRoll) ||
//							 (afterValid && afterDef != null && !afterDef.tileDef.bSlide && !afterDef.tileDef.bRoll) ||
//							 (!beforeValid || !afterValid);

//			Debug.Log($"CheckRollWrap: tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}], isBounded={isBounded}, beforeValid={beforeValid}, afterValid={afterValid}");
//			return isBounded && tilesToMove.Count > 1;
//		}

//		private int GetWrappedTile(List<int> tilesToMove, int tile, int stride)
//		{
//			int index = tilesToMove.IndexOf(tile);
//			int nextIndex = index + (stride > 0 ? 1 : -1);
//			if (nextIndex < 0)
//				nextIndex = tilesToMove.Count - 1;
//			else if (nextIndex >= tilesToMove.Count)
//				nextIndex = 0;
//			return tilesToMove[nextIndex];
//		}

//		private void SlideTiles(int startTile, int stride, List<int> tilesToMove, bool isRollGroup, bool willWrap)
//		{
//			if (isRollGroup && willWrap)
//			{
//				GameObject[] tempTiles = new GameObject[tilesToMove.Count];
//				for (int i = 0; i < tilesToMove.Count; i++)
//				{
//					tempTiles[i] = mapManager.Tiles[tilesToMove[i]];
//				}

//				for (int i = 0; i < tilesToMove.Count; i++)
//				{
//					int tile = tilesToMove[i];
//					int newTile = GetWrappedTile(tilesToMove, tile, stride);
//					mapManager.Tiles[tile] = tempTiles[tilesToMove.IndexOf(newTile)];
//					if (mapManager.Tiles[tile] != null)
//					{
//						int newX = tile % mapManager.Width;
//						int newZ = tile / mapManager.Width;
//						mapManager.Tiles[tile].transform.position = new Vector3(newX, 0f, newZ);
//						string[] nameParts = mapManager.Tiles[tile].name.Split('_');
//						if (nameParts.Length >= 3)
//						{
//							mapManager.Tiles[tile].name = $"{nameParts[0]}_{newX}_{newZ}";
//						}
//					}
//				}
//			}
//			else
//			{
//				int gapTile = tilesToMove[tilesToMove.Count - 1] + stride;
//				if (gapTile >= 0 && gapTile < mapManager.Width * mapManager.Height)
//				{
//					GameObject gapTileObj = mapManager.Tiles[gapTile];
//					for (int i = tilesToMove.Count - 1; i >= 0; i--)
//					{
//						int tile = tilesToMove[i];
//						int nextTile = i < tilesToMove.Count - 1 ? tilesToMove[i + 1] : gapTile;
//						mapManager.Tiles[nextTile] = mapManager.Tiles[tile];
//						if (mapManager.Tiles[nextTile] != null)
//						{
//							int newX = nextTile % mapManager.Width;
//							int newZ = nextTile / mapManager.Width;
//							mapManager.Tiles[nextTile].transform.position = new Vector3(newX, 0f, newZ);
//							string[] nameParts = mapManager.Tiles[nextTile].name.Split('_');
//							if (nameParts.Length >= 3)
//							{
//								mapManager.Tiles[nextTile].name = $"{nameParts[0]}_{newX}_{newZ}";
//							}
//						}
//					}
//					mapManager.Tiles[tilesToMove[0]] = gapTileObj;
//					if (mapManager.Tiles[tilesToMove[0]] != null)
//					{
//						int newX = tilesToMove[0] % mapManager.Width;
//						int newZ = tilesToMove[0] / mapManager.Width;
//						mapManager.Tiles[tilesToMove[0]].transform.position = new Vector3(newX, 0f, newZ);
//						string[] nameParts = mapManager.Tiles[tilesToMove[0]].name.Split('_');
//						if (nameParts.Length >= 3)
//						{
//							mapManager.Tiles[tilesToMove[0]].name = $"{nameParts[0]}_{newX}_{newZ}";
//						}
//					}
//				}
//				else
//				{
//					Debug.LogWarning($"SlideTiles: Invalid gapTile={gapTile}, skipping update");
//					return;
//				}
//			}

//			Debug.Log($"SlideTiles: startTile={startTile} ({startTile % mapManager.Width},{startTile / mapManager.Width}), stride={stride}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}], isRollGroup={isRollGroup}, willWrap={willWrap}");
//		}
//	}
//}