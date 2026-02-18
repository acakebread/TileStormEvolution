using UnityEngine;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class MultiTouchEmulatorTest : MonoBehaviour
	{
		[Header("Look (touch[1] / right mouse emulation)")]
		public float lookSensitivityX = 8.0f;
		public float lookSensitivityY = 8.0f;
		[Range(-90f, 0f)] public float pitchMin = -85f;
		[Range(0f, 90f)] public float pitchMax = 85f;

		public float zoomSpeed = 1f;

		private Camera cam;
		private float pitch = 20f;
		private float yaw = 0f;

		private bool panning;
		private Vector3? prevHit;

		private void Awake()
		{
			panning = false;

			cam = GetComponent<Camera>();
			if (cam == null)
			{
				Debug.LogError($"{nameof(MultiTouchEmulatorTest)} requires a Camera component", this);
				enabled = false;
				return;
			}

			ApplyRotation();
		}

		private void Update()
		{
			var touches = MultiTouchEmulator.touches;

			// Reset panning flag when no touches
			if (touches.Length == 0)
			{
				panning = false;
				prevHit = null;
			}

			// ────────────────────────────────────────────────
			// Rotation / look around ── using average delta of both fingers
			// ────────────────────────────────────────────────
			if (touches.Length >= 2)
			{
				// Find the two primary touches (0 and 1)
				Touch? t0 = null;
				Touch? t1 = null;

				foreach (var t in touches)
				{
					if (t.fingerId == 0) t0 = t;
					if (t.fingerId == 1) t1 = t;
				}

				if (t0.HasValue && t1.HasValue)
				{
					Vector2 delta0 = t0.Value.deltaPosition;
					Vector2 delta1 = t1.Value.deltaPosition;

					// Average movement of both fingers → this gives rotation
					Vector2 avgDelta = (delta0 + delta1) * 0.5f;

					yaw += avgDelta.x * lookSensitivityX;
					pitch -= avgDelta.y * lookSensitivityY;

					pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
					ApplyRotation();
				}
			}

			// ────────────────────────────────────────────────
			// Pan ── left mouse / single touch[0]
			// ────────────────────────────────────────────────
			Touch? panTouch = null;
			foreach (var t in touches)
			{
				if (t.fingerId == 0)
				{
					panTouch = t;
					break;
				}
			}

			// Only pan with exactly one touch (touch[0]), or left mouse emulation
			if (panTouch.HasValue && touches.Length == 1)
			{
				if (!panning)
				{
					prevHit = ScreenPointToPlaneY0(panTouch.Value.position);
					panning = prevHit.HasValue;
				}

				if (panning && prevHit.HasValue)
				{
					Vector3? currentHit = ScreenPointToPlaneY0(panTouch.Value.position);
					if (currentHit.HasValue)
					{
						transform.position += prevHit.Value - currentHit.Value;
						// Optional: update prevHit to current for next frame (smoother feel)
						// prevHit = currentHit;
					}
				}
			}

			// ────────────────────────────────────────────────
			// Pinch zoom ── when two touches exist
			// ────────────────────────────────────────────────
			if (touches.Length == 2)
			{
				Touch ta = touches[0].fingerId == 0 ? touches[0] : touches[1];
				Touch tb = touches[0].fingerId == 1 ? touches[0] : touches[1];

				Vector2 posA_prev = ta.position;
				Vector2 posB_prev = tb.position;
				Vector2 posA_curr = ta.position + ta.deltaPosition;
				Vector2 posB_curr = tb.position + tb.deltaPosition;

				float dist_prev = Vector2.Distance(posA_prev, posB_prev);
				float dist_curr = Vector2.Distance(posA_curr, posB_curr);

				float deltaDistance = dist_curr - dist_prev;

				if (Mathf.Abs(deltaDistance) > 0.8f) // small deadzone to avoid jitter
				{
					// forward = zoom in (pinch out), backward = zoom out (pinch in)
					Vector3 zoomDelta = transform.forward * (deltaDistance * Time.deltaTime * zoomSpeed);

					transform.position += zoomDelta;
				}
			}
		}

		private void ApplyRotation()
		{
			transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
		}

		private Vector3? ScreenPointToPlaneY0(Vector3 screenPos)
		{
			//if (screenPos.z <= 0) return null; // behind camera

			Ray ray = cam.ScreenPointToRay(screenPos);
			Plane plane = new Plane(Vector3.up, Vector3.zero);

			if (plane.Raycast(ray, out float enter) && enter > 0.05f)
			{
				return ray.GetPoint(enter);
			}
			return null;
		}

		// Optional: show which touches are active (for debugging)
		private void OnGUI()
		{
			var touches = MultiTouchEmulator.touches; 
			
			if (touches.Length == 0) return;

			var style = new GUIStyle { fontSize = 14, normal = { textColor = Color.white } };
			GUILayout.Label($"Active touches: {touches.Length}", style);

			foreach (var t in MultiTouchEmulator.touches)
			{
				GUILayout.Label($"  ID {t.fingerId}  pos {t.position:F0}  Δ {t.deltaPosition:F1}  {t.phase}", style);
			}
		}
	}
}
