// ---------------------------------------------------------------
// TextureSet.cs – Clean and simple
// ---------------------------------------------------------------
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class TextureSet
	{
		public string name;
		public bool bAlphaTest;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public TextureFrame[] frames;
	}
}