using UnityEngine;
using System.Linq;

namespace MassiveHadronLtd
{
	public class CameraPath : CameraBase
	{
		private const float VerticalOffset = 0.5f;
		private const float MinDistance = 1f;
		private const float MinFocusPointDistanceFromPlayer = 4f;
		private const float MaxLookAtAngle = 20f;
		private const float FovMin = 35f;
		private const float FovMax = 55f;
		private const float MinCameraHeight = 1.5f;
		private const float MaxCameraHeight = 4f;
		private const float TangentLineExtension = 2f;
		private const bool SortDstNearerPlayer = true;

		private const float DEBUG_DRAW_DURATION = 5f;
		public static bool DEBUG_VISUALIZE_LOZENGE = true;

		protected const float PauseDuration = 1.5f;
		protected const float DefaultSequenceDuration = 8f;
		protected float pauseTimer;
		protected float sequenceTimer;
		protected float currentSequenceDuration;

		private float currentFovMax;
		private BezierData bezierData;

		private struct BezierData
		{
			public Vector3 P0; // Src
			public Vector3 P1; // Control point
			public Vector3 P2; // Dst
		}

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

		protected override void Start()
		{
			if (playerTransform == null) return;

			sequenceTimer = DefaultSequenceDuration + Random.Range(-2, 2);
			pauseTimer = PauseDuration;
			currentSequenceDuration = sequenceTimer;
			currentFovMax = FovMax;

			_data.shake = 1f;
			_data.smoothing = CameraData.DefaultSmoothingRate;
			_data.enablePostProcessing = true;

			// Select start focus point
			var startFocusPoint = playerTransform.position;
			if (focusPoints.Count > 0)
			{
				var validFocusPoint = focusPoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) > MinFocusPointDistanceFromPlayer)
					.OrderBy(p => SortDstNearerPlayer ? Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) : -Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)))
					.FirstOrDefault();
				if (validFocusPoint != Vector3.zero)
					startFocusPoint = validFocusPoint;
			}

			// Select end focus point
			var endFocusPoint = startFocusPoint;
			if (focusPoints.Count > 1)
			{
				var validEndFocusPoints = focusPoints.Where(p => p != startFocusPoint && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) > MinFocusPointDistanceFromPlayer)
					.OrderBy(p => SortDstNearerPlayer ? Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) : -Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)))
					.Take(2).ToList();
				if (validEndFocusPoints.Any())
					endFocusPoint = validEndFocusPoints[Random.Range(0, validEndFocusPoints.Count)];
			}

			// Compute lozenge
			var pathDir = (endFocusPoint - startFocusPoint);
			var lozengeMajor = pathDir.magnitude / 2f;
			var pathDirNorm = pathDir.normalized;
			var perpendicular = Vector3.Cross(Vector3.up, pathDirNorm).normalized;

			var lozengeMinor = Random.Range(MinDistance, lozengeMajor * 0.5f);
			var midPoint = (startFocusPoint + endFocusPoint) / 2f;

			// Generate source and destination points
			var (srcPoint, srcPerimeterPoint, srcTangent1, srcTangent2, srcTheta, srcProjectionDistance) = GeneratePointOutsideLozenge(new Vector2(midPoint.x, midPoint.z), pathDirNorm, perpendicular, lozengeMajor, lozengeMinor);
			var (dstPoint, dstPerimeterPoint, dstTangent1, dstTangent2, dstTheta, dstProjectionDistance) = GeneratePointOutsideLozenge(new Vector2(midPoint.x, midPoint.z), pathDirNorm, perpendicular, lozengeMajor, lozengeMinor);

			// Compute control point (midpoint with random height, as GetIntersectionPoint is unavailable)
			var controlPoint = (srcPoint + dstPoint) / 2f;
			controlPoint.y = Random.Range(MinCameraHeight, MaxCameraHeight);

			bezierData.P0 = srcPoint;
			bezierData.P1 = controlPoint;
			bezierData.P2 = dstPoint;

			_data.position = srcPoint;
			_data.target = startFocusPoint + Vector3.up * VerticalOffset;
			_data.lerpedTarget = _data.target;
			_data.fieldOfView = FovMin;

			if (DEBUG_VISUALIZE_LOZENGE)
				VisualizeLozenge(midPoint, pathDirNorm, perpendicular, lozengeMajor, lozengeMinor, (srcPoint.y + dstPoint.y) / 2f);

			// Debug visualization
			Debug.DrawLine(bezierData.P0, bezierData.P1, Color.red, DEBUG_DRAW_DURATION);
			Debug.DrawLine(bezierData.P1, bezierData.P2, Color.red, DEBUG_DRAW_DURATION);
			Debug.DrawRay(bezierData.P0, Vector3.up * 2f, Color.red, DEBUG_DRAW_DURATION);
			Debug.DrawRay(bezierData.P2, Vector3.up * 2f, Color.red, DEBUG_DRAW_DURATION);
		}

		protected override void Update()
		{
			if (_data.camera == null || playerTransform == null) return;

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer <= 0f)
			{
				if (pauseTimer <= 0f) return;
				pauseTimer -= Time.deltaTime;
				return;
			}

			var easedSequenceTimer = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);

			// Update camera path
			_data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, easedSequenceTimer);

			var q0 = Vector3.Lerp(bezierData.P0, bezierData.P1, easedSequenceTimer); // Q0(t)
			var q1 = Vector3.Lerp(bezierData.P1, bezierData.P2, easedSequenceTimer); // Q1(t)
			var b = Vector3.Lerp(q0, q1, easedSequenceTimer); // B(t)

			_data.position = b;
			_data.target = Vector3.Lerp(bezierData.P0 + Vector3.up * VerticalOffset, bezierData.P2 + Vector3.up * VerticalOffset, easedSequenceTimer);
			_data.lerpedTarget = _data.target;

			Debug.DrawRay(q0, Vector3.up * 1f, Color.red, DEBUG_DRAW_DURATION); // Q0(t)
			Debug.DrawRay(q1, Vector3.up * 1f, Color.blue, DEBUG_DRAW_DURATION); // Q1(t)
			Debug.DrawRay(b, Vector3.up * 1.5f, Color.green, DEBUG_DRAW_DURATION); // B(t)
			Debug.DrawLine(q0, q1, Color.gray, DEBUG_DRAW_DURATION); // Line from Q0 to Q1
		}

		private (Vector3 point, Vector2 perimeterPoint, Vector3 tangent1, Vector3 tangent2, float theta, float projectionDistance) GeneratePointOutsideLozenge(Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
		{
			var theta = Random.Range(0f, 2f * Mathf.PI);
			var perimeterPoint = MathEllipse.GetEllipsePoint(theta, lozengeMajor, lozengeMinor, midPointXZ, pathDir, perpendicular);
			var direction = (perimeterPoint - midPointXZ).normalized;
			var distanceToPerimeter = Vector2.Distance(midPointXZ, perimeterPoint);
			var projectionDistance = distanceToPerimeter + Random.Range(1f, 5f);
			var pointXZ = midPointXZ + direction * projectionDistance;
			var point = new Vector3(pointXZ.x, Random.Range(MinCameraHeight, MaxCameraHeight), pointXZ.y);

			var (tangent1, tangent2) = MathEllipse.ComputeTangentAtPoint(theta, lozengeMajor, lozengeMinor, pathDir, perpendicular);

			return (point, perimeterPoint, tangent1, tangent2, theta, projectionDistance);
		}

		private void VisualizeLozenge(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, float height)
		{
			const int segments = 36;
			var points = new Vector3[segments + 1];
			for (var i = 0; i <= segments; i++)
			{
				var angle = i * 2f * Mathf.PI / segments;
				Vector2 pointXZ = MathEllipse.GetEllipsePoint(angle, lozengeMajor, lozengeMinor, new Vector2(midPoint.x, midPoint.z), pathDir, perpendicular);
				points[i] = new Vector3(pointXZ.x, height, pointXZ.y);
			}
			for (var i = 0; i < segments; i++)
			{
				Debug.DrawLine(points[i], points[i + 1], Color.green, DEBUG_DRAW_DURATION);
			}
		}
	}
}