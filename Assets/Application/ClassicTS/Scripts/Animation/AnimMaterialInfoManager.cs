using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class AnimMaterialInfoManager
	{
		private static readonly Dictionary<string, AnimMaterialInfo> Cache = new();
		private static AnimMaterialInfo[] _infos;

		public static TextureSequence GetTextureSequence(string id)
		{
			var info = Get(id);
			return info != null ? info.ToTextureSequence() : null;
		}

		public static Texture2D GetFrameZero(string id)
		{
			var info = Get(id);
			if (info == null || info.frames == null || info.frames.Length == 0)
				return null;

			return info.frames[0].texture;
		}

		public static AnimMaterialInfo Get(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;

			if (Cache.TryGetValue(id, out var cached))
				return cached;

			_infos ??= Resources.LoadAll<AnimMaterialInfo>(string.Empty);
			var info = _infos.FirstOrDefault(candidate => candidate != null && candidate.Matches(id));
			Cache[id] = info;
			return info;
		}

		public static void ClearCache()
		{
			Cache.Clear();
			_infos = null;
		}
	}
}
