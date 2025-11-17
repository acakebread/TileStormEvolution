// ---------------------------------------------------------------
// TextureFrame.cs – FINAL, CORRECT, NO DUPLICATES, NO EXTRA FIELDS
// ---------------------------------------------------------------
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public struct TextureFrame
	{
		// Runtime-only fields – NEVER serialized
		[JsonIgnore] public Texture2D texture;

		// Serialized fields
		public float fDuration;
		public string name;
		public string szTexture;
	}
}