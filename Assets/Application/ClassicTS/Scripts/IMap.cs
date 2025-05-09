using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public interface IMap
	{
		int Width { get; }
		int Height { get; }
		IReadOnlyList<DatabaseLoader.Waypoint> Waypoints { get; }
		TileProperties GetTileProperties(int tileIndex);
		GameObject GetTileGameObject(int tileIndex);
		GridCoord GetTileCoordinates(int tileIndex);
		int ToIndex(GridCoord coord);
		bool IsValidTileIndex(int tileIndex);
		int[] GetTiles();
		GameObject GetMapRoot();
	}
}