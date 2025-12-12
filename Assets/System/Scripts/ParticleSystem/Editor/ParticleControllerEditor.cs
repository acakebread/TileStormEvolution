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
		private DragState _dragState = DragState.None;
		private int _draggedPulseIndex = -1;
		private const float MIN_PULSE_WIDTH = 0.01f;
		private const float HANDLE_WIDTH_NORM = 0.08f;   // visual handle size in normalized space

		public override void OnInspectorGUI()
		{
			// === RESTORE MISSING SCRIPT FIELD ===
			var script = MonoScript.FromMonoBehaviour((ParticleController)target);
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.Space();

			var controller = (ParticleController)target;
			serializedObject.Update();

			// ----- Debug -------------------------------------------------
			EditorGUILayout.PropertyField(serializedObject.FindProperty("showInSceneView"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("useDebugMaterial"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("updateParticles"));

			// ----- Lifetime -----------------------------------------------
			EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetime"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetimeVariation"));

			// ----- Appearance ---------------------------------------------
			EditorGUILayout.PropertyField(serializedObject.FindProperty("useThreeZoneSlicing"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleMaterial"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeStartTime"));

			// ----- Physics ------------------------------------------------
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enablePhysics"));
			EditorGUI.BeginDisabledGroup(!controller.enablePhysics);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("gravity"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("airFriction"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityBias"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityMagnitude"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCollision"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("groundHeight"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("bounceFriction"));
			EditorGUI.EndDisabledGroup();

			// ----- Floater Behaviour --------------------------------------
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Floater Behaviour", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enableFloater"));
			EditorGUI.BeginDisabledGroup(!controller.enableFloater);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("floaterDriftAmplitude"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("floaterDriftFrequency"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("floaterSpatialScale"));
			EditorGUI.EndDisabledGroup();

			// ----- Animation ----------------------------------------------
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Scale Curve (%):", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("(Y-axis: 0 = 0%, 1 = 100%, 2 = 200%)");

			var curveProp = serializedObject.FindProperty("scaleCurve");
			var curve = curveProp.animationCurveValue;

			// 100 px tall curve editor
			Rect curveRect = EditorGUILayout.GetControlRect(false, 100);
			AnimationCurve newCurve = EditorGUI.CurveField(curveRect, curve, Color.green, new Rect(0, 0, 1, 2));

			// Preset buttons
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Linear (100%)")) newCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
			if (GUILayout.Button("Scale Up (0% to 200%)")) newCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 2f);
			if (GUILayout.Button("Scale Down (200% to 0%)")) newCurve = CreateScaleDownCurve();
			EditorGUILayout.EndHorizontal();

			// Clamp / enforce start-end keys (same logic as old version)
			if (!CurvesRoughlyEqual(curve, newCurve))
			{
				var temp = new AnimationCurve(newCurve.keys);
				if (temp.keys.Length < 2) temp = AnimationCurve.Linear(0f, 1f, 1f, 1f);
				else
				{
					for (int i = 0; i < temp.keys.Length; i++)
					{
						var k = temp.keys[i];
						k.time = Mathf.Clamp01(k.time);
						k.value = Mathf.Clamp(k.value, 0f, 2f);
						temp.MoveKey(i, k);
					}
					bool hasStart = temp.keys.Any(k => Mathf.Approximately(k.time, 0f));
					bool hasEnd = temp.keys.Any(k => Mathf.Approximately(k.time, 1f));
					if (!hasStart) temp.AddKey(0f, Mathf.Clamp(temp.Evaluate(0f), 0f, 2f));
					if (!hasEnd) temp.AddKey(1f, Mathf.Clamp(temp.Evaluate(1f), 0f, 2f));
				}
				// Force free tangents
				for (int i = 0; i < temp.keys.Length; i++)
				{
					AnimationUtility.SetKeyBroken(temp, i, true);
					AnimationUtility.SetKeyLeftTangentMode(temp, i, AnimationUtility.TangentMode.Free);
					AnimationUtility.SetKeyRightTangentMode(temp, i, AnimationUtility.TangentMode.Free);
				}
				newCurve = temp;
			}
			curveProp.animationCurveValue = newCurve;

			// ----- PWM / Emission -----------------------------------------
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleCount"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("scatterScalar"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cycleTime"));

			var pulsesProp = serializedObject.FindProperty("pulses");
			DrawPulseTimeline(controller, pulsesProp);

			// ----- Presets ------------------------------------------------
			EditorGUILayout.Space();
			if (GUILayout.Button("Apply Preset: Burst")) ApplyBurstPreset(controller);
			if (GUILayout.Button("Apply Preset: Continuous")) ApplyContinuousPreset(controller);

			// ----- Bake ---------------------------------------------------
			if (GUILayout.Button("Rebake Scale Table"))
				controller.RebakeScaleTable();

			// Auto-rebake on any change
			if (GUI.changed)
			{
				controller.RebakeScaleTable();
				EditorUtility.SetDirty(controller);
			}

			serializedObject.ApplyModifiedProperties();
		}

		// --------------------------------------------------------------------
		private void DrawPulseTimeline(ParticleController controller, SerializedProperty pulsesProp)
		{
			const float timelineHeight = 40f;
			Rect timelineRect = EditorGUILayout.GetControlRect(false, timelineHeight);
			EditorGUI.DrawRect(timelineRect, new Color(0.2f, 0.2f, 0.2f));

			float width = timelineRect.width;
			float cycleTime = controller.cycleTime;

			// ---- draw existing pulses ---------------------------------------
			for (int i = 0; i < pulsesProp.arraySize; i++)
			{
				var p = pulsesProp.GetArrayElementAtIndex(i);
				var start = p.FindPropertyRelative("start").floatValue;
				var end = p.FindPropertyRelative("end").floatValue;

				// enforce minimum width
				if (end < start + MIN_PULSE_WIDTH) end = start + MIN_PULSE_WIDTH;

				float x0 = timelineRect.x + start * width;
				float x1 = timelineRect.x + end * width;

				Color fill = new Color(0f, 0.8f, 0f, 0.4f);
				EditorGUI.DrawRect(new Rect(x0, timelineRect.y, x1 - x0, timelineRect.height), fill);
			}

			// ---- mouse handling ---------------------------------------------
			Event e = Event.current;
			Vector2 mouse = e.mousePosition;
			float norm = Mathf.Clamp01((mouse.x - timelineRect.x) / width);

			if (e.type == EventType.MouseDown && timelineRect.Contains(mouse))
			{
				// find nearest handle
				int closestIdx = -1;
				bool isStart = false;
				float bestDist = float.MaxValue;

				for (int i = 0; i < pulsesProp.arraySize; i++)
				{
					var p = pulsesProp.GetArrayElementAtIndex(i);
					float s = p.FindPropertyRelative("start").floatValue;
					float en = p.FindPropertyRelative("end").floatValue;

					float dStart = Mathf.Abs(norm - s);
					float dEnd = Mathf.Abs(norm - en);

					if (dStart < bestDist && dStart <= HANDLE_WIDTH_NORM) { bestDist = dStart; closestIdx = i; isStart = true; }
					if (dEnd < bestDist && dEnd <= HANDLE_WIDTH_NORM) { bestDist = dEnd; closestIdx = i; isStart = false; }
				}

				if (closestIdx >= 0)
				{
					_draggedPulseIndex = closestIdx;
					_dragState = isStart ? DragState.Start : DragState.End;
				}
				else
				{
					// create new pulse centered on click
					pulsesProp.arraySize++;
					var np = pulsesProp.GetArrayElementAtIndex(pulsesProp.arraySize - 1);
					float w = 0.1f;
					np.FindPropertyRelative("start").floatValue = Mathf.Clamp01(norm - w * 0.5f);
					np.FindPropertyRelative("end").floatValue = Mathf.Clamp01(norm + w * 0.5f);
				}
				e.Use();
			}
			else if (e.type == EventType.MouseDrag && _dragState != DragState.None && _draggedPulseIndex >= 0)
			{
				var p = pulsesProp.GetArrayElementAtIndex(_draggedPulseIndex);
				var startProp = p.FindPropertyRelative("start");
				var endProp = p.FindPropertyRelative("end");

				if (_dragState == DragState.Start)
				{
					startProp.floatValue = Mathf.Clamp01(norm);
					if (startProp.floatValue > endProp.floatValue - MIN_PULSE_WIDTH)
						endProp.floatValue = startProp.floatValue + MIN_PULSE_WIDTH;
				}
				else
				{
					endProp.floatValue = Mathf.Clamp01(norm);
					if (endProp.floatValue < startProp.floatValue + MIN_PULSE_WIDTH)
						startProp.floatValue = endProp.floatValue - MIN_PULSE_WIDTH;
				}
				e.Use();
			}
			else if (e.type == EventType.MouseUp && _dragState != DragState.None)
			{
				// ----- MERGE OVERLAPPING PULSES -----
				var list = new List<(float s, float e, int idx)>();
				for (int i = 0; i < pulsesProp.arraySize; i++)
				{
					var p = pulsesProp.GetArrayElementAtIndex(i);
					list.Add((p.FindPropertyRelative("start").floatValue,
							  p.FindPropertyRelative("end").floatValue, i));
				}
				list.Sort((a, b) => a.s.CompareTo(b.s));

				var merged = new List<(float s, float e)>();
				float curS = list[0].s, curE = list[0].e;
				for (int i = 1; i < list.Count; i++)
				{
					if (list[i].s <= curE) curE = Mathf.Max(curE, list[i].e);
					else { merged.Add((curS, curE)); curS = list[i].s; curE = list[i].e; }
				}
				merged.Add((curS, curE));

				pulsesProp.arraySize = merged.Count;
				for (int i = 0; i < merged.Count; i++)
				{
					var p = pulsesProp.GetArrayElementAtIndex(i);
					p.FindPropertyRelative("start").floatValue = merged[i].s;
					p.FindPropertyRelative("end").floatValue = merged[i].e;
				}

				_dragState = DragState.None;
				_draggedPulseIndex = -1;
				e.Use();
			}
			else if (e.type == EventType.MouseUp)
			{
				_dragState = DragState.None;
				_draggedPulseIndex = -1;
			}

			// ---- add / remove buttons ---------------------------------------
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Pulse"))
			{
				pulsesProp.arraySize++;
				var np = pulsesProp.GetArrayElementAtIndex(pulsesProp.arraySize - 1);
				np.FindPropertyRelative("start").floatValue = 0f;
				np.FindPropertyRelative("end").floatValue = 0.1f;
			}
			if (pulsesProp.arraySize > 1 && GUILayout.Button("Remove Last Pulse"))
				pulsesProp.arraySize--;
			EditorGUILayout.EndHorizontal();

			// ---- per-pulse labels -------------------------------------------
			for (int i = 0; i < pulsesProp.arraySize; i++)
			{
				var p = pulsesProp.GetArrayElementAtIndex(i);
				float s = p.FindPropertyRelative("start").floatValue;
				float en = p.FindPropertyRelative("end").floatValue;
				EditorGUILayout.LabelField($"Pulse {i + 1}: Start = {s * cycleTime:F3}s, End = {en * cycleTime:F3}s");
			}
		}

		// --------------------------------------------------------------------
		private bool CurvesRoughlyEqual(AnimationCurve a, AnimationCurve b)
		{
			if (a.keys.Length != b.keys.Length) return false;
			for (int i = 0; i < a.keys.Length; i++)
			{
				var ka = a.keys[i];
				var kb = b.keys[i];
				if (!Mathf.Approximately(ka.time, kb.time) ||
					!Mathf.Approximately(ka.value, kb.value) ||
					!Mathf.Approximately(ka.inTangent, kb.inTangent) ||
					!Mathf.Approximately(ka.outTangent, kb.outTangent))
					return false;
			}
			return true;
		}

		private AnimationCurve CreateScaleDownCurve()
		{
			var c = new AnimationCurve();
			c.AddKey(new Keyframe(0f, 2f, -2f, -2f));
			c.AddKey(new Keyframe(1f, 0f, -2f, -2f));
			for (int i = 0; i < c.keys.Length; i++)
			{
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}

		// --------------------------------------------------------------------
		private void ApplyBurstPreset(ParticleController c)
		{
			c.particleCount = 32;
			c.lifetime = 1.5f;
			c.lifetimeVariation = 0.3f;
			c.radius = 0.03f;
			c.color = new Color(1f, 0.8f, 0.3f);
			c.scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 2f);
			c.cycleTime = 0.1f;
			c.pulses = new List<ParticleController.Pulse> { new ParticleController.Pulse { start = 0f, end = 0.1f } };
			c.enablePhysics = true;
			c.gravity = 15f;
			c.velocityMagnitude = new Vector3(3f, 8f, 3f);
			c.enableCollision = true;
			c.bounceFriction = 0.7f;
			c.enableFloater = true;
			c.floaterDriftAmplitude = 2f;
			c.floaterDriftFrequency = 0.25f;
			c.floaterSpatialScale = 0.08f;
			EditorUtility.SetDirty(c);
		}

		private void ApplyContinuousPreset(ParticleController c)
		{
			c.particleCount = 8;
			c.lifetime = 2f;
			c.lifetimeVariation = 1f;
			c.radius = 0.02f;
			c.color = Color.cyan;
			c.scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
			c.cycleTime = 0.05f;
			c.pulses = new List<ParticleController.Pulse> { new ParticleController.Pulse { start = 0f, end = 1f } };
			c.enablePhysics = false;
			c.enableFloater = true;
			c.floaterDriftAmplitude = 1.2f;
			c.floaterDriftFrequency = 0.4f;
			c.floaterSpatialScale = 0.12f;
			EditorUtility.SetDirty(c);
		}
	}
}
#endif