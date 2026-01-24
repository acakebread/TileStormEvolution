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

			// Create readable/writable copy (using the method that worked)
			Mesh writableMesh = MeshUtils.CreateReadableCopyViaBake(filter.sharedMesh);

			if (writableMesh == null)
			{
				Debug.LogWarning($"Bake method failed for {target.name} - trying fallback combine method...");
				writableMesh = MeshUtils.CreateReadableCopyViaCombine(filter.sharedMesh);
			}

			if (writableMesh == null)
			{
				Debug.LogError($"Failed to create readable mesh copy for {target.name}");
				return null;
			}

			// Assign our own writable mesh
			filter.mesh = writableMesh;

			// Apply defaults / parameters
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


	//public class MorphGeomSway : MorphGeomBase
	//{
	//	[SerializeField] private float swayAmplitude = 0.1f;
	//	[SerializeField] private float swayFrequency = 1f;
	//	[SerializeField] private Vector3 swayDirection = new Vector3(1f, 0f, 1f);
	//	[SerializeField] private bool useExternalPhase = false;
	//	[SerializeField, Range(0f, 1f)] private float phase = 0f;
	//	[SerializeField] public bool useExternalSwayVector = false;
	//	[SerializeField] public Vector3 externalSwayVector = Vector3.zero;
	//	[SerializeField] public float swayInfluencePower = 2f; // Power for non-linear rotation influence

	//	protected override void Awake() => base.Awake();

	//	public void SetPhase(float normalizedPhase)
	//	{
	//		phase = Mathf.Clamp01(normalizedPhase);
	//	}

	//	public void SetSwayVector(Vector3 swayVector)
	//	{
	//		useExternalSwayVector = true;
	//		externalSwayVector = swayVector;
	//	}

	//	protected override void ApplyMorphEffect()
	//	{
	//		Vector3 worldAnchorPlaneNormal = transform.TransformDirection(anchorPlaneNormal);
	//		Vector3 pivot = transform.TransformPoint(anchorPlaneNormal * anchorPlaneOffset);
	//		float phaseInput = useExternalPhase ? phase * 2f * Mathf.PI : Time.time * swayFrequency;
	//		float influenceHeight = influenceVolume.size.y;

	//		foreach (var group in cachedVertexGroups)
	//		{
	//			Vector3 vertexWorldPos = group.Key;
	//			if (!IsVertexInInfluenceVolume(vertexWorldPos))
	//				continue;

	//			float distance = GetDistanceToAnchorPlane(vertexWorldPos);
	//			if (distance <= 0f)
	//				continue;

	//			float normalizedDistance = Mathf.Clamp01(distance / influenceHeight);
	//			Vector3 swayVector = useExternalSwayVector
	//				? externalSwayVector * normalizedDistance
	//				: swayDirection.normalized * (Mathf.Sin(phaseInput) * swayAmplitude * normalizedDistance);

	//			Vector3 swayDir = swayVector.normalized;
	//			Vector3 rotationAxis = Vector3.Cross(worldAnchorPlaneNormal, swayDir);
	//			if (rotationAxis.sqrMagnitude < 0.0001f)
	//			{
	//				rotationAxis = Vector3.Cross(worldAnchorPlaneNormal, Vector3.up);
	//				if (rotationAxis.sqrMagnitude < 0.0001f)
	//					rotationAxis = Vector3.right;
	//			}
	//			rotationAxis = rotationAxis.normalized;

	//			float maxAngle = swayVector.magnitude * Mathf.Rad2Deg;
	//			float influence = Mathf.Pow(normalizedDistance, swayInfluencePower);
	//			float angle = maxAngle * influence;

	//			Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

	//			foreach (int index in group.Value)
	//			{
	//				Vector3 worldVertex = originalWorldVertices[index];
	//				Vector3 relativePos = worldVertex - pivot;
	//				Vector3 rotatedRelativePos = rotation * relativePos;
	//				Vector3 newWorldPos = pivot + rotatedRelativePos;
	//				modifiedVertices[index] = transform.InverseTransformPoint(newWorldPos);
	//			}
	//		}
	//	}

	//	protected override void OnValidate()
	//	{
	//		base.OnValidate();
	//		swayAmplitude = Mathf.Max(0f, swayAmplitude);
	//		swayFrequency = Mathf.Max(0f, swayFrequency);
	//		phase = Mathf.Clamp01(phase);
	//		swayInfluencePower = Mathf.Max(0.1f, swayInfluencePower);
	//	}

	//	public struct MorphGeomSwayParameters
	//	{
	//		public Vector3 normal;
	//		public float offset;
	//		public float swayInfluencePower;
	//	}


	//	public static MorphGeomSway AddGeomSway(GameObject gameObject, MorphGeomSwayParameters? parameters = null)
	//	{
	//		var filter = gameObject.GetComponentInChildren<MeshFilter>(true);
	//		if (filter == null) return null;

	//		Mesh source = filter.sharedMesh;
	//		if (source == null) return null;

	//		// Create deep, readable copy
	//		Mesh writableMesh = CreateReadableCopyViaBake(source);// CreateReadableCopy(source, source.name + "_RuntimeCopy");

	//		if (writableMesh == null)
	//		{
	//			Debug.LogError($"Failed to create readable copy for {gameObject.name}");
	//			return null;
	//		}

	//		// Assign it – now it's ours
	//		filter.mesh = writableMesh;

	//		//defaults
	//		var normal = parameters.HasValue ? parameters.Value.normal : Vector3.up;
	//		var offset = null != parameters ? 0.2f : 0f;
	//		var swayInfluencePower = 0.5f; // More top sway
	//		var maxSegmentLength = 0.3f; // Enable stratification with maxSegmentLength for influence volume
	//		var enabled = 0f != maxSegmentLength;

	//		var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
	//		morphGeomSway.SetCustomInfluenceVolume(normal, 0.2f);
	//		morphGeomSway.swayInfluencePower = swayInfluencePower;
	//		morphGeomSway.ConfigureSubdivision(enabled, maxSegmentLength);
	//		return morphGeomSway;
	//	}


	//	public static Mesh CreateReadableCopyViaBake(Mesh original)
	//	{
	//		if (!original) return null;

	//		GameObject temp = new GameObject("BakeHelper");
	//		temp.hideFlags = HideFlags.HideAndDontSave;

	//		try
	//		{
	//			SkinnedMeshRenderer skinned = temp.AddComponent<SkinnedMeshRenderer>();
	//			skinned.sharedMesh = original;              // ← allowed even if not readable

	//			Mesh baked = new Mesh();
	//			skinned.BakeMesh(baked, true);              // ← this copies vertex data!

	//			baked.name = original.name + "_BakedCopy";
	//			baked.MarkDynamic();

	//			return baked;
	//		}
	//		finally
	//		{
	//			Object.DestroyImmediate(temp);
	//		}
	//	}

	//	public static Mesh CreateReadableCopy(Mesh original, string newName = "RuntimeCopy")
	//	{
	//		if (original == null) return null;

	//		// Step 1: Create a blank writable mesh
	//		Mesh copy = new Mesh();
	//		copy.name = newName;

	//		// Step 2: Use CombineMeshes with a dummy GameObject to force deep copy
	//		// This is one of the few officially supported ways to copy non-readable meshes
	//		GameObject tempGO = new GameObject("TempMeshCopier");
	//		tempGO.hideFlags = HideFlags.HideAndDontSave;

	//		try
	//		{
	//			MeshFilter tempFilter = tempGO.AddComponent<MeshFilter>();
	//			tempFilter.sharedMesh = original;  // ← even non-readable is ok here

	//			MeshRenderer tempRenderer = tempGO.AddComponent<MeshRenderer>();

	//			CombineInstance[] combine = new CombineInstance[1];
	//			combine[0].mesh = original;
	//			combine[0].transform = Matrix4x4.identity;

	//			copy.CombineMeshes(combine, true, false, false);

	//			// Important flags
	//			copy.MarkDynamic();           // hint for frequent modification
	//			copy.RecalculateBounds();
	//			copy.RecalculateNormals();    // if you need them
	//										  // copy.RecalculateTangents(); // if you use normal maps

	//			return copy;
	//		}
	//		finally
	//		{
	//			Object.DestroyImmediate(tempGO);
	//		}
	//	}

	//	//public static MorphGeomSway AddGeomSway(GameObject gameObject, MorphGeomSwayParameters? parameters = null)
	//	//{
	//	//	var filter = gameObject.GetComponentInChildren<MeshFilter>(true);
	//	//	// Inside AddGeomSway, right after getting the filter:
	//	//	if (!filter.IsRuntimeWritable())
	//	//	{
	//	//		filter.mesh = Instantiate(filter.sharedMesh);
	//	//		// Optional: 
	//	//		// filter.mesh.name += " (runtime copy)";
	//	//	}

	//	//	//if (null == filter || !filter.IsRuntimeWritable())
	//	//	//{
	//	//	//	Debug.LogError($"geometry not writable in: {gameObject.name}");
	//	//	//	return null;
	//	//	//}

	//	//	//defaults
	//	//	var normal = parameters.HasValue ? parameters.Value.normal : Vector3.up;
	//	//	var offset = null != parameters ? 0.2f : 0f;
	//	//	var swayInfluencePower = 0.5f; // More top sway
	//	//	var maxSegmentLength = 0.3f; // Enable stratification with maxSegmentLength for influence volume
	//	//	var enabled = 0f != maxSegmentLength;

	//	//	var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
	//	//	morphGeomSway.SetCustomInfluenceVolume(normal, 0.2f);
	//	//	morphGeomSway.swayInfluencePower = swayInfluencePower;
	//	//	morphGeomSway.ConfigureSubdivision(enabled, maxSegmentLength);
	//	//	return morphGeomSway;
	//	//}

	//	//private static Mesh GetOrCreateWritableMesh(MeshFilter filter)
	//	//{
	//	//	if (filter.mesh != null && filter.IsRuntimeWritable())
	//	//		return filter.mesh;

	//	//	Mesh newMesh = Object.Instantiate(filter.sharedMesh);
	//	//	newMesh.name = filter.sharedMesh.name + " (Runtime Instance)";
	//	//	filter.mesh = newMesh;
	//	//	return newMesh;
	//	//}
	//}
}