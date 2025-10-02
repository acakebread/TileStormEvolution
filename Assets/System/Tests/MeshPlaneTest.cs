using UnityEngine;

namespace ClassicTilestorm
{
	public class MeshPlaneTest : MonoBehaviour
	{
		[SerializeField] private bool enableSubdivision = true;
		[SerializeField] private Vector3 divisionAxis = Vector3.up;

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
				Debug.LogError("MeshPlaneTest: No MeshFilter or MeshRenderer found. Disabling.");
				enabled = false;
				return;
			}

			// Log original mesh stats
			if (Debug.isDebugBuild)
			{
				Debug.Log($"Original mesh: {meshFilter.sharedMesh.vertexCount} vertices, {meshFilter.sharedMesh.triangles.Length / 3} triangles");
			}

			ApplySubdivision();
		}

		private void ApplySubdivision()
		{
			// Clean up previous mesh and object
			if (newMesh != null) Destroy(newMesh);
			if (newObject != null && newObject != gameObject) Destroy(newObject);

			if (enableSubdivision)
			{
				// Compute plane point at mesh center
				Bounds bounds = meshFilter.sharedMesh.bounds;
				Vector3 localCenter = bounds.center;
				Vector3 planePoint = transform.TransformPoint(localCenter);
				Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);

				// Get the unified mesh
				newMesh = MeshUtils.SplitMeshAlongPlane(meshFilter.sharedMesh, localAxis, 0f);

				// Create new GameObject for the resulting mesh
				newObject = new GameObject(name + "_Split");
				newObject.transform.SetParent(transform, false);
				var newFilter = newObject.AddComponent<MeshFilter>();
				var newRenderer = newObject.AddComponent<MeshRenderer>();
				newFilter.mesh = newMesh;
				newRenderer.material = meshRenderer.material;

				// Disable original mesh
				meshRenderer.enabled = false;
			}
			else
			{
				// Use the original mesh if subdivision is disabled
				newMesh = Instantiate(meshFilter.sharedMesh);
				newObject = gameObject;
				meshFilter.mesh = newMesh;
				meshRenderer.enabled = true;
			}

			// Log new mesh stats
			if (Debug.isDebugBuild && newMesh != null)
			{
				Debug.Log($"New mesh: {newMesh.vertexCount} vertices, {newMesh.triangles.Length / 3} triangles");
			}
		}

		private void OnDestroy()
		{
			if (newMesh != null) Destroy(newMesh);
			if (newObject != null && newObject != gameObject) Destroy(newObject);
		}
	}
}