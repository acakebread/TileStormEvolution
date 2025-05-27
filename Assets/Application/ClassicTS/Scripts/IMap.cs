using UnityEngine;

namespace ClassicTilestorm
{
	public interface IMap
	{
		GameObject gameObject { get; }
		int Width { get; }
		int Height { get; }
		int[] GetTiles();

		GameObject GetTileGameObject(int tileIndex);
		TileProperties GetTileProperties(int tileIndex);
	}
}