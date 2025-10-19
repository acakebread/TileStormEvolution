using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraEditor : CameraBase
	{
		public MapManager mapManager;
		private GameCameraEditorMovement activeMode;
		private GameCameraEditorDrag dragMode;
		private GameCameraEditorPaint paintMode;
		private enum EditorMode { Drag, Paint }
		private EditorMode currentMode = EditorMode.Drag;
		private int selectedMapDefIndex = 0; // Index into mapDefs
		private bool showTileSelector = false;
		private Vector2 scrollPosition = Vector2.zero;
		private int tempSelectedTileDefGlobalIndex = 0; // Index into DatabaseSerializer.TileDefs

		public GameCameraEditor(Camera camera) : base(camera) { }

		public override void Awake()
		{
			base.Awake();

			var cameraTransform = camera.transform;
			cameraTransform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);

			// Initialize mode instances
			dragMode = new GameCameraEditorDrag(camera);
			paintMode = new GameCameraEditorPaint(camera, mapManager, selectedMapDefIndex);
			activeMode = dragMode;
		}

		public override void Start()
		{
			base.Start();
			camera.fieldOfView = 60f;
			postProcessingEnabled = false;
			dragMode.Initialize();
			paintMode.Initialize();

			if (!Object.FindAnyObjectByType<EventSystem>())
				new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		}

		public override void Update()
		{
			base.Update();

			// Workaround: Reset hotControl on mouse release to handle drag outside GUI
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
			{
				GUIUtility.hotControl = 0;
				Debug.Log("Manually reset hotControl on mouse release");
			}

			if (activeMode != null)
				activeMode.Update();
		}

		public override void OnApplicationFocus(bool hasFocus)
		{
			base.OnApplicationFocus(hasFocus);
			if (activeMode != null)
				activeMode.OnApplicationFocus(hasFocus);
		}

		protected override void OnRender()
		{
			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
		}

		public override void OnGUI()
		{
			base.OnGUI();

			float buttonWidth = 120;
			float buttonHeight = 30;
			float margin = 10;

			// Radio buttons for mode selection
			Rect groupRect = new Rect(margin, Screen.height - buttonHeight * 3 - margin * 2, buttonWidth, buttonHeight * 2);
			GUI.BeginGroup(groupRect);
			bool dragToggled = GUI.Toggle(new Rect(0, 0, buttonWidth, buttonHeight), currentMode == EditorMode.Drag, "Drag", "Button");
			bool paintToggled = GUI.Toggle(new Rect(0, buttonHeight, buttonWidth, buttonHeight), currentMode == EditorMode.Paint, "Paint", "Button");

			// Ensure radio button behavior
			if (dragToggled && currentMode != EditorMode.Drag)
			{
				currentMode = EditorMode.Drag;
				activeMode = dragMode;
				Debug.Log("Switched to Drag mode");
			}
			else if (paintToggled && currentMode != EditorMode.Paint)
			{
				currentMode = EditorMode.Paint;
				activeMode = paintMode;
				Debug.Log("Switched to Paint mode");
			}
			GUI.EndGroup();

			// Button to open tile selector
			if (GUI.Button(new Rect(margin + buttonWidth + 10, Screen.height - buttonHeight * 3 - margin * 2, buttonWidth, buttonHeight), "Select Tile"))
			{
				showTileSelector = true;
				// Initialize to a valid TileDef index
				tempSelectedTileDefGlobalIndex = 0;
			}

			// Tile selector popup
			if (showTileSelector)
			{
				float popupWidth = 300;
				float popupHeight = 400;
				float popupX = Screen.width / 2 - popupWidth / 2;
				float popupY = Screen.height / 2 - popupHeight / 2;

				GUI.Box(new Rect(popupX, popupY, popupWidth, popupHeight), "Select Tile Type");

				float scrollViewHeight = popupHeight - 100;
				scrollPosition = GUI.BeginScrollView(
					new Rect(popupX + 10, popupY + 30, popupWidth - 20, scrollViewHeight),
					scrollPosition,
					new Rect(0, 0, popupWidth - 40, DatabaseSerializer.TileDefs.Count * 30)
				);

				for (int i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
				{
					var tileDef = DatabaseSerializer.TileDefs[i];
					string displayName = $"{tileDef.szType} ({tileDef.szTheme})";
					Rect buttonRect = new Rect(0, i * 30, popupWidth - 40, 25);

					if (i == tempSelectedTileDefGlobalIndex)
					{
						GUI.color = Color.yellow;
						GUI.Box(new Rect(buttonRect.x - 5, buttonRect.y - 2, buttonRect.width + 10, buttonRect.height + 4), "");
						GUI.color = Color.white;
					}

					if (GUI.Button(buttonRect, displayName))
					{
						tempSelectedTileDefGlobalIndex = i;
					}
				}

				GUI.EndScrollView();

				// OK and Cancel buttons
				if (GUI.Button(new Rect(popupX + 10, popupY + popupHeight - 60, buttonWidth, buttonHeight), "OK"))
				{
					var tileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
					selectedMapDefIndex = mapManager.GetOrAddMapDefIndex(tileDef.szType, tileDef.szTheme);
					if (selectedMapDefIndex >= 0)
					{
						paintMode.SetTileDefIndex(selectedMapDefIndex);
						Debug.Log($"Selected tileDef: {tileDef.szType} ({tileDef.szTheme}), mapped to mapDefs index={selectedMapDefIndex}");
					}
					showTileSelector = false;
				}

				if (GUI.Button(new Rect(popupX + popupWidth - buttonWidth - 10, popupY + popupHeight - 60, buttonWidth, buttonHeight), "Cancel"))
				{
					showTileSelector = false;
				}
			}
		}
	}
}