using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	public abstract class MorphGeomBase : MonoBehaviour
	{
		protected MeshFilter meshFilter;
		protected Vector3[] originalVertices;
		protected Vector3[] modifiedVertices;
		protected Bounds influenceVolume;
		protected Vector3 anchorPlaneNormal = Vector3.up; // Default: bottom plane (normal points up, in local space)
		protected float anchorPlaneOffset; // Distance from origin along normal to the anchor plane (in local space)

		[SerializeField] protected bool useCustomInfluenceVolume = false;
		[SerializeField] protected Vector3 customInfluenceVolumeCenter = Vector3.zero;
		[SerializeField] protected Vector3 customInfluenceVolumeSize = Vector3.one;
		[SerializeField] protected float boundsEpsilon = 0.01f; // Configurable epsilon for boundary inclusion
		[SerializeField] protected bool subdivideLongGeometry = false; // Toggle for subdividing long geometry
		[SerializeField] protected float maxSegmentLength = 0.5f; // Max length of edge segments (used for influence volume)

		protected virtual void Awake()
		{
			Initialize();
		}

		protected void Initialize()
		{
			// Try to find MeshRenderer first, then get its MeshFilter
			var meshRenderer = GetComponentInChildren<MeshRenderer>(true);
			if (meshRenderer != null)
			{
				meshFilter = meshRenderer.GetComponent<MeshFilter>();
				if (meshFilter != null)
				{
					Debug.Log($"{GetType().Name}: Found MeshFilter via MeshRenderer on {meshRenderer.gameObject.name}");
				}
			}
			else
			{
				// Fallback to direct MeshFilter search
				meshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>(true);
				if (meshFilter != null)
				{
					Debug.Log($"{GetType().Name}: Found MeshFilter on {meshFilter.gameObject.name}");
				}
			}

			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				Debug.LogWarning($"{GetType().Name}: No MeshFilter or mesh found on {gameObject.name} or its children. Disabling component.");
				enabled = false;
				return;
			}

			// Subdivide mesh if enabled
			Mesh workingMesh = meshFilter.sharedMesh;
			if (subdivideLongGeometry)
			{
				workingMesh = SubdivideLongGeometry(workingMesh);
				meshFilter.mesh = workingMesh; // Update meshFilter to use subdivided mesh
			}

			// Cache original vertices
			originalVertices = workingMesh.vertices;
			modifiedVertices = new Vector3[originalVertices.Length];

			// Initialize influence volume
			UpdateInfluenceVolume();

			// Debug vertex inclusion
			if (Debug.isDebugBuild)
			{
				DebugVertexInclusion();
			}
		}

		// Public method to configure subdivision and reinitialize
		public void ConfigureSubdivision(bool enable, float maxSegmentLength)
		{
			subdivideLongGeometry = enable;
			this.maxSegmentLength = Mathf.Max(0.01f, maxSegmentLength);
			Reinitialize();
		}

		// Public method to reinitialize after changing properties
		public void Reinitialize()
		{
			Initialize();
		}

		private Mesh SubdivideLongGeometry(Mesh inputMesh)
		{
			Vector3 planeNormal = transform.InverseTransformDirection(anchorPlaneNormal.normalized);
			Vector3 meshCenter = inputMesh.bounds.center;
			float offset = anchorPlaneOffset;
			Plane minPlane = new Plane(planeNormal, -offset); // Unity Plane uses -distance
			int originalVertexCount = inputMesh.vertices.Length;

			// Use MeshStratifier with numStrata = 3
			Mesh newMesh = MeshStratifier.StratifyMesh(inputMesh, minPlane, numStrata: 3);

			if (Debug.isDebugBuild)
			{
				Debug.Log($"{GetType().Name}: Stratified mesh from {originalVertexCount} to {newMesh.vertexCount} vertices, {newMesh.triangles.Length / 3} triangles");
			}

			return newMesh;
		}

		private void UpdateInfluenceVolume()
		{
			if (!useCustomInfluenceVolume)
			{
				influenceVolume = CalculateWorldSpaceBounds();
			}
			else
			{
				Vector3 size = Vector3.Max(customInfluenceVolumeSize, Vector3.one * 0.01f);
				influenceVolume = new Bounds(transform.TransformPoint(customInfluenceVolumeCenter), size);
			}

			if (!useCustomInfluenceVolume)
			{
				anchorPlaneOffset = meshFilter.sharedMesh.bounds.min.y;
			}

			Debug.Log($"{GetType().Name}: Influence volume - Center: {influenceVolume.center}, Size: {influenceVolume.size}");
		}

		private Bounds CalculateWorldSpaceBounds()
		{
			Bounds localBounds = meshFilter.sharedMesh.bounds;
			Vector3[] vertices = meshFilter.sharedMesh.vertices;
			Bounds worldBounds = new Bounds();

			if (vertices.Length > 0)
			{
				Vector3 firstVertex = transform.TransformPoint(vertices[0]);
				worldBounds = new Bounds(firstVertex, Vector3.zero);
				for (int i = 1; i < vertices.Length; i++)
				{
					worldBounds.Encapsulate(transform.TransformPoint(vertices[i]));
				}
			}
			else
			{
				worldBounds = new Bounds(transform.TransformPoint(localBounds.center), Vector3.Scale(localBounds.size, transform.lossyScale));
			}

			return worldBounds;
		}

		public void SetCustomInfluenceVolume(Vector3 planeNormal, float offset)
		{
			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				Debug.LogWarning($"{GetType().Name}: Cannot set custom influence volume; no valid MeshFilter found.");
				return;
			}

			planeNormal = planeNormal.normalized;
			useCustomInfluenceVolume = true;

			float planeOffset = offset;
			Vector3[] vertices = meshFilter.sharedMesh.vertices;
			float maxProj = float.MinValue;

			foreach (var vertex in vertices)
			{
				float proj = Vector3.Dot(vertex, planeNormal) - planeOffset;
				maxProj = Mathf.Max(maxProj, proj);
			}

			if (maxProj <= 0)
			{
				Debug.LogWarning($"{GetType().Name}: No vertices found on the positive side of the specified plane. Using default bounds.");
				useCustomInfluenceVolume = false;
				UpdateInfluenceVolume();
				return;
			}

			float extent = maxProj;
			Vector3 center = planeNormal * (planeOffset + extent * 0.5f);
			Vector3 size = CalculateWorldSpaceBounds().size;
			size = Vector3.Max(size, Vector3.one * 0.01f);
			size = new Vector3(size.x, extent, size.z);

			customInfluenceVolumeCenter = center;
			customInfluenceVolumeSize = size;
			UpdateInfluenceVolume();

			anchorPlaneNormal = planeNormal;
			anchorPlaneOffset = planeOffset;
		}

		protected virtual void Update()
		{
			if (!enabled || meshFilter == null) return;

			System.Array.Copy(originalVertices, modifiedVertices, originalVertices.Length);
			ApplyMorphEffect();
			meshFilter.mesh.vertices = modifiedVertices;
			meshFilter.mesh.RecalculateNormals();
		}

		protected abstract void ApplyMorphEffect();

		protected float GetDistanceToAnchorPlane(Vector3 vertexWorldPos)
		{
			Vector3 vertexLocalPos = transform.InverseTransformPoint(vertexWorldPos);
			return Vector3.Dot(vertexLocalPos, anchorPlaneNormal) - anchorPlaneOffset;
		}

		protected bool IsVertexInInfluenceVolume(Vector3 vertexWorldPos)
		{
			Vector3 min = influenceVolume.min - Vector3.one * boundsEpsilon;
			Vector3 max = influenceVolume.max + Vector3.one * boundsEpsilon;
			return vertexWorldPos.x >= min.x && vertexWorldPos.x <= max.x &&
				   vertexWorldPos.y >= min.y && vertexWorldPos.y <= max.y &&
				   vertexWorldPos.z >= min.z && vertexWorldPos.z <= max.z;
		}

		protected virtual void OnValidate()
		{
			if (meshFilter != null)
			{
				UpdateInfluenceVolume();
			}
			boundsEpsilon = Mathf.Max(0.0001f, boundsEpsilon);
			maxSegmentLength = Mathf.Max(0.01f, maxSegmentLength);
		}

		private void DebugVertexInclusion()
		{
			int includedVertices = 0;
			foreach (var vertex in meshFilter.sharedMesh.vertices)
			{
				Vector3 worldPos = transform.TransformPoint(vertex);
				if (IsVertexInInfluenceVolume(worldPos))
				{
					includedVertices++;
				}
				else
				{
					Debug.LogWarning($"{GetType().Name}: Vertex at world pos {worldPos} excluded from influence volume {influenceVolume.center}, {influenceVolume.size}");
				}
			}
			Debug.Log($"{GetType().Name}: {includedVertices}/{meshFilter.sharedMesh.vertices.Length} vertices included in influence volume");
		}

		private void OnDrawGizmosSelected()
		{
			if (influenceVolume.size != Vector3.zero && Debug.isDebugBuild)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireCube(influenceVolume.center, influenceVolume.size);

				Gizmos.color = Color.blue;
				Vector3 planeCenter = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);
				Vector3 planeSize = new Vector3(influenceVolume.size.x, 0.01f, influenceVolume.size.z);
				Quaternion planeRotation = Quaternion.LookRotation(anchorPlaneNormal);
				Gizmos.matrix = Matrix4x4.TRS(planeCenter, planeRotation, Vector3.one);
				Gizmos.DrawWireCube(Vector3.zero, planeSize);
				Gizmos.matrix = Matrix4x4.identity;

				// Draw strata planes
				if (subdivideLongGeometry && meshFilter != null && meshFilter.sharedMesh != null)
				{
					Vector3 planeNormal = transform.InverseTransformDirection(anchorPlaneNormal.normalized);
					float offset = anchorPlaneOffset;
					Vector3[] vertices = meshFilter.sharedMesh.vertices;
					float maxDist = float.MinValue;
					foreach (Vector3 vertex in vertices)
					{
						float dist = Vector3.Dot(vertex, planeNormal);
						maxDist = Mathf.Max(maxDist, dist);
					}
					float maxOffset = maxDist;
					float step = (maxOffset - offset) / (3 + 1); // numStrata = 3
					float size = Mathf.Max(influenceVolume.size.x, influenceVolume.size.z);

					for (int i = 1; i <= 3; i++)
					{
						float strataOffset = offset + step * i;
						Vector3 strataPoint = transform.TransformPoint(anchorPlaneNormal * strataOffset);
						Vector3 right = Vector3.Cross(anchorPlaneNormal, Vector3.up).normalized;
						if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(anchorPlaneNormal, Vector3.forward).normalized;
						Vector3 up = Vector3.Cross(right, anchorPlaneNormal).normalized;
						Vector3 p1 = strataPoint + (right + up) * size;
						Vector3 p2 = strataPoint + (right - up) * size;
						Vector3 p3 = strataPoint + (-right - up) * size;
						Vector3 p4 = strataPoint + (-right + up) * size;
						Gizmos.color = Color.yellow;
						Gizmos.DrawLine(p1, p2);
						Gizmos.DrawLine(p2, p3);
						Gizmos.DrawLine(p3, p4);
						Gizmos.DrawLine(p4, p1);
					}
				}

				if (meshFilter != null && meshFilter.sharedMesh != null)
				{
					foreach (var vertex in meshFilter.sharedMesh.vertices)
					{
						Vector3 worldPos = transform.TransformPoint(vertex);
						Gizmos.color = IsVertexInInfluenceVolume(worldPos) ? Color.green : Color.red;
						Gizmos.DrawSphere(worldPos, 0.05f);
					}
				}
			}
		}

		public class Vector3EqualityComparer : IEqualityComparer<Vector3>
		{
			public static readonly Vector3EqualityComparer Instance = new Vector3EqualityComparer();
			private const float Epsilon = 0.0001f;

			public bool Equals(Vector3 a, Vector3 b)
			{
				return Mathf.Abs(a.x - b.x) < Epsilon &&
					   Mathf.Abs(a.y - b.y) < Epsilon &&
					   Mathf.Abs(a.z - b.z) < Epsilon;
			}

			public int GetHashCode(Vector3 obj)
			{
				return ((int)Mathf.Round(obj.x / Epsilon) * 397) ^
					   ((int)Mathf.Round(obj.y / Epsilon) * 397) ^
					   (int)Mathf.Round(obj.z / Epsilon);
			}
		}
	}
}


//using System.Collections.Generic;
//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public abstract class MorphGeomBase : MonoBehaviour
//	{
//		protected MeshFilter meshFilter;
//		protected Vector3[] originalVertices;
//		protected Vector3[] modifiedVertices;
//		protected Bounds influenceVolume;
//		protected Vector3 anchorPlaneNormal = Vector3.up; // Default: bottom plane (normal points up, in local space)
//		protected float anchorPlaneOffset; // Distance from origin along normal to the anchor plane (in local space)

//		[SerializeField] protected bool useCustomInfluenceVolume = false;
//		[SerializeField] protected Vector3 customInfluenceVolumeCenter = Vector3.zero;
//		[SerializeField] protected Vector3 customInfluenceVolumeSize = Vector3.one;
//		[SerializeField] protected float boundsEpsilon = 0.01f; // Configurable epsilon for boundary inclusion
//		[SerializeField] protected bool subdivideLongGeometry = false; // Toggle for subdividing long geometry
//		[SerializeField] protected float maxSegmentLength = 0.5f; // Max length of edge segments before subdivision

//		protected virtual void Awake()
//		{
//			Initialize();
//		}

//		protected void Initialize()
//		{
//			// Try to find MeshRenderer first, then get its MeshFilter
//			var meshRenderer = GetComponentInChildren<MeshRenderer>(true);
//			if (meshRenderer != null)
//			{
//				meshFilter = meshRenderer.GetComponent<MeshFilter>();
//				if (meshFilter != null)
//				{
//					Debug.Log($"{GetType().Name}: Found MeshFilter via MeshRenderer on {meshRenderer.gameObject.name}");
//				}
//			}
//			else
//			{
//				// Fallback to direct MeshFilter search
//				meshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>(true);
//				if (meshFilter != null)
//				{
//					Debug.Log($"{GetType().Name}: Found MeshFilter on {meshFilter.gameObject.name}");
//				}
//			}

//			if (meshFilter == null || meshFilter.sharedMesh == null)
//			{
//				Debug.LogWarning($"{GetType().Name}: No MeshFilter or mesh found on {gameObject.name} or its children. Disabling component.");
//				enabled = false;
//				return;
//			}

//			// Subdivide mesh if enabled
//			Mesh workingMesh = meshFilter.sharedMesh;
//			if (subdivideLongGeometry)
//			{
//				workingMesh = SubdivideLongGeometry(workingMesh);
//				meshFilter.mesh = workingMesh; // Update meshFilter to use subdivided mesh
//			}

//			// Cache original vertices
//			originalVertices = workingMesh.vertices;
//			modifiedVertices = new Vector3[originalVertices.Length];

//			// Initialize influence volume
//			UpdateInfluenceVolume();

//			// Debug vertex inclusion
//			if (Debug.isDebugBuild)
//			{
//				DebugVertexInclusion();
//			}
//		}

//		// Public method to configure subdivision and reinitialize
//		public void ConfigureSubdivision(bool enable, float maxSegmentLength)
//		{
//			subdivideLongGeometry = enable;
//			this.maxSegmentLength = Mathf.Max(0.01f, maxSegmentLength);
//			Reinitialize();
//		}

//		// Public method to reinitialize after changing properties
//		public void Reinitialize()
//		{
//			Initialize();
//		}

//		private Mesh SubdivideLongGeometry(Mesh inputMesh)
//		{
//			Vector3 divisionAxis = transform.TransformDirection(anchorPlaneNormal);
//			Vector3 meshCenter = transform.TransformPoint(inputMesh.bounds.center);
//			int originalVertexCount = inputMesh.vertices.Length;

//			Mesh newMesh = MeshUtils.SubdivideMeshLongEdges(inputMesh, divisionAxis, maxSegmentLength, meshCenter);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"{GetType().Name}: Subdivided mesh from {originalVertexCount} to {newMesh.vertexCount} vertices");
//			}

//			return newMesh;
//		}

//		private void UpdateInfluenceVolume()
//		{
//			if (!useCustomInfluenceVolume)
//			{
//				influenceVolume = CalculateWorldSpaceBounds();
//			}
//			else
//			{
//				Vector3 size = Vector3.Max(customInfluenceVolumeSize, Vector3.one * 0.01f);
//				influenceVolume = new Bounds(transform.TransformPoint(customInfluenceVolumeCenter), size);
//			}

//			if (!useCustomInfluenceVolume)
//			{
//				anchorPlaneOffset = meshFilter.sharedMesh.bounds.min.y;
//			}

//			Debug.Log($"{GetType().Name}: Influence volume - Center: {influenceVolume.center}, Size: {influenceVolume.size}");
//		}

//		private Bounds CalculateWorldSpaceBounds()
//		{
//			Bounds localBounds = meshFilter.sharedMesh.bounds;
//			Vector3[] vertices = meshFilter.sharedMesh.vertices;
//			Bounds worldBounds = new Bounds();

//			if (vertices.Length > 0)
//			{
//				Vector3 firstVertex = transform.TransformPoint(vertices[0]);
//				worldBounds = new Bounds(firstVertex, Vector3.zero);
//				for (int i = 1; i < vertices.Length; i++)
//				{
//					worldBounds.Encapsulate(transform.TransformPoint(vertices[i]));
//				}
//			}
//			else
//			{
//				worldBounds = new Bounds(transform.TransformPoint(localBounds.center), Vector3.Scale(localBounds.size, transform.lossyScale));
//			}

//			return worldBounds;
//		}

//		public void SetCustomInfluenceVolume(Vector3 planeNormal, float offset)
//		{
//			if (meshFilter == null || meshFilter.sharedMesh == null)
//			{
//				Debug.LogWarning($"{GetType().Name}: Cannot set custom influence volume; no valid MeshFilter found.");
//				return;
//			}

//			planeNormal = planeNormal.normalized;
//			useCustomInfluenceVolume = true;

//			float planeOffset = offset;
//			Vector3[] vertices = meshFilter.sharedMesh.vertices;
//			float maxProj = float.MinValue;

//			foreach (var vertex in vertices)
//			{
//				float proj = Vector3.Dot(vertex, planeNormal) - planeOffset;
//				maxProj = Mathf.Max(maxProj, proj);
//			}

//			if (maxProj <= 0)
//			{
//				Debug.LogWarning($"{GetType().Name}: No vertices found on the positive side of the specified plane. Using default bounds.");
//				useCustomInfluenceVolume = false;
//				UpdateInfluenceVolume();
//				return;
//			}

//			float extent = maxProj;
//			Vector3 center = planeNormal * (planeOffset + extent * 0.5f);
//			Vector3 size = CalculateWorldSpaceBounds().size;
//			size = Vector3.Max(size, Vector3.one * 0.01f);
//			size = new Vector3(size.x, extent, size.z);

//			customInfluenceVolumeCenter = center;
//			customInfluenceVolumeSize = size;
//			UpdateInfluenceVolume();

//			anchorPlaneNormal = planeNormal;
//			anchorPlaneOffset = planeOffset;
//		}

//		protected virtual void Update()
//		{
//			if (!enabled || meshFilter == null) return;

//			System.Array.Copy(originalVertices, modifiedVertices, originalVertices.Length);
//			ApplyMorphEffect();
//			meshFilter.mesh.vertices = modifiedVertices;
//			meshFilter.mesh.RecalculateNormals();
//		}

//		protected abstract void ApplyMorphEffect();

//		protected float GetDistanceToAnchorPlane(Vector3 vertexWorldPos)
//		{
//			Vector3 vertexLocalPos = transform.InverseTransformPoint(vertexWorldPos);
//			return Vector3.Dot(vertexLocalPos, anchorPlaneNormal) - anchorPlaneOffset;
//		}

//		protected bool IsVertexInInfluenceVolume(Vector3 vertexWorldPos)
//		{
//			Vector3 min = influenceVolume.min - Vector3.one * boundsEpsilon;
//			Vector3 max = influenceVolume.max + Vector3.one * boundsEpsilon;
//			return vertexWorldPos.x >= min.x && vertexWorldPos.x <= max.x &&
//				   vertexWorldPos.y >= min.y && vertexWorldPos.y <= max.y &&
//				   vertexWorldPos.z >= min.z && vertexWorldPos.z <= max.z;
//		}

//		protected virtual void OnValidate()
//		{
//			if (meshFilter != null)
//			{
//				UpdateInfluenceVolume();
//			}
//			boundsEpsilon = Mathf.Max(0.0001f, boundsEpsilon);
//			maxSegmentLength = Mathf.Max(0.01f, maxSegmentLength);
//		}

//		private void DebugVertexInclusion()
//		{
//			int includedVertices = 0;
//			foreach (var vertex in meshFilter.sharedMesh.vertices)
//			{
//				Vector3 worldPos = transform.TransformPoint(vertex);
//				if (IsVertexInInfluenceVolume(worldPos))
//				{
//					includedVertices++;
//				}
//				else
//				{
//					Debug.LogWarning($"{GetType().Name}: Vertex at world pos {worldPos} excluded from influence volume {influenceVolume.center}, {influenceVolume.size}");
//				}
//			}
//			Debug.Log($"{GetType().Name}: {includedVertices}/{meshFilter.sharedMesh.vertices.Length} vertices included in influence volume");
//		}

//		private void OnDrawGizmosSelected()
//		{
//			if (influenceVolume.size != Vector3.zero && Debug.isDebugBuild)
//			{
//				Gizmos.color = Color.yellow;
//				Gizmos.DrawWireCube(influenceVolume.center, influenceVolume.size);

//				Gizmos.color = Color.blue;
//				Vector3 planeCenter = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);
//				Vector3 planeSize = new Vector3(influenceVolume.size.x, 0.01f, influenceVolume.size.z);
//				Quaternion planeRotation = Quaternion.LookRotation(anchorPlaneNormal);
//				Gizmos.matrix = Matrix4x4.TRS(planeCenter, planeRotation, Vector3.one);
//				Gizmos.DrawWireCube(Vector3.zero, planeSize);
//				Gizmos.matrix = Matrix4x4.identity;

//				if (meshFilter != null && meshFilter.sharedMesh != null)
//				{
//					foreach (var vertex in meshFilter.sharedMesh.vertices)
//					{
//						Vector3 worldPos = transform.TransformPoint(vertex);
//						Gizmos.color = IsVertexInInfluenceVolume(worldPos) ? Color.green : Color.red;
//						Gizmos.DrawSphere(worldPos, 0.05f);
//					}
//				}
//			}
//		}

//		public class Vector3EqualityComparer : IEqualityComparer<Vector3>
//		{
//			public static readonly Vector3EqualityComparer Instance = new Vector3EqualityComparer();
//			private const float Epsilon = 0.0001f;

//			public bool Equals(Vector3 a, Vector3 b)
//			{
//				return Mathf.Abs(a.x - b.x) < Epsilon &&
//					   Mathf.Abs(a.y - b.y) < Epsilon &&
//					   Mathf.Abs(a.z - b.z) < Epsilon;
//			}

//			public int GetHashCode(Vector3 obj)
//			{
//				return ((int)Mathf.Round(obj.x / Epsilon) * 397) ^
//					   ((int)Mathf.Round(obj.y / Epsilon) * 397) ^
//					   (int)Mathf.Round(obj.z / Epsilon);
//			}
//		}
//	}
//}


//using System.Collections.Generic;
//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public abstract class MorphGeomBase : MonoBehaviour
//	{
//		protected MeshFilter meshFilter;
//		protected Vector3[] originalVertices;
//		protected Vector3[] modifiedVertices;
//		protected Bounds influenceVolume;
//		protected Vector3 anchorPlaneNormal = Vector3.up; // Default: bottom plane (normal points up, in local space)
//		protected float anchorPlaneOffset; // Distance from origin along normal to the anchor plane (in local space)

//		[SerializeField] protected bool useCustomInfluenceVolume = false;
//		[SerializeField] protected Vector3 customInfluenceVolumeCenter = Vector3.zero;
//		[SerializeField] protected Vector3 customInfluenceVolumeSize = Vector3.one;
//		[SerializeField] protected float boundsEpsilon = 0.01f; // Configurable epsilon for boundary inclusion
//		[SerializeField] protected bool subdivideLongGeometry = false; // Toggle for subdividing long geometry
//		[SerializeField] protected float maxSegmentLength = 0.5f; // Max length of edge segments before subdivision

//		protected virtual void Awake()
//		{
//			Initialize();
//		}

//		protected void Initialize()
//		{
//			// Try to find MeshRenderer first, then get its MeshFilter
//			var meshRenderer = GetComponentInChildren<MeshRenderer>(true);
//			if (meshRenderer != null)
//			{
//				meshFilter = meshRenderer.GetComponent<MeshFilter>();
//				if (meshFilter != null)
//				{
//					Debug.Log($"{GetType().Name}: Found MeshFilter via MeshRenderer on {meshRenderer.gameObject.name}");
//				}
//			}
//			else
//			{
//				// Fallback to direct MeshFilter search
//				meshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>(true);
//				if (meshFilter != null)
//				{
//					Debug.Log($"{GetType().Name}: Found MeshFilter on {meshFilter.gameObject.name}");
//				}
//			}

//			if (meshFilter == null || meshFilter.sharedMesh == null)
//			{
//				Debug.LogWarning($"{GetType().Name}: No MeshFilter or mesh found on {gameObject.name} or its children. Disabling component.");
//				enabled = false;
//				return;
//			}

//			// Subdivide mesh if enabled
//			Mesh workingMesh = meshFilter.sharedMesh;
//			if (subdivideLongGeometry)
//			{
//				workingMesh = SubdivideLongGeometry(workingMesh);
//				meshFilter.mesh = workingMesh; // Update meshFilter to use subdivided mesh
//			}

//			// Cache original vertices
//			originalVertices = workingMesh.vertices;
//			modifiedVertices = new Vector3[originalVertices.Length];

//			// Initialize influence volume
//			UpdateInfluenceVolume();

//			// Debug vertex inclusion
//			if (Debug.isDebugBuild)
//			{
//				DebugVertexInclusion();
//			}
//		}

//		// Public method to configure subdivision and reinitialize
//		public void ConfigureSubdivision(bool enable, float maxSegmentLength)
//		{
//			subdivideLongGeometry = enable;
//			this.maxSegmentLength = Mathf.Max(0.01f, maxSegmentLength);
//			Reinitialize();
//		}

//		// Public method to reinitialize after changing properties
//		public void Reinitialize()
//		{
//			Initialize();
//		}

//		private Mesh SubdivideLongGeometry(Mesh inputMesh)
//		{
//			Vector3[] vertices = inputMesh.vertices;
//			int[] triangles = inputMesh.triangles;
//			List<Vector3> newVertices = new List<Vector3>(vertices);
//			List<int> newTriangles = new List<int>();
//			List<Vector2> newUVs = inputMesh.uv.Length > 0 ? new List<Vector2>(inputMesh.uv) : new List<Vector2>();
//			List<Vector3> newNormals = inputMesh.normals.Length > 0 ? new List<Vector3>(inputMesh.normals) : new List<Vector3>();

//			int originalVertexCount = vertices.Length;
//			int edgesSubdivided = 0;

//			// Process each triangle
//			for (int i = 0; i < triangles.Length; i += 3)
//			{
//				int v0 = triangles[i];
//				int v1 = triangles[i + 1];
//				int v2 = triangles[i + 2];

//				Vector3 p0 = vertices[v0];
//				Vector3 p1 = vertices[v1];
//				Vector3 p2 = vertices[v2];

//				// Store new vertex indices for this triangle
//				int[] newV0 = { v0 }, newV1 = { v1 }, newV2 = { v2 };

//				// Check and subdivide each edge
//				Vector3[] edges = { p1 - p0, p2 - p1, p0 - p2 };
//				int[] edgeIndices = { v0, v1, v2 };
//				for (int j = 0; j < 3; j++)
//				{
//					Vector3 edge = edges[j];
//					float edgeLengthAlongNormal = Mathf.Abs(Vector3.Dot(edge, anchorPlaneNormal));
//					if (edgeLengthAlongNormal > maxSegmentLength)
//					{
//						int segments = Mathf.CeilToInt(edgeLengthAlongNormal / maxSegmentLength);
//						Vector3 start = vertices[edgeIndices[j]];
//						Vector3 end = vertices[edgeIndices[(j + 1) % 3]];
//						Vector3 dir = (end - start).normalized;
//						float segmentLength = edgeLengthAlongNormal / segments;

//						// Store new vertices for this edge
//						int[] newEdgeVertices = new int[segments];
//						newEdgeVertices[0] = edgeIndices[j]; // Start vertex
//						for (int k = 1; k < segments; k++)
//						{
//							Vector3 newVertex = start + dir * (segmentLength * k);
//							newVertices.Add(newVertex);

//							// Interpolate UVs and normals
//							if (newUVs.Count > 0)
//							{
//								Vector2 uv0 = inputMesh.uv[edgeIndices[j]];
//								Vector2 uv1 = inputMesh.uv[edgeIndices[(j + 1) % 3]];
//								newUVs.Add(Vector2.Lerp(uv0, uv1, (float)k / segments));
//							}
//							if (newNormals.Count > 0)
//							{
//								Vector3 normal0 = inputMesh.normals[edgeIndices[j]];
//								Vector3 normal1 = inputMesh.normals[edgeIndices[(j + 1) % 3]];
//								newNormals.Add(Vector3.Lerp(normal0, normal1, (float)k / segments).normalized);
//							}
//							newEdgeVertices[k] = newVertices.Count - 1;
//						}
//						edgesSubdivided++;

//						// Update vertex indices for the triangle
//						if (j == 0) newV0 = newEdgeVertices; // Edge 0-1
//						else if (j == 1) newV1 = newEdgeVertices; // Edge 1-2
//						else newV2 = newEdgeVertices; // Edge 2-0
//					}
//				}

//				// Add triangles for subdivided edges
//				if (newV0.Length == 1 && newV1.Length == 1 && newV2.Length == 1)
//				{
//					// No subdivision: use original triangle
//					newTriangles.Add(v0);
//					newTriangles.Add(v1);
//					newTriangles.Add(v2);
//				}
//				else
//				{
//					// Simplified fan triangulation: connect to a common vertex
//					int commonVertex = newV0[0]; // Use first vertex of v0 as pivot
//					for (int k = 0; k < Mathf.Max(newV1.Length, newV2.Length); k++)
//					{
//						int v1Index = k < newV1.Length ? newV1[k] : newV1[newV1.Length - 1];
//						int v2Index = k < newV2.Length ? newV2[k] : newV2[newV2.Length - 1];
//						newTriangles.Add(commonVertex);
//						newTriangles.Add(v1Index);
//						newTriangles.Add(v2Index);
//					}
//				}
//			}

//			// Create new mesh
//			Mesh newMesh = new Mesh
//			{
//				vertices = newVertices.ToArray(),
//				triangles = newTriangles.ToArray(),
//				uv = newUVs.Count > 0 ? newUVs.ToArray() : null,
//				normals = newNormals.Count > 0 ? newNormals.ToArray() : null
//			};
//			newMesh.RecalculateBounds();
//			if (newNormals.Count == 0) newMesh.RecalculateNormals();

//			Debug.Log($"{GetType().Name}: Subdivided mesh from {originalVertexCount} to {newVertices.Count} vertices, {edgesSubdivided} edges subdivided");
//			return newMesh;
//		}

//		private void UpdateInfluenceVolume()
//		{
//			if (!useCustomInfluenceVolume)
//			{
//				influenceVolume = CalculateWorldSpaceBounds();
//			}
//			else
//			{
//				Vector3 size = Vector3.Max(customInfluenceVolumeSize, Vector3.one * 0.01f);
//				influenceVolume = new Bounds(transform.TransformPoint(customInfluenceVolumeCenter), size);
//			}

//			if (!useCustomInfluenceVolume)
//			{
//				anchorPlaneOffset = meshFilter.sharedMesh.bounds.min.y;
//			}

//			Debug.Log($"{GetType().Name}: Influence volume - Center: {influenceVolume.center}, Size: {influenceVolume.size}");
//		}

//		private Bounds CalculateWorldSpaceBounds()
//		{
//			Bounds localBounds = meshFilter.sharedMesh.bounds;
//			Vector3[] vertices = meshFilter.sharedMesh.vertices;
//			Bounds worldBounds = new Bounds();

//			if (vertices.Length > 0)
//			{
//				Vector3 firstVertex = transform.TransformPoint(vertices[0]);
//				worldBounds = new Bounds(firstVertex, Vector3.zero);
//				for (int i = 1; i < vertices.Length; i++)
//				{
//					worldBounds.Encapsulate(transform.TransformPoint(vertices[i]));
//				}
//			}
//			else
//			{
//				worldBounds = new Bounds(transform.TransformPoint(localBounds.center), Vector3.Scale(localBounds.size, transform.lossyScale));
//			}

//			return worldBounds;
//		}

//		public void SetCustomInfluenceVolume(Vector3 planeNormal, float offset)
//		{
//			if (meshFilter == null || meshFilter.sharedMesh == null)
//			{
//				Debug.LogWarning($"{GetType().Name}: Cannot set custom influence volume; no valid MeshFilter found.");
//				return;
//			}

//			planeNormal = planeNormal.normalized;
//			useCustomInfluenceVolume = true;

//			float planeOffset = offset;
//			Vector3[] vertices = meshFilter.sharedMesh.vertices;
//			float maxProj = float.MinValue;

//			foreach (var vertex in vertices)
//			{
//				float proj = Vector3.Dot(vertex, planeNormal) - planeOffset;
//				maxProj = Mathf.Max(maxProj, proj);
//			}

//			if (maxProj <= 0)
//			{
//				Debug.LogWarning($"{GetType().Name}: No vertices found on the positive side of the specified plane. Using default bounds.");
//				useCustomInfluenceVolume = false;
//				UpdateInfluenceVolume();
//				return;
//			}

//			float extent = maxProj;
//			Vector3 center = planeNormal * (planeOffset + extent * 0.5f);
//			Vector3 size = CalculateWorldSpaceBounds().size;
//			size = Vector3.Max(size, Vector3.one * 0.01f);
//			size = new Vector3(size.x, extent, size.z);

//			customInfluenceVolumeCenter = center;
//			customInfluenceVolumeSize = size;
//			UpdateInfluenceVolume();

//			anchorPlaneNormal = planeNormal;
//			anchorPlaneOffset = planeOffset;
//		}

//		protected virtual void Update()
//		{
//			if (!enabled || meshFilter == null) return;

//			System.Array.Copy(originalVertices, modifiedVertices, originalVertices.Length);
//			ApplyMorphEffect();
//			meshFilter.mesh.vertices = modifiedVertices;
//			meshFilter.mesh.RecalculateNormals();
//		}

//		protected abstract void ApplyMorphEffect();

//		protected float GetDistanceToAnchorPlane(Vector3 vertexWorldPos)
//		{
//			Vector3 vertexLocalPos = transform.InverseTransformPoint(vertexWorldPos);
//			return Vector3.Dot(vertexLocalPos, anchorPlaneNormal) - anchorPlaneOffset;
//		}

//		protected bool IsVertexInInfluenceVolume(Vector3 vertexWorldPos)
//		{
//			Vector3 min = influenceVolume.min - Vector3.one * boundsEpsilon;
//			Vector3 max = influenceVolume.max + Vector3.one * boundsEpsilon;
//			return vertexWorldPos.x >= min.x && vertexWorldPos.x <= max.x &&
//				   vertexWorldPos.y >= min.y && vertexWorldPos.y <= max.y &&
//				   vertexWorldPos.z >= min.z && vertexWorldPos.z <= max.z;
//		}

//		protected virtual void OnValidate()
//		{
//			if (meshFilter != null)
//			{
//				UpdateInfluenceVolume();
//			}
//			boundsEpsilon = Mathf.Max(0.0001f, boundsEpsilon);
//			maxSegmentLength = Mathf.Max(0.01f, maxSegmentLength);
//		}

//		private void DebugVertexInclusion()
//		{
//			int includedVertices = 0;
//			foreach (var vertex in meshFilter.sharedMesh.vertices)
//			{
//				Vector3 worldPos = transform.TransformPoint(vertex);
//				if (IsVertexInInfluenceVolume(worldPos))
//				{
//					includedVertices++;
//				}
//				else
//				{
//					Debug.LogWarning($"{GetType().Name}: Vertex at world pos {worldPos} excluded from influence volume {influenceVolume.center}, {influenceVolume.size}");
//				}
//			}
//			Debug.Log($"{GetType().Name}: {includedVertices}/{meshFilter.sharedMesh.vertices.Length} vertices included in influence volume");
//		}

//		private void OnDrawGizmosSelected()
//		{
//			if (influenceVolume.size != Vector3.zero && Debug.isDebugBuild)
//			{
//				Gizmos.color = Color.yellow;
//				Gizmos.DrawWireCube(influenceVolume.center, influenceVolume.size);

//				Gizmos.color = Color.blue;
//				Vector3 planeCenter = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);
//				Vector3 planeSize = new Vector3(influenceVolume.size.x, 0.01f, influenceVolume.size.z);
//				Quaternion planeRotation = Quaternion.LookRotation(anchorPlaneNormal);
//				Gizmos.matrix = Matrix4x4.TRS(planeCenter, planeRotation, Vector3.one);
//				Gizmos.DrawWireCube(Vector3.zero, planeSize);
//				Gizmos.matrix = Matrix4x4.identity;

//				if (meshFilter != null && meshFilter.sharedMesh != null)
//				{
//					foreach (var vertex in meshFilter.sharedMesh.vertices)
//					{
//						Vector3 worldPos = transform.TransformPoint(vertex);
//						Gizmos.color = IsVertexInInfluenceVolume(worldPos) ? Color.green : Color.red;
//						Gizmos.DrawSphere(worldPos, 0.05f);
//					}
//				}
//			}
//		}

//		public class Vector3EqualityComparer : IEqualityComparer<Vector3>
//		{
//			public static readonly Vector3EqualityComparer Instance = new Vector3EqualityComparer();
//			private const float Epsilon = 0.0001f;

//			public bool Equals(Vector3 a, Vector3 b)
//			{
//				return Mathf.Abs(a.x - b.x) < Epsilon &&
//					   Mathf.Abs(a.y - b.y) < Epsilon &&
//					   Mathf.Abs(a.z - b.z) < Epsilon;
//			}

//			public int GetHashCode(Vector3 obj)
//			{
//				return ((int)Mathf.Round(obj.x / Epsilon) * 397) ^
//					   ((int)Mathf.Round(obj.y / Epsilon) * 397) ^
//					   (int)Mathf.Round(obj.z / Epsilon);
//			}
//		}
//	}
//}