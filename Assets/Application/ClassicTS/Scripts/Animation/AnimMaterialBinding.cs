using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	public sealed class AnimMaterialBinding : MonoBehaviour
	{
		private readonly List<AnimMaterial> _materials = new();

		internal void Add(AnimMaterial material)
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
