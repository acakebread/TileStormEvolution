using ClassicTilestorm;
using ClassicTilestorm.Assets;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class RenderModelFactory
	{
		public static RenderModelData Create(Definition definition, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default)
		{
			var data = new RenderModelData();

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
			Vector3 rootPos, Quaternion rootRot, Vector3 rootScale, RenderModelData target)
		{
			Matrix4x4 rootMatrix = Matrix4x4.TRS(rootPos, rootRot, rootScale);

			// Get all MeshFilters + MeshRenderers in prefab (without instantiating)
			var filters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
			var renderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);

			if (filters.Length == 0) return;

			// Very naive version - assumes mesh filter & renderer indices match
			for (int i = 0; i < Mathf.Min(filters.Length, renderers.Length); i++)
			{
				var filter = filters[i];
				var renderer = renderers[i];

				if (filter.sharedMesh == null) continue;

				var localToRoot = filter.transform.localToWorldMatrix;
				var worldMatrix = rootMatrix * localToRoot;

				var mats = renderer.sharedMaterials;

				// Apply definition material override if present
				if (!string.IsNullOrEmpty(def.material))
				{
					var overrideMat = MaterialAssets.Find(def.material);
					if (overrideMat != null)
					{
						mats = new Material[mats.Length];
						for (int j = 0; j < mats.Length; j++)
							mats[j] = overrideMat;
					}
				}

				target.AddMeshInstance(filter.sharedMesh, mats, worldMatrix);
			}

			// Todo: handle texture animation sequence if needed (later)
			// Todo: handle child prefabs / nested hierarchies
		}
	}
}