using UnityEngine;
using System.Collections.Generic;
using System;

namespace MassiveHadronLtd
{
	public readonly struct MeshInstanceInfo
	{
		public readonly Mesh mesh;
		public readonly Material[] materials;
		public readonly Matrix4x4 localToWorld;
		public readonly int subMeshCount;
		public readonly int layer;

		public MeshInstanceInfo(Mesh mesh, Material[] materials, Matrix4x4 localToWorld, int layer = 0)
		{
			this.mesh = mesh;
			this.materials = materials;
			this.localToWorld = localToWorld;
			this.subMeshCount = mesh != null ? mesh.subMeshCount : 0;
			this.layer = layer;
		}
	}

	/// <summary>
	/// Data container for CommandRenderCamera. Holds one or more mesh instances and bounds.
	/// </summary>
	public class CommandRenderModelData
	{
		public readonly List<MeshInstanceInfo> meshInstances = new();
		public Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

		public CommandRenderModelData() { }

		// Convenience constructor for a single mesh instance
		public CommandRenderModelData(Mesh mesh, Material[] materials, Matrix4x4 localToWorld, int layer = 0)
		{
			AddMeshInstance(mesh, materials, localToWorld, layer);
		}

		public void Clear()
		{
			meshInstances.Clear();
			bounds = new Bounds(Vector3.zero, Vector3.zero);
		}

		public void AddMeshInstance(Mesh mesh, Material[] materials, Matrix4x4 localToWorld, int layer = 0)
		{
			meshInstances.Add(new MeshInstanceInfo(mesh, materials, localToWorld, layer));
			bounds.Encapsulate(BoundsTransformed(mesh.bounds, localToWorld));
		}

		private static Bounds BoundsTransformed(Bounds localBounds, Matrix4x4 matrix)
		{
			var corners = new Vector3[8];
			int i = 0;

			for (int x = -1; x <= 1; x += 2)
				for (int y = -1; y <= 1; y += 2)
					for (int z = -1; z <= 1; z += 2)
					{
						Vector3 offset = Vector3.Scale(new Vector3(x, y, z), localBounds.extents);
						corners[i++] = matrix.MultiplyPoint(localBounds.center + offset);
					}

			var bounds = new Bounds(corners[0], Vector3.zero);
			for (int j = 1; j < 8; j++)
				bounds.Encapsulate(corners[j]);

			return bounds;
		}

		/// <summary>
		/// Creates a CommandRenderModelData from a prefab (e.g. arrow) without instantiating it in the scene.
		/// Position/rotation/scale default to identity (you can adjust later if needed).
		/// </summary>
		public static CommandRenderModelData Instantiate(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default, Color? tint = null)
		{
			if (prefab == null) return null;
			if (scale == default) scale = Vector3.one;

			var matrix = Matrix4x4.TRS(position, rotation, scale);

			var meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
			var skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);

			if (meshRenderers.Length == 0 && skinnedRenderers.Length == 0)
			{
				Debug.LogWarning($"No renderers found on prefab: {prefab.name}");
				return null;
			}

			var target = new CommandRenderModelData();

			foreach (var renderer in meshRenderers)
			{
				var filter = renderer.GetComponent<MeshFilter>();
				if (filter?.sharedMesh == null) continue;

				var worldMatrix = matrix * filter.transform.localToWorldMatrix;
				var mats = renderer.sharedMaterials;

				target.AddMeshInstance(filter.sharedMesh, CreateTintedMaterials(mats, tint), worldMatrix);
			}

			foreach (var skinned in skinnedRenderers)
			{
				if (skinned.sharedMesh == null) continue;

				var worldMatrix = matrix * skinned.transform.localToWorldMatrix;
				var mats = skinned.sharedMaterials;

				target.AddMeshInstance(skinned.sharedMesh, CreateTintedMaterials(mats, tint), worldMatrix);
			}

			return target;

			static Material[] CreateTintedMaterials(Material[] originalMats, Color? tintColor)
			{
				if (originalMats == null) return Array.Empty<Material>();

				var result = new Material[originalMats.Length];

				// Use the exact property name that works for your arrow shader
				const string baseColorProperty = "_BASE_COLOR";   // This matched what worked in EditorDirectionUtil
				int colorID = Shader.PropertyToID(baseColorProperty);

				for (int i = 0; i < originalMats.Length; i++)
				{
					if (originalMats[i] == null)
					{
						result[i] = null;
						continue;
					}

					var copy = new Material(originalMats[i]);

					if (tintColor.HasValue)
					{
						// Use SetColor with the correct property ID instead of .color
						copy.SetColor(colorID, tintColor.Value);
					}

					result[i] = copy;
				}
				return result;
			}
		}
	}
}
