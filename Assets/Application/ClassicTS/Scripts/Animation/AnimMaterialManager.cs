using System.Collections.Generic;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public sealed class AnimMaterialManager : MonoBehaviour
	{
		private static AnimMaterialManager _instance;
		private readonly Dictionary<Key, AnimMaterial> _materials = new();
		private readonly List<AnimMaterial> _animatedMaterials = new();

		private static AnimMaterialManager Instance
		{
			get
			{
				if (_instance != null) return _instance;

				var gameObject = new GameObject(nameof(AnimMaterialManager))
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				DontDestroyOnLoad(gameObject);
				_instance = gameObject.AddComponent<AnimMaterialManager>();
				return _instance;
			}
		}

		public static AnimMaterial GetOrCreate(TextureSequence sequence, Material sourceMaterial, Material replacementMaterial = null)
		{
			if (sourceMaterial == null && replacementMaterial == null) return null;
			if (sequence == null && replacementMaterial == null) return null;

			return Instance.GetOrCreateInternal(sequence, sourceMaterial, replacementMaterial);
		}

		public static void Apply(Renderer renderer, TextureSequence sequence, Material replacementMaterial = null)
		{
			if (renderer == null) return;
			if (sequence == null && replacementMaterial == null) return;

			var sourceMaterials = renderer.sharedMaterials;
			if (sourceMaterials == null || sourceMaterials.Length == 0) return;

			var animatedMaterials = new Material[sourceMaterials.Length];
			for (var i = 0; i < sourceMaterials.Length; i++)
			{
				var animMaterial = GetOrCreate(sequence, sourceMaterials[i], replacementMaterial);
				animatedMaterials[i] = animMaterial?.Material ?? sourceMaterials[i];
			}

			renderer.sharedMaterials = animatedMaterials;
		}

		public static bool Apply(GameObject gameObject, TextureSequence sequence, Material replacementMaterial = null)
		{
			if (gameObject == null) return false;
			if (sequence == null && replacementMaterial == null) return false;

			var applied = false;
			var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			for (var i = 0; i < meshRenderers.Length; i++)
			{
				Apply(meshRenderers[i], sequence, replacementMaterial);
				applied = true;
			}

			var skinnedRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			for (var i = 0; i < skinnedRenderers.Length; i++)
			{
				Apply(skinnedRenderers[i], sequence, replacementMaterial);
				applied = true;
			}

			return applied;
		}

		public static bool IsEmissive(TextureSequence sequence, Material sourceMaterial, Material replacementMaterial = null)
			=> GetOrCreate(sequence, sourceMaterial, replacementMaterial)?.IsEmissive ?? MaterialUtils.IsEmissive(replacementMaterial);

		public static void Clear()
		{
			if (_instance == null) return;
			_instance.ClearInternal();
		}

		private AnimMaterial GetOrCreateInternal(TextureSequence sequence, Material sourceMaterial, Material replacementMaterial)
		{
			var key = new Key(sequence, sourceMaterial, replacementMaterial);
			if (_materials.TryGetValue(key, out var material))
				return material;

			material = new AnimMaterial(sequence, sourceMaterial, replacementMaterial);
			_materials.Add(key, material);
			if (material.IsAnimated)
				_animatedMaterials.Add(material);

			return material;
		}

		private void Update()
		{
			var deltaTime = Time.deltaTime;
			for (var i = 0; i < _animatedMaterials.Count; i++)
				_animatedMaterials[i].Update(deltaTime);
		}

		private void OnDestroy()
		{
			if (_instance == this)
				_instance = null;

			ClearInternal();
		}

		private void ClearInternal()
		{
			foreach (var material in _materials.Values)
				material.Destroy();

			_materials.Clear();
			_animatedMaterials.Clear();
		}

		private readonly struct Key
		{
			private readonly string _sequenceId;
			private readonly EntityId _sourceMaterialId;
			private readonly EntityId _replacementMaterialId;
			private readonly Vector2 _mainTextureOffset;
			private readonly Vector2 _mainTextureScale;

			public Key(TextureSequence sequence, Material sourceMaterial, Material replacementMaterial)
			{
				_sequenceId = sequence?.id ?? string.Empty;
				_sourceMaterialId = sourceMaterial != null ? sourceMaterial.GetEntityId() : default;
				_replacementMaterialId = replacementMaterial != null ? replacementMaterial.GetEntityId() : default;
				_mainTextureOffset = sourceMaterial != null ? sourceMaterial.mainTextureOffset : Vector2.zero;
				_mainTextureScale = sourceMaterial != null ? sourceMaterial.mainTextureScale : Vector2.one;
			}
		}
	}
}
