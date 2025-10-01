using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public abstract class GeomMorph : MonoBehaviour
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
		[SerializeField] private float boundsEpsilon = 0.01f; // Configurable epsilon for boundary inclusion

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

			// Cache original vertices
			originalVertices = meshFilter.sharedMesh.vertices;
			modifiedVertices = new Vector3[originalVertices.Length];

			// Initialize influence volume
			UpdateInfluenceVolume();

			// Debug vertex inclusion
			DebugVertexInclusion();
		}

		private void UpdateInfluenceVolume()
		{
			if (!useCustomInfluenceVolume)
			{
				// Use mesh bounds, transformed to world space
				influenceVolume = CalculateWorldSpaceBounds();
			}
			else
			{
				// Ensure valid size
				Vector3 size = Vector3.Max(customInfluenceVolumeSize, Vector3.one * 0.01f);
				influenceVolume = new Bounds(transform.TransformPoint(customInfluenceVolumeCenter), size);
			}

			// Set anchor plane (bottom of the bounds) if not set by custom volume
			if (!useCustomInfluenceVolume)
			{
				anchorPlaneOffset = meshFilter.sharedMesh.bounds.min.y; // Bottom plane in local space
			}

			Debug.Log($"{GetType().Name}: Influence volume - Center: {influenceVolume.center}, Size: {influenceVolume.size}");
		}

		// Calculate world-space bounds accounting for transform
		private Bounds CalculateWorldSpaceBounds()
		{
			Bounds localBounds = meshFilter.sharedMesh.bounds;
			Vector3[] vertices = meshFilter.sharedMesh.vertices;
			Bounds worldBounds = new Bounds();

			// Transform all vertices to world space to compute accurate bounds
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
				// Fallback to transformed local bounds if no vertices
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

			// Define the plane in local space
			float planeOffset = offset;

			// Get mesh vertices in local space
			Vector3[] vertices = meshFilter.sharedMesh.vertices;
			float maxProj = float.MinValue;

			// Project vertices along the plane normal to find the furthest extent
			foreach (var vertex in vertices)
			{
				float proj = Vector3.Dot(vertex, planeNormal) - planeOffset;
				maxProj = Mathf.Max(maxProj, proj);
			}

			// If maxProj <= 0, no vertices are on the positive side of the plane
			if (maxProj <= 0)
			{
				Debug.LogWarning($"{GetType().Name}: No vertices found on the positive side of the specified plane. Using default bounds.");
				useCustomInfluenceVolume = false;
				UpdateInfluenceVolume();
				return;
			}

			// Set influence volume to encompass all vertices on the positive side of the plane
			float extent = maxProj;
			Vector3 center = planeNormal * (planeOffset + extent * 0.5f);
			Vector3 size = CalculateWorldSpaceBounds().size; // Use world-space bounds size as a base
			size = Vector3.Max(size, Vector3.one * 0.01f);
			size = new Vector3(size.x, extent, size.z);

			customInfluenceVolumeCenter = center; // Local space
			customInfluenceVolumeSize = size;
			UpdateInfluenceVolume();

			// Update anchor plane
			anchorPlaneNormal = planeNormal;
			anchorPlaneOffset = planeOffset;
		}

		protected virtual void Update()
		{
			if (!enabled || meshFilter == null) return;

			// Copy original vertices to modified array
			System.Array.Copy(originalVertices, modifiedVertices, originalVertices.Length);

			// Apply morphing effect
			ApplyMorphEffect();

			// Update mesh vertices
			meshFilter.mesh.vertices = modifiedVertices;
			meshFilter.mesh.RecalculateNormals();
		}

		protected abstract void ApplyMorphEffect();

		protected float GetDistanceToAnchorPlane(Vector3 vertexWorldPos)
		{
			// Convert vertex to local space for plane distance calculation
			Vector3 vertexLocalPos = transform.InverseTransformPoint(vertexWorldPos);
			return Vector3.Dot(vertexLocalPos, anchorPlaneNormal) - anchorPlaneOffset;
		}

		protected bool IsVertexInInfluenceVolume(Vector3 vertexWorldPos)
		{
			// Manual bounds check to include boundary vertices
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
		}

		// Debug vertex inclusion
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

		// Debug visualization for influence volume and vertices
		private void OnDrawGizmosSelected()
		{
			if (influenceVolume.size != Vector3.zero)
			{
				// Draw influence volume
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireCube(influenceVolume.center, influenceVolume.size);

				// Draw anchor plane
				Gizmos.color = Color.blue;
				Vector3 planeCenter = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);
				Vector3 planeSize = new Vector3(influenceVolume.size.x, 0.01f, influenceVolume.size.z);
				Quaternion planeRotation = Quaternion.LookRotation(anchorPlaneNormal);
				Gizmos.matrix = Matrix4x4.TRS(planeCenter, planeRotation, Vector3.one);
				Gizmos.DrawWireCube(Vector3.zero, planeSize);
				Gizmos.matrix = Matrix4x4.identity;

				// Draw vertices (included in green, excluded in red)
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
	}

	public class MorphGeomSway : GeomMorph
	{
		[SerializeField] private float swayAmplitude = 0.1f;
		[SerializeField] private float swayFrequency = 1f;
		[SerializeField] private Vector3 swayDirection = new Vector3(1f, 0f, 1f);
		[SerializeField] private bool useExternalPhase = false;
		[SerializeField, Range(0f, 1f)] private float phase = 0f;
		[SerializeField] public bool useExternalSwayVector = false; // Made public
		[SerializeField] public Vector3 externalSwayVector = Vector3.zero; // Made public

		// Public method to set external phase
		public void SetPhase(float normalizedPhase)
		{
			useExternalPhase = true;
			phase = Mathf.Clamp01(normalizedPhase);
		}

		// Public method to set external sway vector
		public void SetSwayVector(Vector3 swayVector)
		{
			useExternalSwayVector = true;
			externalSwayVector = swayVector;
		}

		// Rest of the class remains unchanged
		protected override void ApplyMorphEffect()
		{
			// Map world positions to vertex indices to ensure identical positions move together
			Dictionary<Vector3, List<int>> vertexGroups = new Dictionary<Vector3, List<int>>(Vector3EqualityComparer.Instance);

			// Group vertices by their world position
			for (int i = 0; i < originalVertices.Length; i++)
			{
				Vector3 worldPos = transform.TransformPoint(originalVertices[i]);
				if (!vertexGroups.ContainsKey(worldPos))
					vertexGroups[worldPos] = new List<int>();
				vertexGroups[worldPos].Add(i);
			}

			foreach (var group in vertexGroups)
			{
				Vector3 vertexWorldPos = group.Key;
				if (!IsVertexInInfluenceVolume(vertexWorldPos))
					continue;

				float distance = GetDistanceToAnchorPlane(vertexWorldPos);
				if (distance <= 0f) // At or below anchor plane
					continue;

				// Normalize distance
				float influenceHeight = influenceVolume.size.y;
				float normalizedDistance = Mathf.Clamp01(distance / influenceHeight);

				Vector3 swayVector;
				if (useExternalSwayVector)
				{
					swayVector = externalSwayVector * normalizedDistance;
				}
				else
				{
					// Calculate sway offset
					float phaseInput = useExternalPhase ? phase * 2f * Mathf.PI : Time.time * swayFrequency;
					float swayOffset = Mathf.Sin(phaseInput) * swayAmplitude * normalizedDistance;
					swayVector = swayDirection.normalized * swayOffset;
				}

				// Apply to all vertices in this group
				foreach (int index in group.Value)
				{
					modifiedVertices[index] = originalVertices[index] + transform.InverseTransformVector(swayVector);
				}
			}
		}

		protected override void OnValidate()
		{
			base.OnValidate();
			swayAmplitude = Mathf.Max(0f, swayAmplitude);
			swayFrequency = Mathf.Max(0f, swayFrequency);
			phase = Mathf.Clamp01(phase);
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