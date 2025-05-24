using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public interface IMap
	{
		GameObject gameObject { get; }
		int Width { get; }
		int Height { get; }
		int[] GetTiles();
		bool IsValidTileIndex(int tileIndex);
		int ToIndex(GridCoord coord);
		TileProperties GetTileProperties(int tileIndex);
		GameObject GetTileGameObject(int tileIndex);
		GridCoord GetTileCoordinates(int tileIndex);
		IReadOnlyList<DatabaseLoader.Waypoint> Waypoints { get; }
	}
}