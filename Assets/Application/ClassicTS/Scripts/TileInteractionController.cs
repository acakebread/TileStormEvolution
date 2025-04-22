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

		public void Start()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnDragStart += OnDragStart;
			gestureSystem.OnDragging += OnDragging;
			gestureSystem.OnDragEnd += OnDragEnd;
		}

		private void OnDestroy()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnDragStart -= OnDragStart;
			gestureSystem.OnDragEnd -= OnDragEnd;
			gestureSystem.OnDragging -= OnDragging;

			// Clear highlights on destroy
			if (tileStrip.TileIndices != null && tileStrip.TileIndices.Count > 0)
				mapManager.HighlightStrip(tileStrip, false);
		}

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			tileStrip = default;
		}

		private void OnDragStart(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
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
			tileStrip = mapManager.GetTileStrip(tileIndex, 0); // No direction yet
			mapManager.HighlightStrip(tileStrip, true);
		}

		private void OnDragging(List<Vector3> gestures)
		{
			if (dragIndex == -1) return;
			foreach (var gesture in gestures)
			{
				mapManager.HighlightStrip(tileStrip, false);

				// restore all the tiles positions to correct grid location before any displacements are calculated
				if (null != tileStrip.TileIndices)
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
					if (true == mapManager.RollStrip(tileStrip))
						dragIndex = mapManager.GetAdjacentTile(dragIndex, dirBit);
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);

					gestureSystem.ConsumeGesture(gesture);
				}
				else
				{
					// Remainder is partial drag position

					var delta = Vector3.zero;

					tileStrip = new MapManager.TileStrip();
					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z))
					{
						if (Mathf.Abs(gesture.x) > 0f)
						{
							tileStrip = mapManager.GetTileStrip(dragIndex, gesture.x > 0f ? TileProperties.East : TileProperties.West);
							if (mapManager.Tiles[tileStrip.Last].Properties.IsDock | mapManager.Tiles[tileStrip.Last].Properties.IsRoll)
								delta = new Vector3(gesture.x, 0, 0);
						}
					}
					else
					{
						if (Mathf.Abs(gesture.z) > 0f)
						{
							tileStrip = mapManager.GetTileStrip(dragIndex, gesture.z > 0f ? TileProperties.North : TileProperties.South);
							if (mapManager.Tiles[tileStrip.Last].Properties.IsDock | mapManager.Tiles[tileStrip.Last].Properties.IsRoll)
								delta = new Vector3(0, 0, gesture.z);
						}
					}

					if (null != tileStrip.TileIndices)
					{
						foreach (var tileIndex in tileStrip.TileIndices)
							mapManager.Tiles[tileIndex].GameObject.transform.position += delta;
					}
				}
				mapManager.HighlightStrip(tileStrip, true);
			}
		}

		private void OnDragEnd()
		{
			if (dragIndex == -1) return;
			mapManager.HighlightStrip(tileStrip, false);

			var currentPos = mapManager.Tiles[dragIndex].GameObject.transform.position;

			// Check bounds for both X and Z directions
			var bounds = mapManager.GetMovementBounds(dragIndex, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			int x = Mathf.RoundToInt(Mathf.Clamp(currentPos.x, bounds.MinWest.X, bounds.MaxEast.X));
			int z = Mathf.RoundToInt(Mathf.Clamp(currentPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z));
			var targetCoord = new GridCoord(x, z);
			int targetIndex = mapManager.ToIndex(targetCoord);

			if (targetIndex != dragIndex)
				mapManager.RollStrip(tileStrip);

			if (null != tileStrip.TileIndices)
			{
				foreach (var tileIndex in tileStrip.TileIndices)
					mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
			}

			dragIndex = -1;
			tileStrip = default;
		}
	}
}