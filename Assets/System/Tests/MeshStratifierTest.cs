//using UnityEngine;

//public class MeshStratifierTest : MonoBehaviour
//{
//	void Start()
//	{
//		Mesh originalMesh = new Mesh();
//		Vector3[] vertices = new Vector3[]
//		{
//			new Vector3(-1, -1, 0),   // v0: below plane
//            new Vector3(-1, 1, 0),    // v1: above plane
//            new Vector3(1, 1, 0),     // v2: above plane
//            new Vector3(1, -1, 0),    // v3: below plane
//            new Vector3(0, 1, 0),     // v4: above plane
//            new Vector3(0, -1, 0)     // v5: below plane
//        };
//		int[] triangles = new int[] { 0, 1, 5, 1, 4, 5, 4, 2, 5, 2, 3, 5 }; // Counterclockwise
//		Vector3[] normals = new Vector3[]
//		{
//			Vector3.forward,
//			Vector3.forward,
//			Vector3.forward,
//			Vector3.forward,
//			Vector3.forward,
//			Vector3.forward
//		};
//		Vector2[] uvs = new Vector2[]
//		{
//			new Vector2(0, 0),
//			new Vector2(0, 1),
//			new Vector2(1, 1),
//			new Vector2(1, 0),
//			new Vector2(0.5f, 1),
//			new Vector2(0.5f, 0)
//		};

//		originalMesh.vertices = vertices;
//		originalMesh.triangles = triangles;
//		originalMesh.normals = normals;
//		originalMesh.uv = uvs;

//		Vector3 planeNormal = Vector3.up;
//		float offset = -1f;
//		Plane minPlane = new Plane(planeNormal, -offset);
//		int numStrata = 3;

//		Mesh stratifiedMesh = MeshStratifier.StratifyMesh(originalMesh, minPlane, numStrata);
//		GetComponent<MeshFilter>().mesh = stratifiedMesh;
//	}
//}


//using UnityEngine;

//public class MeshStratifierTest : MonoBehaviour
//{
//	void Start()
//	{
//		// Create a mesh with a single triangle
//		Mesh originalMesh = new Mesh();
//		Vector3[] vertices = new Vector3[]
//		{
//			new Vector3(0, -1, 0),  // v0: below plane
//            new Vector3(-1, 1, 0),   // v1: above plane
//            new Vector3(1, 1, 0)   // v2: above plane
//        };
//		int[] triangles = new int[] { 0, 1, 2 }; // Counterclockwise
//		Vector3[] normals = new Vector3[]
//		{
//			Vector3.forward, // Assuming facing +Z for simplicity
//            Vector3.forward,
//			Vector3.forward
//		};
//		Vector2[] uvs = new Vector2[]
//		{
//			new Vector2(0, 0),
//			new Vector2(1, 0),
//			new Vector2(0, 1)
//		};

//		originalMesh.vertices = vertices;
//		originalMesh.triangles = triangles;
//		originalMesh.normals = normals;
//		originalMesh.uv = uvs;

//		// Set up the plane and strata
//		Vector3 planeNormal = Vector3.up; // Slice along Y-axis
//		float offset = -1.0f; // Plane at Y=-1.0
//		Plane minPlane = new Plane(planeNormal, -offset); // distance = -offset = 0
//		int numStrata = 2; // Single stratum

//		// Apply stratification
//		Mesh stratifiedMesh = MeshStratifier.StratifyMesh(originalMesh, minPlane, numStrata);
//		GetComponent<MeshFilter>().mesh = stratifiedMesh;
//	}
//}


using UnityEngine;

public class MeshStratifierTest : MonoBehaviour
{
	public int numStrata = 3; // default 3 layers
	void Start()
	{
		Mesh originalMesh = GetComponent<MeshFilter>().mesh;
		Vector3 planeNormal = Vector3.up; // Slice along Y-axis
		float offset = -0.5f; // Plane at Y=-0.5
		Plane minPlane = new Plane(planeNormal, -offset); // distance = -offset to place plane at y = offset
		Mesh stratifiedMesh = MeshStratifier.StratifyMesh(originalMesh, minPlane, numStrata);
		GetComponent<MeshFilter>().mesh = stratifiedMesh;
	}
}
