using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class TextureFrame
	{
		[JsonProperty("texture")] public string textureName;
		[JsonProperty("duration")] public float duration;

		[JsonIgnore] private Texture2D _texture;
		[JsonIgnore] public Texture2D texture
		{
			get => _texture;
			set => _texture = value;
		}
	}
}