using UnityEngine;

namespace ClassicTilestorm
{
	public interface IMap
	{
		int Width { get; }
		int Height { get; }
		int[] GetTiles();

		GameObject GetTileGameObject(int tileIndex);
		TileProperties GetTileProperties(int tileIndex);
	}
}