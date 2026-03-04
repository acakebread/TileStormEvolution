namespace ClassicTilestorm
{
	// Editor-only fake attachment that represents one map cell
	public class Cell : ISelectable
	{
		public string type;
		public string name;
		public int tile = -1;

		public Cell(int tileIndex)
		{
			type = "Cell"; // or leave as base, doesn't matter
			tile = tileIndex;
		}

		// Optional: give it a nice name in the side panel
		//public string TypeName => "Cell";
	}
}
