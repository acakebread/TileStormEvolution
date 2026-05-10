using System;
using Newtonsoft.Json;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	[Serializable]
	public class TextureFrame
	{
		[JsonProperty("texture")] public string textureName;
		[JsonProperty("duration")] public float duration;

		[JsonIgnore] private Texture2D _texture;
		[JsonIgnore]
		public Texture2D texture
		{
			get => _texture;
			set => _texture = value;
		}
	}

	[Serializable]
	public class AnimMaterial
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

		[JsonIgnore] public Texture2D FirstTexture => ResolvedFrames.Length > 0 ? ResolvedFrames[0].texture : null;
	}

	public sealed class AnimMaterialInstance
	{
		private readonly TextureFrame[] _frames;
		private readonly bool _animateEmissionMap;
		private int _currentFrame;
		private float _timer;

		public Material Material { get; }
		public bool IsEmissive { get; }
		public bool IsAnimated => _frames != null && _frames.Length > 1;

		public event Action<Texture2D> OnTextureChanged;

		internal AnimMaterialInstance(AnimMaterial definition, Material sourceMaterial, Material replacementMaterial)
		{
			_frames = definition?.ResolvedFrames ?? Array.Empty<TextureFrame>();
			Material = new Material(replacementMaterial != null ? replacementMaterial : sourceMaterial)
			{
				name = BuildName(definition, sourceMaterial, replacementMaterial),
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

		private static string BuildName(AnimMaterial definition, Material sourceMaterial, Material replacementMaterial)
		{
			var sourceName = replacementMaterial != null ? replacementMaterial.name : sourceMaterial != null ? sourceMaterial.name : "Material";
			return definition != null ? $"{sourceName} [{definition.id} AnimMaterial]" : $"{sourceName} [AnimMaterial]";
		}
	}
}
