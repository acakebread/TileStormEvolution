using MassiveHadronLtd;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DebugTools
{
	public class MouseInputDebugger : MonoBehaviour
	{
		[Header("Display")]
		public bool showGUI = true;
		public float graphScale = 10f;

		private float dbg_dx, dbg_dy;
		private float dbg_peak;
		private float dbg_accum;
		private int dbg_samples;

		private const int DBG_HISTORY = 120;
		private readonly float[] dbg_history = new float[DBG_HISTORY];
		private int dbg_index = 0;

		private void Update()
		{
			float dx = 0f;
			float dy = 0f;

			// Read raw mouse delta
#if ENABLE_INPUT_SYSTEM
			if (Mouse.current != null)
			{
				Vector2 delta = Mouse.current.delta.ReadValue();
				dx = delta.x;
				dy = delta.y;
			}
#endif

			float rawMag = Mathf.Sqrt(dx * dx + dy * dy);

			// Apply normalization (Editor = 1.0, WebGL = fixed scale)
			float normalizedMag = MouseDeltaNormalizer.GetNormalizedMagnitude(rawMag);

			// Store values for display and graph
			dbg_dx = dx;
			dbg_dy = dy;
			float mag = normalizedMag;   // Use normalized value for stats & graph

			// Update stats
			if (mag > dbg_peak)
				dbg_peak = mag;

			dbg_accum += mag;
			dbg_samples++;

			dbg_history[dbg_index] = mag;
			dbg_index = (dbg_index + 1) % DBG_HISTORY;
		}

		private void OnGUI()
		{
			if (!showGUI)
				return;

			float x = 10f;
			float y = 10f;

			GUI.Box(new Rect(x, y, 420, 340), "Mouse Debug + Normalizer");

			// Raw values
			GUI.Label(new Rect(x + 10, y + 30, 400, 20), $"Raw dx: {dbg_dx:F4}    dy: {dbg_dy:F4}");
			float rawMag = Mathf.Sqrt(dbg_dx * dbg_dx + dbg_dy * dbg_dy);
			GUI.Label(new Rect(x + 10, y + 50, 400, 20), $"Raw mag: {rawMag:F4}");

			// Normalized values
			GUI.Label(new Rect(x + 10, y + 70, 400, 20), $"Normalized mag: {rawMag * MouseDeltaNormalizer.GetCurrentScale():F4}");

			float avg = dbg_samples > 0 ? dbg_accum / dbg_samples : 0f;
			GUI.Label(new Rect(x + 10, y + 90, 400, 20), $"Avg (normalized): {avg:F4}");
			GUI.Label(new Rect(x + 10, y + 110, 400, 20), $"Peak (normalized): {dbg_peak:F4}");

			// Scale information
			GUI.Label(new Rect(x + 10, y + 140, 400, 20), $"Scale factor: {MouseDeltaNormalizer.GetCurrentScale():F4}");

			if (Application.isEditor)
			{
				GUI.Label(new Rect(x + 10, y + 160, 400, 20), "Running in UNITY EDITOR → No scaling applied (scale = 1.0)");
			}
			else if (Application.platform == RuntimePlatform.WebGLPlayer)
			{
				GUI.Label(new Rect(x + 10, y + 160, 400, 20), "WebGL Build → Fixed scaling applied");
			}

			// Calibration / Reset button (kept for convenience)
			if (GUI.Button(new Rect(x + 10, y + 190, 200, 30), "Reset Stats"))
			{
				ResetStats();
			}

			// Graph (shows normalized history)
			float graphX = x + 10;
			float graphY = y + 230;
			float graphW = 300;
			float graphH = 70;

			GUI.Box(new Rect(graphX, graphY, graphW, graphH), "Normalized Mouse Magnitude History");

			for (int i = 0; i < DBG_HISTORY; i++)
			{
				int idx = (dbg_index + i) % DBG_HISTORY;
				float v = dbg_history[idx];
				float px = graphX + (i / (float)DBG_HISTORY) * graphW;
				float h = Mathf.Clamp(v * graphScale, 0f, graphH);

				GUI.DrawTexture(new Rect(px, graphY + graphH - h, 2, h), Texture2D.whiteTexture);
			}
		}

		// Optional helper
		public void ResetStats()
		{
			dbg_peak = 0f;
			dbg_accum = 0f;
			dbg_samples = 0;
			for (int i = 0; i < DBG_HISTORY; i++)
				dbg_history[i] = 0f;
		}
	}
}

//using MassiveHadronLtd;
//using UnityEngine;
//#if ENABLE_INPUT_SYSTEM
//using UnityEngine.InputSystem;
//#endif

//namespace DebugTools
//{
//	public class MouseInputDebugger : MonoBehaviour
//	{
//		[Header("Display")]
//		public bool showGUI = true;
//		public float graphScale = 10f;

//		private float dbg_dx, dbg_dy;
//		private float dbg_peak;
//		private float dbg_accum;
//		private int dbg_samples;

//		private const int DBG_HISTORY = 120;
//		private readonly float[] dbg_history = new float[DBG_HISTORY];
//		private int dbg_index = 0;

//		private void Update()
//		{
//			float dx = 0f;
//			float dy = 0f;

//#if ENABLE_INPUT_SYSTEM
//			if (Mouse.current != null)
//			{
//				Vector2 delta = Mouse.current.delta.ReadValue();
//				dx = delta.x;
//				dy = delta.y;
//			}
//#elif ENABLE_LEGACY_INPUT_MANAGER
//            dx = Input.GetAxisRaw("Mouse X");
//            dy = Input.GetAxisRaw("Mouse Y");
//#endif

//			float rawMag = Mathf.Sqrt(dx * dx + dy * dy);
//			float normalizedMag = MouseDeltaNormalizer.GetNormalizedMagnitude(rawMag);

//			dbg_dx = dx;
//			dbg_dy = dy;

//			float mag = normalizedMag;

//			if (mag > dbg_peak) dbg_peak = mag;
//			dbg_accum += mag;
//			dbg_samples++;

//			dbg_history[dbg_index] = mag;
//			dbg_index = (dbg_index + 1) % DBG_HISTORY;
//		}

//		private void OnGUI()
//		{
//			if (!showGUI) return;

//			float x = 10;
//			float y = 10;

//			GUI.Box(new Rect(x, y, 400, 340), "Mouse Debug + Normalizer");

//			GUI.Label(new Rect(x + 10, y + 30, 380, 20), $"Raw dx: {dbg_dx:F4}   dy: {dbg_dy:F4}");
//			float rawMag = Mathf.Sqrt(dbg_dx * dbg_dx + dbg_dy * dbg_dy);
//			GUI.Label(new Rect(x + 10, y + 50, 380, 20), $"Raw mag: {rawMag:F4}");

//			GUI.Label(new Rect(x + 10, y + 70, 380, 20), $"Normalized mag: {rawMag * MouseDeltaNormalizer.GetCurrentScale():F4}");

//			float avg = dbg_samples > 0 ? dbg_accum / dbg_samples : 0f;
//			GUI.Label(new Rect(x + 10, y + 90, 380, 20), $"Avg (normalized): {avg:F4}");
//			GUI.Label(new Rect(x + 10, y + 110, 380, 20), $"Peak (normalized): {dbg_peak:F4}");

//			// Status
//			if (MouseDeltaNormalizer.IsCalibrating())
//			{
//				GUI.Label(new Rect(x + 10, y + 140, 380, 20), $"CALIBRATION PHASE — Wiggle mouse! ({MouseDeltaNormalizer.GetTimeLeft():F1}s left)");
//				GUI.Label(new Rect(x + 10, y + 160, 380, 20), $"Highest raw mag so far: {MouseDeltaNormalizer.GetCalibMaxRawMag():F2}");
//			}
//			else
//			{
//				GUI.Label(new Rect(x + 10, y + 140, 380, 20), $"Calibrated — Scale factor: {MouseDeltaNormalizer.GetCurrentScale():F4}");
//				GUI.Label(new Rect(x + 10, y + 160, 380, 20), $"Calibration peak used: {MouseDeltaNormalizer.GetCalibMaxRawMag():F2}");
//				if (!MouseDeltaNormalizer.HasCalibrationData())
//					GUI.Label(new Rect(x + 10, y + 180, 380, 20), "No movement → raw values used");
//			}

//			if (GUI.Button(new Rect(x + 10, y + 200, 180, 30), "Restart Calibration (5s)"))
//			{
//				MouseDeltaNormalizer.TriggerManualCalibration();
//				ResetStats();
//			}

//			// Graph
//			float graphX = x + 10;
//			float graphY = y + 240;
//			float graphW = 280;
//			float graphH = 60;

//			GUI.Box(new Rect(graphX, graphY, graphW, graphH), "Normalized History");

//			for (int i = 0; i < DBG_HISTORY; i++)
//			{
//				int idx = (dbg_index + i) % DBG_HISTORY;
//				float v = dbg_history[idx];
//				float px = graphX + (i / (float)DBG_HISTORY) * graphW;
//				float h = Mathf.Clamp(v * graphScale, 0, graphH);
//				GUI.DrawTexture(new Rect(px, graphY + graphH - h, 2, h), Texture2D.whiteTexture);
//			}
//		}

//		public void ResetStats()
//		{
//			dbg_peak = 0f;
//			dbg_accum = 0f;
//			dbg_samples = 0;
//			for (int i = 0; i < DBG_HISTORY; i++)
//				dbg_history[i] = 0f;
//		}
//	}
//}
