using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class RenderModelFactory
	{
		public static CommandRenderModelData Create(Definition definition, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default)
		{
			var data = new CommandRenderModelData();

			if (definition == null || string.IsNullOrEmpty(definition.model))
				return data;

			scale = scale == default ? Vector3.one : scale;

			// Get the base model (same logic as DefinitionFactory)
			var modelPrefab = ModelAssets.Find(definition.model);
			if (modelPrefab == null)
			{
				Debug.LogWarning($"RenderModelFactory: Model not found: {definition.model}");
				return data;
			}

			// We need the mesh(es) and materials from the prefab
			// This is the tricky part — we have to traverse the prefab hierarchy without instantiating
			CollectMeshRenderers(modelPrefab, definition, position, rotation, scale, data);

			return data;
		}

		private static void CollectMeshRenderers(GameObject prefabRoot, Definition def,
			Vector3 rootPos, Quaternion rootRot, Vector3 rootScale, CommandRenderModelData target)
		{
			var rootMatrix = Matrix4x4.TRS(rootPos, rootRot, rootScale);

			// Collect both types
			var meshRenderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);
			var skinnedRenderers = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

			if (meshRenderers.Length == 0 && skinnedRenderers.Length == 0)
			{
				Debug.LogWarning($"No renderers found on prefab: {prefabRoot.name}");
				return;
			}

			//temporary provision to suppress texture replacement on loaded HD models
			var isHD = (meshRenderers.Length == 1 && meshRenderers[0].sharedMaterials.Length >= 2) || meshRenderers.Length >= 2;
			var firstTex = isHD ? null : TextureSequenceManager.GetFrameZero(def.texture);

			var replacement = string.IsNullOrEmpty(def.material) ? null : MaterialAssets.Find(def.material);

			// Process MeshRenderers
			foreach (var renderer in meshRenderers)
			{
				var filter = renderer.GetComponent<MeshFilter>();
				if (filter == null || filter.sharedMesh == null) continue;

				var worldMatrix = rootMatrix * filter.transform.localToWorldMatrix;
				var mats = renderer.sharedMaterials;
				target.AddMeshInstance(filter.sharedMesh, ReplacementMaterials(mats, firstTex, replacement), worldMatrix);
			}

			// Process SkinnedMeshRenderers
			foreach (var skinned in skinnedRenderers)
			{
				if (skinned.sharedMesh == null) continue;

				var worldMatrix = rootMatrix * skinned.transform.localToWorldMatrix;
				var mats = skinned.sharedMaterials;
				target.AddMeshInstance(skinned.sharedMesh, ReplacementMaterials(mats, firstTex, replacement), worldMatrix);
			}

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