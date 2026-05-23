using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class RenderModelFactory
	{
		public static CommandRenderModelData Create(Definition definition, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default, bool refreshMaterials = true)
		{
			var prefab = ModelAssets.Find(definition?.model);
			if (null == prefab)
			{
				Debug.LogWarning($"RenderModelFactory: Model not found: {definition?.model}");
				return null;
			}

			var replacementMaterial = DefinitionFactory.ResolveMaterialOverride(definition);
			var texture = replacementMaterial != null
				? null
				: AnimMaterialInfoManager.GetFrameZero(MaterialUtils.GetPrimaryTextureName(prefab));
			var matrix = Matrix4x4.TRS(position, rotation, scale == default ? Vector3.one : scale);

			return CollectMeshRenderers(prefab, replacementMaterial, texture, matrix, refreshMaterials);
		}

		private static CommandRenderModelData CollectMeshRenderers(GameObject prefab, Material replacementMaterial, Texture2D texture, Matrix4x4 matrix, bool refreshMaterials)
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
				target.AddMeshInstance(filter.sharedMesh, ReplacementMaterials(mats, replacementMaterial, texture, refreshMaterials), worldMatrix);
			}

			// Process SkinnedMeshRenderers
			foreach (var skinned in skinnedRenderers)
			{
				if (skinned.sharedMesh == null) continue;

				var worldMatrix = matrix * skinned.transform.localToWorldMatrix;
				var mats = skinned.sharedMaterials;
				target.AddMeshInstance(skinned.sharedMesh, ReplacementMaterials(mats, replacementMaterial, texture, refreshMaterials), worldMatrix);
			}

			return target;

			static Material[] ReplacementMaterials(Material[] mats, Material replacementMaterial, Texture2D texture, bool refreshMaterials)
			{
				if (mats == null || mats.Length == 0)
					return System.Array.Empty<Material>();

				var result = new Material[mats.Length];
				for (var m = 0; m < mats.Length; m++)
				{
					var source = mats[m];
					if (source == null) continue;

					var copy = replacementMaterial != null
						? new Material(replacementMaterial)
						: new Material(source);

					copy.mainTextureOffset = source.mainTextureOffset;
					copy.mainTextureScale = source.mainTextureScale;

					if (texture) copy.mainTexture = texture;
					if (refreshMaterials)
						MaterialUtils.ForceMaterialRefresh(copy);
					result[m] = copy;
				}
				return result;
			}
		}
	}
}
