using UnityEngine;

namespace GamePreviewNamespace
{
	public static class DebugVisualizationHelper
	{
		public class OriginalMaterialHolder : MonoBehaviour { public Material originalMaterial; }

		public static void HighlightStrip(IMap map, in TileStripHelper.TileStrip strip, bool highlight)
		{
			if (!PreviewSettings.ShowTileSelection) return;
			if (strip.Indices == null) return;
			foreach (var tileIndex in strip.Indices)
				HighlightTile(map.GetTileGameObject(tileIndex), highlight);
			if (TileStripHelper.SpareTile != null) HighlightTile(TileStripHelper.SpareTile, highlight);
		}

		private static void HighlightTile(GameObject tile, bool enable)
		{
			if (tile == null) return;
			var meshRenderer = tile.GetComponentInChildren<MeshRenderer>();
			if (meshRenderer == null) return;

			if (enable)
			{
				if (!tile.TryGetComponent<OriginalMaterialHolder>(out var holder))
				{
					holder = tile.AddComponent<OriginalMaterialHolder>();
					holder.originalMaterial = meshRenderer.material;
				}
				meshRenderer.material = new Material(meshRenderer.material) { color = Color.cyan };
			}
			else
			{
				if (tile.TryGetComponent<OriginalMaterialHolder>(out var holder) && holder.originalMaterial != null)
					meshRenderer.material = holder.originalMaterial;
			}
		}

		public static GameObject CreateDebugTile()
		{
			var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
			primitive.transform.localPosition = Vector3.zero;
			primitive.transform.localScale = Vector3.one;
			primitive.name = "debug tile";

			var meshFilter = primitive.GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				Mesh originalMesh = meshFilter.sharedMesh;
				Mesh newMesh = Object.Instantiate(originalMesh);
				Vector3[] vertices = newMesh.vertices;
				for (int i = 0; i < vertices.Length; i++)
				{
					vertices[i].y *= 0.05f;
					vertices[i].y -= 0.05f;
				}
				newMesh.vertices = vertices;
				newMesh.RecalculateBounds();
				newMesh.RecalculateNormals();
				meshFilter.mesh = newMesh;
			}

			var meshRenderer = primitive.GetComponent<MeshRenderer>();
			if (meshRenderer != null) meshRenderer.material = new Material(meshRenderer.material) { color = new Color(0.2f, 0.3f, 0.15f, 1f) };
			return primitive;
		}
	}
}