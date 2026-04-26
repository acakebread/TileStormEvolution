#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ClassicTilestorm
{
	public class PreviewSettingsEditorWindow : EditorWindow
	{
		private PreviewSettings instance;
		private Vector2 scrollPos;

		[MenuItem("Window/ClassicTilestorm/Preview Settings")]
		public static void ShowWindow()
		{
			GetWindow<PreviewSettingsEditorWindow>("Preview Settings");
		}

		void OnEnable()
		{
			instance = FindObjectOfType<PreviewSettings>();
			if (instance == null)
				Debug.LogWarning("No PreviewSettings found in scene. Add one to a GameObject.");
		}

		void OnGUI()
		{
			if (instance == null)
			{
				EditorGUILayout.HelpBox("No PreviewSettings instance in scene.", MessageType.Warning);
				if (GUILayout.Button("Find Again")) instance = FindObjectOfType<PreviewSettings>();
				return;
			}

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

			SerializedObject serialized = new SerializedObject(instance);
			SerializedProperty prop = serialized.GetIterator();
			bool enterChildren = true;
			while (prop.NextVisible(enterChildren))
			{
				enterChildren = false;
				if (prop.name == "m_Script") continue; // Skip script field

				EditorGUILayout.PropertyField(prop, true);
			}
			serialized.ApplyModifiedProperties();

			EditorGUILayout.EndScrollView();

			//if (GUILayout.Button("Reset to Defaults"))
			//{
			//	instance.LoadMapName = "Industrial 01";
			//	PreviewSettings.Scrambled = true;
			//	PreviewSettings.Difficulty = false;
			//	PreviewSettings.ShowHiddenTiles = false;
			//	PreviewSettings.ShowTileSelection = false;
			//	PreviewSettings.LaunchInCinemaMode = false;
			//	PreviewSettings.databaseJsonFile = null; // Note: Direct field access for reset
			//	PreviewSettings.GeometryPath = "ClassicTS/Geometry/";
			//	PreviewSettings.TexturePath = "ClassicTS/Textures/";
			//	PreviewSettings.SkycubesPath = "ClassicTS/SkyCubes/";
			//	PreviewSettings.DebugMode = false;
			//	EditorUtility.SetDirty(instance);
			//	AssetDatabase.SaveAssets();
			//}
		}
	}
}
#endif