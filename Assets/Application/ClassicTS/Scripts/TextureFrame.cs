using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class TextureFrame
	{
		[JsonIgnore] public Texture2D runtimeTexture;

		[JsonProperty("texture")] public string textureName;
		[JsonProperty("duration")] public float duration;

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