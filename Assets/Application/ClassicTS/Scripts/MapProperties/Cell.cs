namespace ClassicTilestorm
{
	// Editor-only fake attachment that represents one map cell
	public class Cell : MapAttachment
	{
		public Cell(int tileIndex)
		{
			type = "Cell"; // or leave as base, doesn't matter
			tile = tileIndex;
		}

		// Optional: give it a nice name in the side panel
		public override string TypeName => "Cell";
	}
}
