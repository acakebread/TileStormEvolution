// ---------------------------------------------------------------
// TextureFrame.cs — ONLY THE ONE LINE FIXED
// ---------------------------------------------------------------
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public struct TextureFrame
	{
		// Runtime-only — the actual loaded Texture2D
		[JsonIgnore] public Texture2D runtimeTexture;

		// Modern JSON fields — these are serialized
		[JsonProperty("texture")] public string textureName;
		[JsonProperty("duration")] public float duration;

		// LEGACY COMPATIBILITY SHIMS — old code still works
		[JsonIgnore]
		public Texture2D texture
		{
			get => runtimeTexture;
			set => runtimeTexture = value;
		}

		[JsonIgnore] public string szTexture => textureName ?? "";
		[JsonIgnore] public float fDuration => duration;

		// Optional: clean modern accessors (for new code)
		[JsonIgnore] public string TextureName => textureName ?? "";
		[JsonIgnore] public float Duration => duration;
	}
}