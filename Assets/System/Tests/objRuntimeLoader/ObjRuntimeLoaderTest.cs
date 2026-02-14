using MassiveHadronLtd;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class ObjRuntimeLoaderTest : MonoBehaviour
{
	[Header("OBJ Resource Path (inside Resources folder)")]
	[SerializeField] private string resourcePath = "Models/jun_boundary_tree_double";

	[Header("Optional Material")]
	[SerializeField] private Material material;

	private void Start()
	{
		LoadObj();
	}

	private void LoadObj()
	{
		TextAsset objFile = Resources.Load<TextAsset>(resourcePath);

		if (objFile == null)
		{
			Debug.LogError($"OBJ not found at Resources/{resourcePath}");
			return;
		}

		Mesh mesh = ObjRuntimeLoader.LoadFromText(objFile.text, resourcePath);

		if (mesh == null)
		{
			Debug.LogError("Failed to create mesh from OBJ");
			return;
		}

		// Ensure components exist
		MeshFilter filter = GetComponent<MeshFilter>();
		if (filter == null)
			filter = gameObject.AddComponent<MeshFilter>();

		MeshRenderer renderer = GetComponent<MeshRenderer>();
		if (renderer == null)
			renderer = gameObject.AddComponent<MeshRenderer>();

		filter.mesh = mesh;

		if (material != null)
			renderer.material = material;

		Debug.Log($"OBJ loaded successfully: {mesh.vertexCount} vertices");
	}
}
