using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ReflectionPassCamera))]
public class ReflectionPassCameraEditor : Editor
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
		var usePerfectMirrorProp = serializedObject.FindProperty("usePerfectMirror");
		var useSurfaceFilmProp = serializedObject.FindProperty("useSurfaceFilm");

		EditorGUILayout.PropertyField(usePerfectMirrorProp);

		if (!usePerfectMirrorProp.boolValue)
		{
			EditorGUILayout.PropertyField(useSurfaceFilmProp);

			if (useSurfaceFilmProp.boolValue)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("filmIntensity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseScale"));
			}
		}
		else
		{
			useSurfaceFilmProp.boolValue = false;
		}

		EditorGUILayout.Space();
		EditorGUILayout.PropertyField(serializedObject.FindProperty("customReflectionMaterial"));

		if (serializedObject.ApplyModifiedProperties())
		{
			// Force immediate update
			var targetScript = (ReflectionPassCamera)target;
			targetScript.OnValidate();
		}
	}
}