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

		// This is the only duration field that exists when saving
		public float fDuration;

		// Runtime helper – only exists in memory, never saved
		[JsonIgnore]
		public float Duration => fDuration > 0f ? fDuration : 1f;

		// Serialized fields
		public string name;
		public string szTexture;
	}
}