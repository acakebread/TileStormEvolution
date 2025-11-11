#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	[CustomEditor(typeof(ParticleController))]
	public class ParticleControllerEditor : Editor
	{
		private enum DragState { None, Start, End }
		private DragState dragState = DragState.None;
		private int draggedPulseIndex = -1;
		private const float MIN_PULSE_WIDTH = 0.01f;

		private static bool IsGameViewMaximized()
		{
#if UNITY_EDITOR
			var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
			return gameView != null && gameView.maximized;
#else
            return false;
#endif
		}

		public override void OnInspectorGUI()
		{
			var controller = (ParticleController)target;
			serializedObject.Update();

			// ----- Debug -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("showInSceneView"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("useDebugMaterial"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("updateParticles"));

			// ----- Lifetime -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetime"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetimeVariation"));

			// ----- Appearance -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("useThreeZoneSlicing"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleMaterial"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeStartTime"));

			// ----- Physics -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enablePhysics"));
			EditorGUI.BeginDisabledGroup(!controller.enablePhysics);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("gravity"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("friction"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityBias"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityMagnitude"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCollision"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("groundHeight"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("bounceDamping"));
			EditorGUI.EndDisabledGroup();

			// ----- Floater Behaviour -----
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Floater Behaviour", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enableFloater"));
			EditorGUI.BeginDisabledGroup(!controller.enableFloater);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("floaterDriftAmplitude"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("floaterDriftFrequency"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("floaterSpatialScale"));
			EditorGUI.EndDisabledGroup();

			// ----- Animation -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleCurve"));

			// ----- PWM / Emission -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleCount"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("scatterScalar"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cycleTime"));

			var pulsesProp = serializedObject.FindProperty("pulses");
			EditorGUILayout.PropertyField(pulsesProp, true);

			if (pulsesProp.isExpanded && pulsesProp.arraySize > 0)
			{
				DrawPulseTimeline(controller);
			}

			if (GUILayout.Button("Apply Preset: Burst"))
			{
				ApplyBurstPreset(controller);
			}
			if (GUILayout.Button("Apply Preset: Continuous"))
			{
				ApplyContinuousPreset(controller);
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawPulseTimeline(ParticleController controller)
		{
			Rect rect = GUILayoutUtility.GetRect(0, 40);
			rect.x += 18;
			rect.width -= 36;

			float cycleTime = controller.cycleTime;
			Handles.color = Color.gray;
			Handles.DrawLine(new Vector3(rect.x, rect.y + 20), new Vector3(rect.xMax, rect.y + 20));

			GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
			for (int i = 0; i <= 10; i++)
			{
				float t = i / 10f;
				float x = rect.x + t * rect.width;
				Handles.DrawLine(new Vector3(x, rect.y + 18), new Vector3(x, rect.y + 22));
				if (i % 2 == 0)
					EditorGUI.LabelField(new Rect(x - 10, rect.y + 24, 20, 16), (t * cycleTime).ToString("0.0"), labelStyle);
			}

			for (int i = 0; i < controller.pulses.Count; i++)
			{
				var pulse = controller.pulses[i];
				float startX = rect.x + pulse.start * rect.width;
				float endX = rect.x + pulse.end * rect.width;

				Color fill = new Color(0.2f, 0.8f, 0.2f, 0.3f);
				Color outline = new Color(0.2f, 0.8f, 0.2f, 1f);
				EditorGUI.DrawRect(new Rect(startX, rect.y + 5, endX - startX, 30), fill);
				Handles.color = outline;
				Handles.DrawLine(new Vector3(startX, rect.y + 5), new Vector3(startX, rect.y + 35));
				Handles.DrawLine(new Vector3(endX, rect.y + 5), new Vector3(endX, rect.y + 35));

				EditorGUIUtility.AddCursorRect(new Rect(startX - 5, rect.y, 10, 40), MouseCursor.ResizeHorizontal);
				EditorGUIUtility.AddCursorRect(new Rect(endX - 5, rect.y, 10, 40), MouseCursor.ResizeHorizontal);

				if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
				{
					if (Mathf.Abs(Event.current.mousePosition.x - startX) < 10)
					{
						dragState = DragState.Start;
						draggedPulseIndex = i;
					}
					else if (Mathf.Abs(Event.current.mousePosition.x - endX) < 10)
					{
						dragState = DragState.End;
						draggedPulseIndex = i;
					}
					Event.current.Use();
				}
			}

			if (dragState != DragState.None && Event.current.type == EventType.MouseDrag)
			{
				float norm = (Event.current.mousePosition.x - rect.x) / rect.width;
				norm = Mathf.Clamp01(norm);
				var pulse = controller.pulses[draggedPulseIndex];
				if (dragState == DragState.Start)
				{
					pulse.start = Mathf.Min(norm, pulse.end - MIN_PULSE_WIDTH);
				}
				else
				{
					pulse.end = Mathf.Max(norm, pulse.start + MIN_PULSE_WIDTH);
				}
				EditorUtility.SetDirty(controller);
				Event.current.Use();
			}

			if (Event.current.type == EventType.MouseUp)
			{
				dragState = DragState.None;
				draggedPulseIndex = -1;
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("+ Add Pulse"))
			{
				controller.pulses.Add(new ParticleController.Pulse { start = 0.2f, end = 0.3f });
				EditorUtility.SetDirty(controller);
			}
			if (controller.pulses.Count > 1 && GUILayout.Button("- Remove Last"))
			{
				controller.pulses.RemoveAt(controller.pulses.Count - 1);
				EditorUtility.SetDirty(controller);
			}
			EditorGUILayout.EndHorizontal();
		}

		private void ApplyBurstPreset(ParticleController controller)
		{
			controller.particleCount = 32;
			controller.lifetime = 1.5f;
			controller.lifetimeVariation = 0.3f;
			controller.radius = 0.03f;
			controller.color = new Color(1f, 0.8f, 0.3f);
			controller.scaleCurve = CreateScaleUpCurve();
			controller.cycleTime = 0.1f;
			controller.pulses = new List<ParticleController.Pulse>
			{
				new ParticleController.Pulse { start = 0f, end = 0.1f }
			};
			controller.enablePhysics = true;
			controller.gravity = 15f;
			controller.velocityMagnitude = new Vector3(3f, 8f, 3f);
			controller.enableCollision = true;
			controller.bounceDamping = 0.7f;
			controller.enableFloater = true;
			controller.floaterDriftAmplitude = 2f;
			controller.floaterDriftFrequency = 0.25f;
			controller.floaterSpatialScale = 0.08f;
			EditorUtility.SetDirty(controller);
		}

		private void ApplyContinuousPreset(ParticleController controller)
		{
			controller.particleCount = 8;
			controller.lifetime = 2f;
			controller.lifetimeVariation = 1f;
			controller.radius = 0.02f;
			controller.color = Color.cyan;
			controller.scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
			controller.cycleTime = 0.05f;
			controller.pulses = new List<ParticleController.Pulse>
			{
				new ParticleController.Pulse { start = 0f, end = 1f }
			};
			controller.enablePhysics = false;
			controller.enableFloater = true;
			controller.floaterDriftAmplitude = 1.2f;
			controller.floaterDriftFrequency = 0.4f;
			controller.floaterSpatialScale = 0.12f;
			EditorUtility.SetDirty(controller);
		}

		private bool CurvesEqual(AnimationCurve a, AnimationCurve b)
		{
			if (a.keys.Length != b.keys.Length) return false;
			for (int i = 0; i < a.keys.Length; i++)
			{
				var ka = a.keys[i];
				var kb = b.keys[i];
				if (ka.time != kb.time || ka.value != kb.value ||
					ka.inTangent != kb.inTangent || ka.outTangent != kb.outTangent)
					return false;
			}
			return true;
		}

		private AnimationCurve CreateLinearCurve()
		{
			var c = new AnimationCurve();
			float[] times = { 0f, 0.33f, 0.66f, 1f };
			for (int i = 0; i < times.Length; i++)
			{
				var k = new Keyframe(times[i], 1f, 0f, 0f);
				c.AddKey(k);
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}

		private AnimationCurve CreateScaleUpCurve()
		{
			var c = new AnimationCurve();
			float[] times = { 0f, 0.33f, 0.66f, 1f };
			float[] vals = { 0f, 0.66f, 1.33f, 2f };
			float slope = 2f;
			for (int i = 0; i < times.Length; i++)
			{
				var k = new Keyframe(times[i], vals[i], slope, slope);
				c.AddKey(k);
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}

		private AnimationCurve CreateScaleDownCurve()
		{
			var c = new AnimationCurve();
			float[] times = { 0f, 0.33f, 0.66f, 1f };
			float[] vals = { 2f, 1.33f, 0.66f, 0f };
			float slope = -2f;
			for (int i = 0; i < times.Length; i++)
			{
				var k = new Keyframe(times[i], vals[i], slope, slope);
				c.AddKey(k);
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}
	}
}
#endif