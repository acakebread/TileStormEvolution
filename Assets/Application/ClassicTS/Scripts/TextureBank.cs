// ---------------------------------------------------------------
// TextureBank.cs  –  Modern + Legacy Shim (Perfect)
// ---------------------------------------------------------------
using System;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[Serializable]
	public class TextureBank
	{
		public string id;
		public bool alphaTest = false;
		public TextureFrame[] frames = Array.Empty<TextureFrame>();

		// ← CRITICAL: old code still does ts.name literally everywhere
		[JsonIgnore] public string name => id ?? "";
		[JsonIgnore] public bool bAlphaTest => alphaTest;
	}
}