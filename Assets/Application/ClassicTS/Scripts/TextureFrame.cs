// TextureFrame.cs
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class TextureFrame  // ← class, not struct — this is correct!
	{
		[JsonIgnore] public Texture2D runtimeTexture;

		[JsonProperty("texture")] public string textureName;
		[JsonProperty("duration")] public float duration;

		// Legacy shims — old code still works
		[JsonIgnore]
		public Texture2D texture
		{
			get => runtimeTexture;
			set => runtimeTexture = value;
		}

		[JsonIgnore] public string szTexture => textureName ?? "";
		[JsonIgnore] public float fDuration => duration;

		// Modern clean accessors
		[JsonIgnore] public string TextureName => textureName ?? "";
		[JsonIgnore] public float Duration => duration;
	}
}