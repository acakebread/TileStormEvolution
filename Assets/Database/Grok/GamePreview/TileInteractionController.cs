using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static GameDatabase.DatabaseLoader;
using System;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private MapManager mapManager;
		private float tileMoveSpeed;
		private float dragThreshold;
		private Vector3 dragStartPos;
		private bool isDragging;
		private int dragTileIndex;
		private int dragStride;
		private GameObject draggedTileObj;
		private Vector3 draggedTileOriginalPos;
		private List<(int tileIndex, Vector3 newPos, Vector3 startPos, float timer, GameObject tileObj)> pendingMoves;

		public void Initialize(MapManager manager, float moveSpeed, float threshold)
		{
			mapManager = manager;
			tileMoveSpeed = moveSpeed;
			dragThreshold = threshold;
			isDragging = false;
			dragStride = 0;
			draggedTileObj = null;
			pendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
		}

		public void UpdateInteractions()
		{
			HandleInput();
			UpdatePendingMoves();
		}

		private void HandleInput()
		{
			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				RaycastHit hit;
				if (Physics.Raycast(ray, out hit))
				{
					Vector3 hitPos = hit.point;
					int x = Mathf.FloorToInt(hitPos.x + 0.5f);
					int z = Mathf.FloorToInt(hitPos.z + 0.5f);
					int tileIndex = z * mapManager.Width + x;
					Debug.Log($"Raycast hit: hitPos=({hitPos.x},{hitPos.z}), calculated=({x},{z}), tileIndex={tileIndex}");

					if (tileIndex >= 0 && tileIndex < mapManager.TileMap.Length)
					{
						TileDef tileDef = mapManager.GetTileDefAt(tileIndex);
						if (tileDef != null && tileDef.szType != "tile_invisible")
						{
							Debug.Log($"Mouse down at ({x},{z}), tileIndex={tileIndex}, szType={tileDef.szType}, bSlide={tileDef.bSlide}, bRoll={tileDef.bRoll}, bDock={tileDef.bDock}");
							if (tileDef.bSlide || tileDef.bRoll)
							{
								isDragging = true;
								dragStartPos = hitPos;
								dragTileIndex = tileIndex;
								dragStride = 0;
								draggedTileObj = FindTileObject(tileIndex);
								if (draggedTileObj != null)
								{
									draggedTileOriginalPos = draggedTileObj.transform.position;
								}
								pendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
								Debug.Log($"Start drag: tile={tileIndex} ({x},{z})");
							}
							else
							{
								Debug.Log("Tile not movable (bSlide=false, bRoll=false)");
							}
						}
						else
						{
							Debug.Log($"No TileDef or invisible tile at tileIndex={tileIndex}");
						}
					}
					else
					{
						Debug.Log($"Invalid tileIndex={tileIndex}, tileMap.Length={mapManager.TileMap.Length}");
					}
				}
				else
				{
					Debug.Log("Raycast missed");
				}
			}

			if (isDragging && Input.GetMouseButton(0))
			{
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				RaycastHit hit;
				if (Physics.Raycast(ray, out hit))
				{
					Vector3 currentPos = hit.point;
					Vector3 delta = currentPos - dragStartPos;
					Debug.Log($"Dragging: delta=({delta.x},{delta.z}), dragTileIndex={dragTileIndex}");

					int newStride = 0;
					float absX = Mathf.Abs(delta.x);
					float absZ = Mathf.Abs(delta.z);
					if (absX > absZ && absX > 0.1f)
					{
						newStride = delta.x > 0 ? 1 : -1;
					}
					else if (absZ > 0.1f)
					{
						newStride = delta.z > 0 ? mapManager.Width : -mapManager.Width;
					}

					if (newStride != 0 && CanSlideTiles(dragTileIndex, newStride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap))
					{
						if (newStride != dragStride)
						{
							dragStride = newStride;
							pendingMoves.Clear();
							Debug.Log($"Direction set: stride={dragStride} ({(dragStride == 1 ? "East" : dragStride == -1 ? "West" : dragStride == mapManager.Width ? "North" : "South")})");
						}

						Debug.Log($"Chain formed: tiles=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
						pendingMoves.Clear();
						float distance = dragStride == 1 || dragStride == -1 ? delta.x : delta.z;
						Vector3 direction = dragStride == 1 ? Vector3.right :
										   dragStride == -1 ? Vector3.left :
										   dragStride == mapManager.Width ? Vector3.forward :
										   Vector3.back;

						for (int i = 0; i < tilesToMove.Count; i++)
						{
							int tile = tilesToMove[i];
							int nextTile = isRollGroup && willWrap ? GetWrappedTile(tilesToMove, tile, dragStride) : tile + dragStride;
							Vector3 newPos = new Vector3(nextTile % mapManager.Width, 0f, nextTile / mapManager.Width);
							Vector3 startPos = new Vector3(tile % mapManager.Width, 0f, tile / mapManager.Width);
							GameObject tileObj = tile == dragTileIndex ? draggedTileObj : FindTileObject(tile);
							if (tileObj != null)
							{
								pendingMoves.Add((tile, newPos, startPos, 0f, tileObj));
							}
						}

						foreach (var move in pendingMoves)
						{
							if (move.tileObj != null)
							{
								float visualDistance = Mathf.Clamp(distance, -1f, 1f); // Simplified for chain
								move.tileObj.transform.position = move.startPos + direction * visualDistance;
							}
						}
					}
					else
					{
						Debug.Log($"Direction rejected: stride={newStride}, reason={(newStride == 0 ? "No direction" : "Invalid move")}");
						if (draggedTileObj != null)
							draggedTileObj.transform.position = draggedTileOriginalPos;
						pendingMoves.Clear();
					}
				}
				else
				{
					Debug.Log("Dragging raycast missed");
				}
			}

			if (isDragging && Input.GetMouseButtonUp(0))
			{
				bool commitMove = false;
				float distance = 0f;
				if (draggedTileObj != null && dragStride != 0)
				{
					Vector3 delta = draggedTileObj.transform.position - draggedTileOriginalPos;
					distance = dragStride == 1 || dragStride == -1 ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z);
					commitMove = distance >= dragThreshold;
					Debug.Log($"Mouse up: distance={distance}, commitMove={commitMove}, threshold={dragThreshold}");
				}

				if (commitMove && dragStride != 0 && CanSlideTiles(dragTileIndex, dragStride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap))
				{
					Debug.Log($"Committing move: tiles=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
					SlideTiles(dragTileIndex, dragStride, tilesToMove, isRollGroup, willWrap);
				}
				else
				{
					Debug.Log($"Drag cancelled: commitMove={commitMove}, stride={dragStride}");
					// Removed tileMap writes to prevent corruption
					foreach (var move in pendingMoves)
					{
						if (move.tileObj != null)
						{
							move.tileObj.transform.position = move.startPos;
						}
					}
				}

				if (draggedTileObj != null)
				{
					draggedTileObj.transform.position = draggedTileOriginalPos;
				}

				isDragging = false;
				dragStride = 0;
				draggedTileObj = null;
				pendingMoves.Clear();
				mapManager.UpdateTilePositions();
				Debug.Log($"TileMap after: [{string.Join(", ", mapManager.TileMap.Take(10))}...] (first 10)");
				Debug.Log("End drag");
			}
		}

		private GameObject FindTileObject(int tileIndex)
		{
			int x = tileIndex % mapManager.Width;
			int z = tileIndex / mapManager.Width;
			foreach (Transform tile in mapManager.MapRoot.transform)
			{
				if (tile.name.Contains("Eggbot"))
					continue;
				string[] parts = tile.name.Split('_');
				if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out int tx) && int.TryParse(parts[parts.Length - 1], out int tz))
				{
					if (tx == x && tz == z)
						return tile.gameObject;
				}
			}
			Debug.LogWarning($"FindTileObject: No object for tileIndex={tileIndex} ({x},{z})");
			return null;
		}

		private void UpdatePendingMoves()
		{
			if (pendingMoves == null)
				return;

			List<(int tileIndex, Vector3 newPos, Vector3 startPos, float timer, GameObject tileObj)> newPendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
			foreach (var move in pendingMoves)
			{
				if (move.tileObj != null)
				{
					float newTimer = move.timer + Time.deltaTime * tileMoveSpeed;
					float t = Mathf.Clamp01(newTimer);
					move.tileObj.transform.position = Vector3.Lerp(move.startPos, move.newPos, t);
					if (t < 1f)
					{
						newPendingMoves.Add((move.tileIndex, move.newPos, move.startPos, newTimer, move.tileObj));
					}
				}
			}
			pendingMoves = newPendingMoves;
		}

		private bool CanSlideTiles(int startTile, int stride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap)
		{
			tilesToMove = new List<int>();
			isRollGroup = false;
			willWrap = false;

			if (startTile < 0 || startTile >= mapManager.TileMap.Length)
			{
				Debug.LogError($"CanSlideTiles: Invalid startTile={startTile}");
				return false;
			}

			TileDef startDef = mapManager.GetTileDefAt(startTile);
			if (startDef == null || (!startDef.bSlide && !startDef.bRoll))
			{
				Debug.Log($"CanSlideTiles: Invalid start tile {startTile}, bSlide={startDef?.bSlide}, bRoll={startDef?.bRoll}");
				return false;
			}

			int currentTile = startTile;
			tilesToMove.Add(currentTile);
			bool isStartRoll = startDef.bRoll;
			isRollGroup = isStartRoll;
			bool hasDock = startDef.bDock;

			int currentX = currentTile % mapManager.Width;
			int currentZ = currentTile / mapManager.Width;
			bool withinBounds = stride == 1 ? (currentX < mapManager.Width - 1) :
							   stride == -1 ? (currentX > 0) :
							   stride == mapManager.Width ? (currentZ < mapManager.Height - 1) :
							   stride == -mapManager.Width ? (currentZ > 0) : false;

			if (!withinBounds)
			{
				if (isStartRoll && !hasDock)
				{
					willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
					Debug.Log($"CanSlideTiles: Out of bounds, willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
					return willWrap;
				}
				Debug.Log($"CanSlideTiles: Start tile out of bounds: ({currentX},{currentZ}), stride={stride}");
				return false;
			}

			int nextTile = currentTile + stride;
			while (withinBounds && nextTile >= 0 && nextTile < mapManager.TileMap.Length)
			{
				TileDef nextDef = mapManager.GetTileDefAt(nextTile);
				if (nextDef == null)
				{
					Debug.Log($"CanSlideTiles: No TileDef at nextTile={nextTile}");
					return false;
				}

				if (nextDef.szType == "tile_empty" || nextDef.szType == "tile_invisible")
				{
					Debug.Log($"CanSlideTiles: Found gap at {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width}), tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
					return true;
				}

				// Tightened check: stop at any non-movable tile in chain
				if (!nextDef.bSlide && !nextDef.bRoll)
				{
					if (isRollGroup && !hasDock)
					{
						willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
						Debug.Log($"CanSlideTiles: Hit fixed tile at {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width}), willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
						return willWrap;
					}
					Debug.Log($"CanSlideTiles: Blocked by fixed tile {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width}), szType={nextDef.szType}");
					return false;
				}

				if (nextDef.bDock)
				{
					hasDock = true;
					Debug.Log($"CanSlideTiles: Blocked by docked tile {nextTile} ({nextTile % mapManager.Width},{nextTile / mapManager.Width})");
					return false;
				}

				tilesToMove.Add(nextTile);
				isRollGroup = isRollGroup || nextDef.bRoll;
				currentTile = nextTile;
				currentX = currentTile % mapManager.Width;
				currentZ = currentTile / mapManager.Width;
				nextTile = currentTile + stride;
				withinBounds = stride == 1 ? (currentX < mapManager.Width - 1) :
							  stride == -1 ? (currentX > 0) :
							  stride == mapManager.Width ? (currentZ < mapManager.Height - 1) :
							  stride == -mapManager.Width ? (currentZ > 0) : false;
			}

			if (isRollGroup && !hasDock)
			{
				willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
				Debug.Log($"CanSlideTiles: End of chain, willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
				return willWrap;
			}

			Debug.Log($"CanSlideTiles: No gap or valid wrap found, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
			return false;
		}

		private bool CheckRollWrap(int startTile, int stride, out List<int> tilesToMove)
		{
			tilesToMove = new List<int>();
			int currentTile = startTile;
			TileDef startDef = mapManager.GetTileDefAt(startTile);
			if (startDef == null || !startDef.bRoll)
				return false;

			tilesToMove.Add(currentTile);
			bool withinBounds = stride == 1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
							   stride == -1 ? (currentTile % mapManager.Width > 0) :
							   stride == mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) :
							   stride == -mapManager.Width ? (currentTile / mapManager.Width > 0) : false;

			int nextTile = currentTile + stride;
			while (withinBounds && nextTile >= 0 && nextTile < mapManager.TileMap.Length)
			{
				TileDef nextDef = mapManager.GetTileDefAt(nextTile);
				if (nextDef == null || !nextDef.bRoll || nextDef.bDock)
					break;
				tilesToMove.Add(nextTile);
				currentTile = nextTile;
				nextTile = currentTile + stride;
				withinBounds = stride == 1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
							  stride == -1 ? (currentTile % mapManager.Width > 0) :
							  stride == mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) :
							  stride == -mapManager.Width ? (currentTile / mapManager.Width > 0) : false;
			}

			currentTile = startTile;
			int prevTile = currentTile - stride;
			withinBounds = stride == 1 ? (currentTile % mapManager.Width > 0) :
						  stride == -1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
						  stride == mapManager.Width ? (currentTile / mapManager.Width > 0) :
						  stride == -mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) : false;

			while (withinBounds && prevTile >= 0 && prevTile < mapManager.TileMap.Length)
			{
				TileDef prevDef = mapManager.GetTileDefAt(prevTile);
				if (prevDef == null || !prevDef.bRoll || prevDef.bDock)
					break;
				tilesToMove.Insert(0, prevTile);
				currentTile = prevTile;
				prevTile = currentTile - stride;
				withinBounds = stride == 1 ? (currentTile % mapManager.Width > 0) :
							  stride == -1 ? (currentTile % mapManager.Width < mapManager.Width - 1) :
							  stride == mapManager.Width ? (currentTile / mapManager.Width > 0) :
							  stride == -mapManager.Width ? (currentTile / mapManager.Width < mapManager.Height - 1) : false;
			}

			int firstTile = tilesToMove[0];
			int lastTile = tilesToMove[tilesToMove.Count - 1];
			int beforeFirst = stride > 0 ? firstTile - stride : lastTile - stride;
			int afterLast = stride > 0 ? lastTile + stride : firstTile + stride;

			bool beforeValid = beforeFirst >= 0 && beforeFirst < mapManager.TileMap.Length;
			bool afterValid = afterLast >= 0 && afterLast < mapManager.TileMap.Length;

			TileDef beforeDef = beforeValid ? mapManager.GetTileDefAt(beforeFirst) : null;
			TileDef afterDef = afterValid ? mapManager.GetTileDefAt(afterLast) : null;

			bool isBounded = (beforeValid && beforeDef != null && !beforeDef.bSlide && !beforeDef.bRoll) ||
							 (afterValid && afterDef != null && !afterDef.bSlide && !afterDef.bRoll) ||
							 (!beforeValid || !afterValid);

			Debug.Log($"CheckRollWrap: tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}], isBounded={isBounded}, beforeValid={beforeValid}, afterValid={afterValid}");
			return isBounded && tilesToMove.Count > 1;
		}

		private int GetWrappedTile(List<int> tilesToMove, int tile, int stride)
		{
			int index = tilesToMove.IndexOf(tile);
			int nextIndex = index + (stride > 0 ? 1 : -1);
			if (nextIndex < 0)
				nextIndex = tilesToMove.Count - 1;
			else if (nextIndex >= tilesToMove.Count)
				nextIndex = 0;
			return tilesToMove[nextIndex];
		}

		private void SlideTiles(int startTile, int stride, List<int> tilesToMove, bool isRollGroup, bool willWrap)
		{
			int[] newTileMap = new int[mapManager.TileMap.Length]; // Avoid Clone to ensure clean state
			Array.Copy(mapManager.TileMap, newTileMap, mapManager.TileMap.Length);

			if (isRollGroup && willWrap)
			{
				int[] tempMap = new int[mapManager.TileMap.Length];
				Array.Copy(mapManager.TileMap, tempMap, mapManager.TileMap.Length);
				foreach (int tile in tilesToMove)
				{
					int newTile = GetWrappedTile(tilesToMove, tile, stride);
					newTileMap[tile] = tempMap[newTile];
				}
			}
			else
			{
				// Move tiles toward gap
				int gapTile = tilesToMove[tilesToMove.Count - 1] + stride;
				if (gapTile >= 0 && gapTile < mapManager.TileMap.Length)
				{
					for (int i = tilesToMove.Count - 1; i >= 0; i--)
					{
						int tile = tilesToMove[i];
						int nextTile = i < tilesToMove.Count - 1 ? tilesToMove[i + 1] : gapTile;
						newTileMap[nextTile] = mapManager.TileMap[tile];
					}
					// Fill first tile with gap’s original content
					newTileMap[tilesToMove[0]] = mapManager.TileMap[gapTile];
				}
				else
				{
					Debug.LogWarning($"SlideTiles: Invalid gapTile={gapTile}, skipping update");
					return;
				}
			}

			mapManager.UpdateTileMap(newTileMap);
			foreach (int tile in tilesToMove)
			{
				int nextTile = isRollGroup && willWrap ? GetWrappedTile(tilesToMove, tile, stride) : tile + stride;
				GameObject tileObj = FindTileObject(tile);
				if (tileObj != null && nextTile >= 0 && nextTile < mapManager.TileMap.Length)
				{
					tileObj.transform.position = new Vector3(nextTile % mapManager.Width, 0f, nextTile / mapManager.Width);
					// Update tileObj name to reflect new position
					int newX = nextTile % mapManager.Width;
					int newZ = nextTile / mapManager.Width;
					string[] nameParts = tileObj.name.Split('_');
					if (nameParts.Length >= 3)
					{
						tileObj.name = $"{nameParts[0]}_{newX}_{newZ}";
					}
				}
			}
			Debug.Log($"SlideTiles: startTile={startTile} ({startTile % mapManager.Width},{startTile / mapManager.Width}), stride={stride}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}], isRollGroup={isRollGroup}, willWrap={willWrap}");
		}
	}
}