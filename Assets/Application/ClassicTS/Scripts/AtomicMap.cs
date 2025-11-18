// AtomicMap.cs — Self-contained map + dependencies
using System;

namespace ClassicTilestorm
{
	[Serializable]
	public class AtomicMap
	{
		public Map map;
		public Definition[] definitions;
		public TextureSequence[] textures;
		public string version = "2.0";
		public string author = "Player";
		public string exportedFrom = "ClassicTilestorm";
	}
}