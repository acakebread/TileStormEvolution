using System;
using System.Collections.Generic;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public sealed class AnimMaterialManager : MonoBehaviour
	{
		private static AnimMaterialManager _instance;
		private readonly Dictionary<Key, Entry> _materials = new();
		private readonly List<AnimMaterialInstance> _animatedMaterials = new();

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

		public static AnimMaterialInstance Acquire(AnimMaterial definition, Material sourceMaterial, Material replacementMaterial = null)
		{
			if (sourceMaterial == null && replacementMaterial == null) return null;
			if (definition == null && replacementMaterial == null) return null;

			return Instance.AcquireInternal(definition, sourceMaterial, replacementMaterial);
		}

		public static void Release(AnimMaterialInstance material)
		{
			if (_instance == null || material == null) return;
			_instance.ReleaseInternal(material);
		}

		public static bool Apply(GameObject gameObject, AnimMaterial definition, Material replacementMaterial = null)
		{
			if (gameObject == null) return false;
			if (definition == null && replacementMaterial == null) return false;

			var binding = gameObject.GetComponent<AnimMaterialBinding>();
			if (binding == null)
				binding = gameObject.AddComponent<AnimMaterialBinding>();
			binding.Clear();

			var applied = false;
			var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			for (var i = 0; i < meshRenderers.Length; i++)
			{
				applied |= Apply(meshRenderers[i], definition, replacementMaterial, binding);
			}

			var skinnedRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			for (var i = 0; i < skinnedRenderers.Length; i++)
			{
				applied |= Apply(skinnedRenderers[i], definition, replacementMaterial, binding);
			}

			if (!applied)
				DestroyBinding(binding);

			return applied;
		}

		public static bool IsEmissive(AnimMaterial definition, Material sourceMaterial, Material replacementMaterial = null)
			=> MaterialUtils.IsEmissive(replacementMaterial != null ? replacementMaterial : sourceMaterial);

		public static void Clear()
		{
			if (_instance == null) return;
			_instance.ClearInternal();
		}

		private static bool Apply(Renderer renderer, AnimMaterial definition, Material replacementMaterial, AnimMaterialBinding binding)
		{
			if (renderer == null || binding == null) return false;

			var sourceMaterials = renderer.sharedMaterials;
			if (sourceMaterials == null || sourceMaterials.Length == 0) return false;

			var animatedMaterials = new Material[sourceMaterials.Length];
			var applied = false;

			for (var i = 0; i < sourceMaterials.Length; i++)
			{
				var animMaterial = Acquire(definition, sourceMaterials[i], replacementMaterial);
				animatedMaterials[i] = animMaterial?.Material ?? sourceMaterials[i];
				if (animMaterial == null) continue;

				binding.Add(animMaterial);
				applied = true;
			}

			if (applied)
				renderer.sharedMaterials = animatedMaterials;

			return applied;
		}

		private AnimMaterialInstance AcquireInternal(AnimMaterial definition, Material sourceMaterial, Material replacementMaterial)
		{
			var key = new Key(definition, replacementMaterial);

			if (_materials.TryGetValue(key, out var entry))
			{
				entry.ReferenceCount++;
				return entry.Material;
			}

			var material = new AnimMaterialInstance(definition, sourceMaterial, replacementMaterial);
			_materials.Add(key, new Entry(key, material));

			if (material.IsAnimated)
				_animatedMaterials.Add(material);

			return material;
		}

		private void ReleaseInternal(AnimMaterialInstance material)
		{
			foreach (var pair in _materials)
			{
				var entry = pair.Value;
				if (entry.Material != material) continue;

				entry.ReferenceCount--;
				if (entry.ReferenceCount > 0) return;

				_materials.Remove(pair.Key);
				_animatedMaterials.Remove(material);
				entry.Material.Destroy();
				return;
			}
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
			foreach (var entry in _materials.Values)
				entry.Material.Destroy();

			_materials.Clear();
			_animatedMaterials.Clear();
		}

		private static void DestroyBinding(AnimMaterialBinding binding)
		{
			if (binding == null) return;

			if (Application.isPlaying)
				Destroy(binding);
			else
				DestroyImmediate(binding);
		}

		private sealed class Entry
		{
			public readonly Key Key;
			public readonly AnimMaterialInstance Material;
			public int ReferenceCount;

			public Entry(Key key, AnimMaterialInstance material)
			{
				Key = key;
				Material = material;
				ReferenceCount = 1;
			}
		}

		private readonly struct Key : IEquatable<Key>
		{
			private readonly string _definitionId;
			private readonly EntityId _replacementMaterialId;

			public Key(AnimMaterial definition, Material replacementMaterial)
			{
				_definitionId = definition?.id ?? string.Empty;
				_replacementMaterialId = replacementMaterial != null
					? replacementMaterial.GetEntityId()
					: default;
			}

			public bool Equals(Key other)
			{
				return string.Equals(_definitionId, other._definitionId) &&
					   _replacementMaterialId.Equals(other._replacementMaterialId);
			}

			public override bool Equals(object obj)
			{
				return obj is Key other && Equals(other);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = 17;
					hash = hash * 23 + (_definitionId?.GetHashCode() ?? 0);
					hash = hash * 23 + _replacementMaterialId.GetHashCode();
					return hash;
				}
			}
		}
	}
}
