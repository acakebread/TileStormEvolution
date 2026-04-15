using UnityEngine;
using UnityEditor;

namespace MassiveHadronLtd
{
	[CustomEditor(typeof(ReflectionEffectCamera))]
	public class ReflectionEffectCameraEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("Plane Settings", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("planeNormal"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("offset"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Reflection Effects", EditorStyles.boldLabel);

			var effectModeProp = serializedObject.FindProperty("effectMode");
			EditorGUILayout.PropertyField(effectModeProp);

			switch ((ReflectionEffectCamera.EffectMode)effectModeProp.enumValueIndex)
			{
				case ReflectionEffectCamera.EffectMode.PerfectMirror:
					EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorTint"), new GUIContent("Mirror Tint"));
					break;

				case ReflectionEffectCamera.EffectMode.SurfaceFilm:
					EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorTint"), new GUIContent("Mirror Tint / Dim Color"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseScale"));

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Fresnel Reflection", EditorStyles.boldLabel);
					EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionStrength"), new GUIContent("Reflection Strength"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("fresnelSharpness"),
						new GUIContent("Fresnel Sharpness", "Higher = reflection only at extreme grazing angles\nLower = broader reflection"));
					break;

				case ReflectionEffectCamera.EffectMode.FrostEffect:
					EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorTint"), new GUIContent("Base Color"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseStrength"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("frostDepth"));

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Fresnel Reflection", EditorStyles.boldLabel);
					EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionStrength"), new GUIContent("Reflection Strength"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("fresnelSharpness"),
						new GUIContent("Fresnel Sharpness", "Higher = reflection only at extreme grazing angles\nLower = broader reflection"));
					break;

				case ReflectionEffectCamera.EffectMode.Water:
					EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorTint"), new GUIContent("Base Color"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleSpeed"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleAmplitude"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleFrequency"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleOffset"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionStrength"));
					EditorGUILayout.PropertyField(serializedObject.FindProperty("fresnelSharpness"),
						new GUIContent("Fresnel Sharpness", "Higher = reflection only at extreme grazing angles\nLower = broader reflection"));
					break;

				//case ReflectionEffectCamera.EffectMode.OceanEffect:
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorTint"), new GUIContent("Base Color"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleSpeed"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleAmplitude"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleFrequency"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleOffset"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseStrength"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("frostDepth"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("frostThreshold"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("frostFadeRange"));

				//	EditorGUILayout.Space();
				//	EditorGUILayout.LabelField("Fresnel Reflection", EditorStyles.boldLabel);
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionStrength"), new GUIContent("Reflection Strength"));
				//	EditorGUILayout.PropertyField(serializedObject.FindProperty("fresnelSharpness"),
				//		new GUIContent("Fresnel Sharpness", "Higher = reflection only at extreme grazing angles\nLower = broader reflection"));
				//	break;

				default:
					EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorTint"));
					break;
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}