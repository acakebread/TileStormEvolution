using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class MorphGeomBase : MonoBehaviour
	{
		protected MeshFilter meshFilter;
		protected Vector3[] originalVertices;
		protected Vector3[] modifiedVertices;
		protected Vector3[] originalWorldVertices; // Added for caching world-space vertices
		protected Dictionary<Vector3, List<int>> cachedVertexGroups; // Added for caching vertex groups
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

			// Cache world-space vertices and vertex groups
			originalWorldVertices = new Vector3[originalVertices.Length];
			cachedVertexGroups = new Dictionary<Vector3, List<int>>(Vector3EqualityComparer.Instance);
			for (int i = 0; i < originalVertices.Length; i++)
			{
				originalWorldVertices[i] = transform.TransformPoint(originalVertices[i]);
				Vector3 worldPos = originalWorldVertices[i];
				if (!cachedVertexGroups.ContainsKey(worldPos))
					cachedVertexGroups[worldPos] = new List<int>();
				cachedVertexGroups[worldPos].Add(i);
			}

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
			int originalTrianglesCount = inputMesh.triangles.Length ;

			// Use MeshStratifier with numStrata = 3
			Mesh newMesh = MeshStratifier.StratifyMesh(inputMesh, minPlane, numStrata: 3);

			if (Debug.isDebugBuild)
			{
				Debug.Log($"{GetType().Name}: Stratified mesh from {originalVertexCount} to {newMesh.vertexCount} vertices, and {originalTrianglesCount / 3} to {newMesh.triangles.Length / 3} triangles");
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

			// Expand bounds to account for epsilon
			influenceVolume.Expand(boundsEpsilon * 2f);

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
			return influenceVolume.Contains(vertexWorldPos); // Use Bounds.Contains for efficiency
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
				//else
				//{
				//	Debug.LogWarning($"{GetType().Name}: Vertex at world pos {worldPos} excluded from influence volume {influenceVolume.center}, {influenceVolume.size}");
				//}
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