using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class RenderModelFactory
	{
		public static CommandRenderModelData Create(Definition definition, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default)
		{
			var prefab = ModelAssets.Find(definition?.model);
			if (null == prefab)
			{
				Debug.LogWarning($"RenderModelFactory: Model not found: {definition.model}");
				return null;
			}

			//temporary provision to suppress texture replacement on loaded HD models
			var texture = DefinitionFactory.IsHD(prefab) ? null : (AnimMaterialInfoManager.GetFrameZero(definition.texture) ?? TextureSequenceManager.GetFrameZero(definition.texture));
			var material = MaterialAssets.Find(definition.material);
			var matrix = Matrix4x4.TRS(position, rotation, scale == default ? Vector3.one : scale);

			return CollectMeshRenderers(prefab, texture, material, matrix);
		}

		private static CommandRenderModelData CollectMeshRenderers(GameObject prefab, Texture2D texture, Material material, Matrix4x4 matrix)
		{
			// We need the mesh(es) and materials from the prefab
			// This is the tricky part — we have to traverse the prefab hierarchy without instantiating

			// Collect both types
			var meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
			var skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);

			if (meshRenderers.Length == 0 && skinnedRenderers.Length == 0)
			{
				Debug.LogWarning($"No renderers found on prefab: {prefab.name}");
				return null;
			}

			var target = new CommandRenderModelData();
			// Process MeshRenderers
			foreach (var renderer in meshRenderers)
			{
				var filter = renderer.GetComponent<MeshFilter>();
				if (filter == null || filter.sharedMesh == null) continue;

				var worldMatrix = matrix * filter.transform.localToWorldMatrix;
				var mats = renderer.sharedMaterials;
				target.AddMeshInstance(filter.sharedMesh, ReplacementMaterials(mats, texture, material), worldMatrix);
			}

			// Process SkinnedMeshRenderers
			foreach (var skinned in skinnedRenderers)
			{
				if (skinned.sharedMesh == null) continue;

				var worldMatrix = matrix * skinned.transform.localToWorldMatrix;
				var mats = skinned.sharedMaterials;
				target.AddMeshInstance(skinned.sharedMesh, ReplacementMaterials(mats, texture, material), worldMatrix);
			}

			return target;

			static Material[] ReplacementMaterials(Material[] mats, Texture2D texture, Material material)
			{
				if (!MaterialUtils.IsEmissive(material)) material = null;
				var emissive = MaterialUtils.EmissiiveColour(material, Color.white * 1.2f);

				var result = new Material[mats.Length];
				for (var m = 0; m < mats.Length; m++)
				{
					if (mats[m] == null) continue;
					var copy = new Material(mats[m]);
					if (texture) copy.mainTexture = texture;
					if (material) copy.color = emissive;
					result[m] = copy;
				}
				return result;
			}
		}
	}
}
