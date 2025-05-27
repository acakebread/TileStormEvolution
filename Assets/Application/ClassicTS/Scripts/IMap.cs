namespace ClassicTilestorm
{
	public interface IMap
	{
		int Width { get; }
		int Height { get; }
		int[] Indices { get; }
		//Tile[] Tiles { get; }
		Tile GetTile(int tileIndex);
	}
}