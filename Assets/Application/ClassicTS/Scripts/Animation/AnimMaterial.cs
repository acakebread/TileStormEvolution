using System;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public sealed class AnimMaterial
	{
		private readonly TextureFrame[] _frames;
		private readonly bool _animateEmissionMap;
		private int _currentFrame;
		private float _timer;

		public Material Material { get; }
		public bool IsEmissive { get; }
		public bool IsAnimated => _frames != null && _frames.Length > 1;

		public event Action<Texture2D> OnTextureChanged;

		internal AnimMaterial(TextureSequence sequence, Material sourceMaterial, Material replacementMaterial)
		{
			_frames = sequence?.ResolvedFrames ?? Array.Empty<TextureFrame>();
			Material = new Material(replacementMaterial != null ? replacementMaterial : sourceMaterial)
			{
				name = BuildName(sequence, sourceMaterial, replacementMaterial),
				hideFlags = HideFlags.DontSave
			};

			if (sourceMaterial != null)
			{
				Material.mainTextureOffset = sourceMaterial.mainTextureOffset;
				Material.mainTextureScale = sourceMaterial.mainTextureScale;
			}

			IsEmissive = MaterialUtils.IsEmissive(Material);
			_animateEmissionMap = replacementMaterial != null && IsEmissive;
			if (IsEmissive)
				Material.EnableKeyword("_EMISSION");

			ApplyFrame(0);
		}

		public void Update(float deltaTime)
		{
			if (!IsAnimated) return;

			_timer += deltaTime;
			var duration = Mathf.Max(0.0001f, _frames[_currentFrame].duration);
			if (_timer < duration) return;

			_timer %= duration;
			_currentFrame = (_currentFrame + 1) % _frames.Length;
			ApplyFrame(_currentFrame);
		}

		public void ApplyFrame(int index)
		{
			if (Material == null || _frames == null || index < 0 || index >= _frames.Length) return;

			var texture = _frames[index].texture;
			if (texture == null) return;

			Material.mainTexture = texture;
			if (_animateEmissionMap)
				Material.SetTexture("_EmissionMap", texture);

			OnTextureChanged?.Invoke(texture);
		}

		internal void Destroy()
		{
			if (Application.isPlaying)
				UnityEngine.Object.Destroy(Material);
			else
				UnityEngine.Object.DestroyImmediate(Material);
		}

		private static string BuildName(TextureSequence sequence, Material sourceMaterial, Material replacementMaterial)
		{
			var sourceName = replacementMaterial != null ? replacementMaterial.name : sourceMaterial != null ? sourceMaterial.name : "Material";
			return sequence != null ? $"{sourceName} [{sequence.id} AnimMaterial]" : $"{sourceName} [AnimMaterial]";
		}
	}
}
