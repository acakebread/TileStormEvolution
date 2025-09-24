using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ReflectionEffectCamera))]
public class ReflectionEffectCameraEditor : Editor
{
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		// Plane settings
		EditorGUILayout.LabelField("Plane Settings", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(serializedObject.FindProperty("planeNormal"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("offset"));

		// Effect settings
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Reflection Effects", EditorStyles.boldLabel);
		var effectModeProp = serializedObject.FindProperty("effectMode");
		EditorGUILayout.PropertyField(effectModeProp);

		// Show properties based on the selected effect mode
		switch ((ReflectionEffectCamera.EffectMode)effectModeProp.enumValueIndex)
		{
			case ReflectionEffectCamera.EffectMode.PerfectMirror:
				// No additional properties for PerfectMirror
				break;

			case ReflectionEffectCamera.EffectMode.SurfaceFilm:
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("filmIntensity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseScale"));
				break;

			case ReflectionEffectCamera.EffectMode.FrostEffect:
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("frostRadius"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("baseColor"));
				break;
		}

		if (serializedObject.ApplyModifiedProperties())
		{
			var targetScript = (ReflectionEffectCamera)target;
			targetScript.OnValidate();
		}
	}
}