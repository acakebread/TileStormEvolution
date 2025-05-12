using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class TextureManager
	{
		private static Dictionary<string, Texture2D> textures = new();
		public static Texture2D Get(string name)
		{
			if (true == textures.ContainsKey(name)) return textures[name];
			string texPath = $"{PreviewSettings.TexturePath}{name}".Replace(".tga", "").Replace(".png", "");
			textures[name] = Resources.Load<Texture2D>(texPath);
			if (null == textures[name]) Debug.LogWarning($"Texture not found: {texPath}");
			return textures[name];
		}
	}
}