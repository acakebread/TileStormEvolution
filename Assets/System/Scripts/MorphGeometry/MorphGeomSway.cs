using UnityEngine;

namespace MassiveHadronLtd
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
		[SerializeField] public float swayInfluencePower = 2f; // Power for non-linear rotation influence

		public void SetPhase(float normalizedPhase)
		{
			phase = Mathf.Clamp01(normalizedPhase);
		}

		public void SetSwayVector(Vector3 swayVector)
		{
			externalSwayVector = swayVector;
		}

		protected override void ApplyMorphEffect()
		{
			Vector3 worldAnchorPlaneNormal = transform.TransformDirection(anchorPlaneNormal);
			Vector3 pivot = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);
			float phaseInput = useExternalPhase ? phase * 2f * Mathf.PI : Time.time * swayFrequency;
			float influenceHeight = influenceVolume.size.y;

			foreach (var group in cachedVertexGroups)
			{
				Vector3 vertexWorldPos = group.Key;
				if (!IsVertexInInfluenceVolume(vertexWorldPos))
					continue;

				float distance = GetDistanceToAnchorPlane(vertexWorldPos);
				if (distance <= 0f)
					continue;

				float normalizedDistance = Mathf.Clamp01(distance / influenceHeight);
				Vector3 swayVector = useExternalSwayVector
					? externalSwayVector * normalizedDistance
					: swayDirection.normalized * (Mathf.Sin(phaseInput) * swayAmplitude * normalizedDistance);

				Vector3 swayDir = swayVector.normalized;
				Vector3 rotationAxis = Vector3.Cross(worldAnchorPlaneNormal, swayDir);
				if (rotationAxis.sqrMagnitude < 0.0001f)
				{
					rotationAxis = Vector3.Cross(worldAnchorPlaneNormal, Vector3.up);
					if (rotationAxis.sqrMagnitude < 0.0001f)
						rotationAxis = Vector3.right;
				}
				rotationAxis = rotationAxis.normalized;

				float maxAngle = swayVector.magnitude * Mathf.Rad2Deg;
				float influence = Mathf.Pow(normalizedDistance, swayInfluencePower);
				float angle = maxAngle * influence;

				Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

				foreach (int index in group.Value)
				{
					Vector3 worldVertex = originalWorldVertices[index];
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
			swayInfluencePower = Mathf.Max(0.1f, swayInfluencePower);
		}
	}
}