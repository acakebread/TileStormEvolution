using UnityEngine;
using GameDatabase;
using System.Collections.Generic;

namespace GamePreviewNamespace
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