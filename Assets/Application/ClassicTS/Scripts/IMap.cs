using UnityEngine;

namespace ClassicTilestorm
{
	public interface IMap
	{
		GameObject gameObject { get; }
		int Width { get; }
		int Height { get; }
		int[] GetTiles();
		bool IsValidTileIndex(int tileIndex);
		int ToIndex(int X, int Z);
		TileProperties GetTileProperties(int tileIndex);
		GameObject GetTileGameObject(int tileIndex);
		GridCoord GetTileCoordinates(int tileIndex);
		DatabaseLoader.Waypoint[] Waypoints { get; }
	}
}