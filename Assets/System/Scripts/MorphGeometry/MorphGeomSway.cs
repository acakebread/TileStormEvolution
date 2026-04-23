//#define VERBOSE

using UnityEngine;

namespace MassiveHadronLtd
{
	// =========================================================================
	// Main sway component
	// =========================================================================
	public class MorphGeomSway : MorphGeomBase
	{
		[Header("Sway Settings")]
		[SerializeField] private float swayAmplitude = 0.1f;
		[SerializeField] private float swayFrequency = 1f;
		[SerializeField] private Vector3 swayDirection = new Vector3(1f, 0f, 1f);

		[Header("Advanced")]
		[SerializeField] private bool useExternalPhase = false;
		[SerializeField, Range(0f, 1f)] private float phase = 0f;
		[SerializeField] public bool useExternalSwayVector = false;
		[SerializeField] public Vector3 externalSwayVector = Vector3.zero;
		[SerializeField] public float swayInfluencePower = 2f;

		protected override void Awake() => base.Awake();

		public void SetPhase(float normalizedPhase)
		{
			phase = Mathf.Clamp01(normalizedPhase);
		}

		public void SetSwayVector(Vector3 swayVector)
		{
			useExternalSwayVector = true;
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
				if (distance <= 0f) continue;

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

				rotationAxis.Normalize();

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

		public struct MorphGeomSwayParameters
		{
			public Vector3 normal;
			public float offset;
			public float swayInfluencePower;

			public static MorphGeomSwayParameters Default => new MorphGeomSwayParameters
			{
				normal = Vector3.up,
				offset = 0.3f,              // or 0.3f if you want legacy parity
				swayInfluencePower = 0.5f
			};
		}

		public static MorphGeomSway AddGeomSway(GameObject target, MorphGeomSwayParameters? parameters = null)
		{
			if (target == null) return null;

			var filter = target.GetComponentInChildren<MeshFilter>(true);
			if (filter == null || filter.sharedMesh == null)
			{
				Debug.LogError($"No valid MeshFilter or sharedMesh found on {target.name}");
				return null;
			}

			Mesh targetMesh = filter.sharedMesh;

			// ────────────────────────────────────────────────────────────────
			// Check if already readable → use original + warn for future fix
			// ────────────────────────────────────────────────────────────────
			if (targetMesh.isReadable)
			{
#if VERBOSE
				Debug.LogWarning(
					$"Mesh '{targetMesh.name}' on '{target.name}' is already readable. " +
					"Skipping runtime copy (good for performance). " +
					"Consider enabling Read/Write only if you need runtime modifications on this model. " +
					"For WebGL optimization, keep non-modified meshes non-readable."
				);
#endif

				// No need to assign a copy — original is already writable
				// (but we still assign filter.mesh = targetMesh just to be explicit/safe)
				filter.mesh = targetMesh;
			}
			else
			{
				// Non-readable → attempt to create writable copy (will likely fail in WebGL)
				Debug.Log($"Creating writable copy for non-readable mesh '{targetMesh.name}' on '{target.name}'... " +
						  "(this may fail in WebGL builds — consider enabling Read/Write in import settings)");

				Mesh writableMesh = MeshUtils.CreateReadableCopyViaBake(targetMesh);

				if (writableMesh == null || writableMesh.vertexCount == 0)
				{
					Debug.LogWarning($"Bake method failed for '{target.name}' — trying fallback combine method...");
					writableMesh = MeshUtils.CreateReadableCopyViaCombine(targetMesh);
				}

				if (writableMesh == null || writableMesh.vertexCount == 0)
				{
					Debug.LogError(
						$"Failed to create readable mesh copy for '{target.name}' " +
						$"(original verts: {targetMesh.vertexCount}, isReadable: {targetMesh.isReadable}). " +
						"Mesh modifications will not work in builds (especially WebGL). " +
						"Enable Read/Write Enabled in the model's import settings."
					);

					// Optional: early exit or fallback to a dummy mesh
					// return null;  // ← uncomment if you want to abort adding the component
					// or: writableMesh = MeshUtils.GenerateQuadXZ(); // procedural fallback
				}
				else
				{
					Debug.Log($"Writable copy created successfully ({writableMesh.vertexCount} verts)");
				}

				// Assign whatever we got (even if empty — so you see broken visuals and know to fix)
				filter.mesh = writableMesh;
				targetMesh = writableMesh; // for the rest of the method if needed
			}

			// ────────────────────────────────────────────────────────────────
			// Proceed with adding the component (parameters, etc.)
			// ────────────────────────────────────────────────────────────────
			var p = parameters ?? MorphGeomSwayParameters.Default;

			var sway = target.AddComponent<MorphGeomSway>();

			sway.SetCustomInfluenceVolume(p.normal, p.offset);
			sway.swayInfluencePower = p.swayInfluencePower > 0f ? p.swayInfluencePower : 0.5f;

			// Optional: enable stratification/subdivision
			const float defaultMaxSegment = 0.3f;
			sway.ConfigureSubdivision(true, defaultMaxSegment);

			return sway;
		}
	}
}