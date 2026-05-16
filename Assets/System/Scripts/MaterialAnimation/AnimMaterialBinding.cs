using System.Collections.Generic;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public sealed class AnimMaterialBinding : MonoBehaviour, IMaterialSource
	{
		private readonly List<AnimMaterialInstance> _materials = new();
		public IEnumerable<AnimMaterialInstance> GetMaterials() => _materials;

		IEnumerable<Material> IMaterialSource.GetMaterials()
		{
			for (var i = 0; i < _materials.Count; i++)
			{
				var material = _materials[i]?.Material;
				if (material != null)
					yield return material;
			}
		}

		internal void Add(AnimMaterialInstance material)
		{
			if (material != null)
				_materials.Add(material);
		}

		internal void Clear()
		{
			for (var i = 0; i < _materials.Count; i++)
				AnimMaterialManager.Release(_materials[i]);

			_materials.Clear();
		}

		private void OnDestroy()
		{
			Clear();
		}
	}
}
