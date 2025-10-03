using UnityEngine;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class MeshPlaneTest : MonoBehaviour
	{
		[SerializeField] private bool enableSubdivision = true;
		[SerializeField] private Vector3 divisionAxis = Vector3.up;
		[SerializeField] private float offset = 0f;

		private MeshFilter meshFilter;
		private MeshRenderer meshRenderer;
		private Mesh newMesh;
		private GameObject newObject;

		private void Awake()
		{
			meshFilter = GetComponent<MeshFilter>();
			meshRenderer = GetComponent<MeshRenderer>();

			if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
			{
				Debug.LogError("MeshPlaneTest: Missing MeshFilter or MeshRenderer. Disabling.");
				enabled = false;
				return;
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Original mesh: {meshFilter.sharedMesh.vertexCount} vertices, {meshFilter.sharedMesh.triangles.Length / 3} triangles");
			}

			ApplySubdivision();
		}

		private void ApplySubdivision()
		{
			// Clean up previous objects
			if (newMesh != null) Destroy(newMesh);
			if (newObject != null && newObject != gameObject) Destroy(newObject);

			if (!enableSubdivision)
			{
				newMesh = Instantiate(meshFilter.sharedMesh);
				meshFilter.mesh = newMesh;
				meshRenderer.enabled = true;
				newObject = gameObject;
				return;
			}

			// Compute local plane axis
			Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);

			// Split the mesh along the plane
			newMesh = MeshUtilTest.SplitMeshAlongPlane(meshFilter.sharedMesh, localAxis, offset);

			// Create new GameObject for split mesh
			newObject = new GameObject(name + "_Split");
			newObject.transform.SetParent(transform, false);
			var newFilter = newObject.AddComponent<MeshFilter>();
			var newRenderer = newObject.AddComponent<MeshRenderer>();
			newFilter.mesh = newMesh;
			newRenderer.material = meshRenderer.material;

			// Disable original mesh
			meshRenderer.enabled = false;

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Split mesh: {newMesh.vertexCount} vertices, {newMesh.triangles.Length / 3} triangles");
			}
		}

		private void OnDestroy()
		{
			if (newMesh != null) Destroy(newMesh);
			if (newObject != null && newObject != gameObject) Destroy(newObject);
		}
	}
}
