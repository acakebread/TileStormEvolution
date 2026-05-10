using System;
using Newtonsoft.Json;
using UnityEngine;

namespace ClassicTilestorm
{
	[Serializable]
	public class TextureSequence
	{
		public string id;
		public string name { get => id; }//future replacement for id - just the display name in the editor
		public bool alphaTest = false;

		// Canonical single texture (shorthand)
		public string texture;

		// Only used for real animated sequences
		public TextureFrame[] frames;

		private TextureFrame[] _resolvedFrames;

		[JsonIgnore]
		public TextureFrame[] ResolvedFrames
		{
			get
			{
				if (_resolvedFrames != null) return _resolvedFrames;

				if (!string.IsNullOrEmpty(texture))
				{
					_resolvedFrames = new[] { new TextureFrame { textureName = texture, duration = 0f } };
				}
				else
				{
					_resolvedFrames = frames?.Length > 0 ? frames : Array.Empty<TextureFrame>();
				}
				return _resolvedFrames;
			}
		}

		internal void SetResolvedFrames(TextureFrame[] resolved)
		{
			_resolvedFrames = resolved;
		}

		[JsonIgnore] public bool bAlphaTest => alphaTest;
		[JsonIgnore] public Texture2D FirstTexture => ResolvedFrames.Length > 0 ? ResolvedFrames[0].texture : null;
	}
}
