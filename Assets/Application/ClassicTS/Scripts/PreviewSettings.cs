using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClassicTilestorm
{
	public enum PreviewMode
	{
		Player,
		Cinema,
		Editor
	}

	public class PreviewSettings : MonoBehaviour
	{
		[Header("map to load")]
		[SerializeField] private string loadMapName = "Industrial 01";
		public static string LoadMapName { get => instance.loadMapName; set => instance.loadMapName = value; }

		[Header("load map scrambled or solved")]
		[SerializeField] private bool scrambled = true;
		public static bool Scrambled => instance.scrambled;

		[Header("enable or disable easy mode")]
		[SerializeField] private bool difficulty = false;
		public static bool Difficulty => instance.difficulty;

		[Header("hidden tiles")]
		[SerializeField] private bool showHiddenTiles = false;
		public static bool ShowHiddenTiles => instance.showHiddenTiles;

		[Header("tile selection")]
		[SerializeField] private bool showTileSelection = false;
		public static bool ShowTileSelection => instance.showTileSelection;

		[Header("resource paths")]
		[SerializeField] private TextAsset databaseJsonFile;
		public static TextAsset DatabaseJsonFile => instance.databaseJsonFile;

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance.geometryPath;

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance.texturePath;

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => instance.skycubesPath;

		[Header("Game Mode")]
		[SerializeField] private PreviewMode previewMode = PreviewMode.Player;

		public static PreviewMode CurrentMode
		{
			get => instance != null ? instance.previewMode : PreviewMode.Player;
			set
			{
				if (instance != null)
				{
					instance.previewMode = value;
				}
			}
		}

		private static PreviewSettings instance;
		void Awake() => instance = this;

#if UNITY_EDITOR
		[CustomEditor(typeof(PreviewSettings))]
		public class PreviewSettingsEditor : Editor
		{
			public override void OnInspectorGUI()
			{
				serializedObject.Update();

				// Draw all properties except the previewMode
				SerializedProperty prop = serializedObject.GetIterator();
				bool enterChildren = true;
				while (prop.NextVisible(enterChildren))
				{
					if (prop.name == "previewMode")
					{
						continue; // Skip the default enum field
					}

					EditorGUILayout.PropertyField(prop, true);
				}

				// Custom radio buttons for previewMode
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Game Mode", EditorStyles.boldLabel);
				SerializedProperty modeProp = serializedObject.FindProperty("previewMode");
				int selectedIndex = (int)modeProp.enumValueIndex;
				string[] modeNames = { "Player Mode", "Cinema Mode", "Editor Mode" };
				selectedIndex = GUILayout.SelectionGrid(selectedIndex, modeNames, 3, EditorStyles.radioButton);
				modeProp.enumValueIndex = (int)selectedIndex;

				serializedObject.ApplyModifiedProperties();
			}
		}
#endif
	}
}