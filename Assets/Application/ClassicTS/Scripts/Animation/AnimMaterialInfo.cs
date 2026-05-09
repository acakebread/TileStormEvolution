using System;
using UnityEngine;

namespace ClassicTilestorm
{
	[CreateAssetMenu(fileName = "AnimMaterialInfo", menuName = "Classic TileStorm/Anim Material Info")]
	public sealed class AnimMaterialInfo : ScriptableObject
	{
		public string id;
		public bool alphaTest;
		public AnimMaterialFrame[] frames = Array.Empty<AnimMaterialFrame>();

		public TextureSequence ToTextureSequence()
		{
			var sequence = new TextureSequence
			{
				id = string.IsNullOrEmpty(id) ? name : id,
				alphaTest = alphaTest
			};

			var resolvedFrames = new TextureFrame[frames?.Length ?? 0];
			for (var i = 0; i < resolvedFrames.Length; i++)
			{
				var frame = frames[i];
				resolvedFrames[i] = new TextureFrame
				{
					textureName = frame.texture != null ? frame.texture.name : null,
					duration = frame.duration,
					texture = frame.texture
				};
			}

			sequence.SetResolvedFrames(resolvedFrames);
			return sequence;
		}

		public bool Matches(string key)
		{
			if (string.IsNullOrEmpty(key)) return false;

			return string.Equals(id, key, StringComparison.OrdinalIgnoreCase) ||
				   string.Equals(name, key, StringComparison.OrdinalIgnoreCase);
		}
	}

	[Serializable]
	public struct AnimMaterialFrame
	{
		public Texture2D texture;
		public float duration;
	}
}
