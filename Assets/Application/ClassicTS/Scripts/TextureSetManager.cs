using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class TextureSetManager
	{
		private static readonly Dictionary<string, TextureSequence> _cache = new();

		public static TextureSequence GetTextureSequence(string id, string path)
		{
			//var _id = System.IO.Path.GetFileNameWithoutExtension(path);

			if (string.IsNullOrEmpty(id)) return null;

			if (_cache.TryGetValue(id, out var cached))
				return cached;

			var sequence = ResourceManager.TextureSets.FirstOrDefault(ts => ts.id == id);
			if (sequence == null) return null;

			// Work on a copy so we can mutate safely
			var frames = sequence.ResolvedFrames;
			var framesArray = frames.ToArray(); // copy for mutation
			bool modified = false;

			for (int i = 0; i < framesArray.Length; i++)
			{
				var frame = framesArray[i];
				if (frame.texture == null && !string.IsNullOrEmpty(frame.textureName))
				{
					var texturePath = $"{path}{frame.textureName}";
					frame.texture = TextureCache.Get(texturePath);
					framesArray[i] = frame;
					modified = true;
				}
			}

			if (modified)
			{
				sequence.SetResolvedFrames(framesArray); // write back loaded textures
			}

			_cache[id] = sequence;
			return sequence;
		}

		public static void ClearCache() => _cache.Clear();
	}
}