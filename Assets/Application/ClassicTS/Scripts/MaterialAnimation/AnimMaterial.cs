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
		public string name { get => id; }
		public bool alphaTest = false;

		public string texture;
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

		internal void SetResolvedFrames(TextureFrame[] resolved) => _resolvedFrames = resolved;

		[JsonIgnore] public Texture2D FirstTexture => ResolvedFrames.Length > 0 ? ResolvedFrames[0].texture : null;
	}

	public sealed class AnimMaterialInstance
	{
		private readonly TextureFrame[] _frames;
		private readonly bool _animateEmissionMap;
		private int _currentFrame;
		private float _timer;

		public Material Material { get; }
		public bool IsEmissive { get; private set; }
		public bool IsAnimated => _frames != null && _frames.Length > 1;

		public event Action<Texture2D> OnTextureChanged;

		internal AnimMaterialInstance(AnimMaterial definition, Material sourceMaterial, Material replacementMaterial)
		{
			_frames = definition?.ResolvedFrames ?? Array.Empty<TextureFrame>();

			// Create material
			Material baseMat = replacementMaterial != null ? replacementMaterial : sourceMaterial;
			Material = new Material(baseMat)
			{
				name = BuildName(definition, sourceMaterial, replacementMaterial),
				hideFlags = HideFlags.DontSave
			};

			if (sourceMaterial != null)
			{
				Material.mainTextureOffset = sourceMaterial.mainTextureOffset;
				Material.mainTextureScale = sourceMaterial.mainTextureScale;
			}

			// === IMPROVED EMISSIVE DETECTION ===
			DetectAndSetupEmissive();

			_animateEmissionMap = IsEmissive;
			if (IsEmissive)
				Material.EnableKeyword("_EMISSION");

			ApplyFrame(0);
		}

		private void DetectAndSetupEmissive()
		{
			if (Material == null)
			{
				IsEmissive = false;
				return;
			}

			// Check 1: Already has strong emission color
			Color emissionColor = Material.GetColor("_EmissionColor");
			bool hasEmissionColor = emissionColor.maxColorComponent > 0.01f;

			// Check 2: Emission keyword or map
			bool hasEmissionKeyword = Material.IsKeywordEnabled("_EMISSION");
			bool hasEmissionMap = Material.GetTexture("_EmissionMap") != null;

			IsEmissive = hasEmissionColor || hasEmissionKeyword || hasEmissionMap;

			// If we found emission but intensity is low, boost it (important for OBJ imported materials)
			if (IsEmissive && emissionColor.maxColorComponent < 1.0f)
			{
				Material.SetColor("_EmissionColor", emissionColor * 1.5f); // Boost if needed
			}

			// Make sure keyword is enabled
			if (IsEmissive)
				Material.EnableKeyword("_EMISSION");
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
			if (Material == null || _frames == null || index < 0 || index >= _frames.Length)
				return;

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
			var sourceName = replacementMaterial != null ? replacementMaterial.name
						 : sourceMaterial != null ? sourceMaterial.name
						 : "Material";
			return definition != null ? $"{sourceName} [{definition.id} AnimMaterial]" : $"{sourceName} [AnimMaterial]";
		}
	}
}