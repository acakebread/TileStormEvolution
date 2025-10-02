using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class MorphGeomSway : MorphGeomBase
	{
		[SerializeField] private float swayAmplitude = 0.1f;
		[SerializeField] private float swayFrequency = 1f;
		[SerializeField] private Vector3 swayDirection = new Vector3(1f, 0f, 1f);
		[SerializeField] private bool useExternalPhase = false;
		[SerializeField, Range(0f, 1f)] private float phase = 0f;
		[SerializeField] public bool useExternalSwayVector = false;
		[SerializeField] public Vector3 externalSwayVector = Vector3.zero;

		// Public method to set external phase
		public void SetPhase(float normalizedPhase)
		{
			//useExternalPhase = true;//this is set externally
			phase = Mathf.Clamp01(normalizedPhase);
		}

		// Public method to set external sway vector
		public void SetSwayVector(Vector3 swayVector)
		{
			//useExternalSwayVector = true;//this is set externally
			externalSwayVector = swayVector;
		}

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

			// Compute rotation pivot (center of anchor plane in world space)
			Vector3 worldAnchorPlaneNormal = transform.TransformDirection(anchorPlaneNormal);
			Vector3 pivot = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);

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

				// Compute rotation axis (cross product of plane normal and sway direction)
				Vector3 swayDir = swayVector.normalized;
				Vector3 rotationAxis = Vector3.Cross(worldAnchorPlaneNormal, swayDir);
				if (rotationAxis.sqrMagnitude < 0.0001f) // Avoid singularity if parallel
				{
					rotationAxis = Vector3.Cross(worldAnchorPlaneNormal, Vector3.up); // Fallback axis
					if (rotationAxis.sqrMagnitude < 0.0001f)
						rotationAxis = Vector3.right; // Final fallback
				}
				rotationAxis = rotationAxis.normalized;

				// Compute rotation angle (based on sway magnitude, scaled by distance)
				float maxAngle = swayVector.magnitude * Mathf.Rad2Deg; // Convert magnitude to degrees
				float angle = maxAngle * normalizedDistance;

				// Create rotation quaternion
				Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

				// Apply rotation to vertex relative to pivot
				foreach (int index in group.Value)
				{
					Vector3 localVertex = originalVertices[index];
					Vector3 worldVertex = transform.TransformPoint(localVertex);
					Vector3 relativePos = worldVertex - pivot;
					Vector3 rotatedRelativePos = rotation * relativePos;
					Vector3 newWorldPos = pivot + rotatedRelativePos;
					modifiedVertices[index] = transform.InverseTransformPoint(newWorldPos);
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
}