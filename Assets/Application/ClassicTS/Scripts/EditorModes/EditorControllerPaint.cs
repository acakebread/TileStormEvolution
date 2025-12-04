using MassiveHadronLtd;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEditor;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private string selectedDefinitionId = "tile_empty";
		public string SelectedDefinitionID => selectedDefinitionId;
		private List<string> definitionCycleList = new();
		private int cycleIndex = 0;

		private readonly GuiUtils.AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
		private Vector2 scrollPos = Vector2.zero;

		public override bool IsMouseOverModeGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Paint)
				return false;

			// Use the actual animated panel rect
			var panelRect = sidePanel.GetRect(20f, 20f);
			panelRect.x = Screen.width - panelRect.xMax + 20f; // because GetRect returns left-aligned
															   // Actually better: let AutoHidePanel expose screen-space rect properly, but for now:
			float panelWidth = sidePanel.CurrentWidth;
			var screenRect = new Rect(Screen.width - panelWidth - 20f, 20f, panelWidth, Screen.height - 40f);

			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return screenRect.Contains(mouse);
		}

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override void Update()
		{
			base.Update();
			if (!camera || editorController.IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(selectedDefinitionId);

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile();

			var selectedDefinition = ResourceManager.GetDefinition(selectedDefinitionId);
			if (selectedDefinition != null)
				EditorUtil.UpdateGhostTile(camera, editorController.iMapManager, selectedDefinition);
		}

		public override void OnDisable() => EditorUtil.HideGhostTile();

		private void EditMapTile(string defID = null)
		{
			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);

			if (defID != null)
			{
				var mapIndex = editorController.iMapManager.WorldToMapIndex(worldPos);
				if (mapIndex != -1)
				{
					var currentId = editorController.iMapManager.GetDefinitionAtIndex(mapIndex);
					if (currentId == selectedDefinitionId && definitionCycleList.Count > 1)
					{
						cycleIndex = (cycleIndex + 1) % definitionCycleList.Count;
						selectedDefinitionId = definitionCycleList[cycleIndex];
						EditorUtil.DestroyGhostTile();
						defID = selectedDefinitionId;
					}
				}
			}
			else
				defID = "tile_empty";

			var snappedPos = editorController.iMapManager.SnappedMapPosition(worldPos);

			editorController.iMapManager.UpdateTileAt(
				Mathf.FloorToInt(snappedPos.x),
				Mathf.FloorToInt(snappedPos.z),
				defID,
				expand: true,
				onEdited: editorController.OnMapChanged
			);
		}

		public void SetSelectedDefinitionById(string id)
		{
			selectedDefinitionId = id ?? "tile_empty";

			definitionCycleList = ResourceManager.DefinitionNavGroup(selectedDefinitionId);
			cycleIndex = definitionCycleList.IndexOf(selectedDefinitionId);

			EditorUtil.DestroyGhostTile();
			var def = ResourceManager.GetDefinition(selectedDefinitionId);
			if (def != null)
				EditorUtil.UpdateGhostTile(camera, editorController.iMapManager, def);
			else
				EditorUtil.HideGhostTile();
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Paint || camera == null) return;

			sidePanel.Update();

			// Optional: keep panel open while using it
			if (sidePanel.IsGuiActive())
				sidePanel.ForceExpand();

			Rect panelRect = sidePanel.GetRect(20f, 20f);

			// Draw background box
			GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
			GUI.Box(panelRect, "Tile Selector", EditorStyles.toolbarButton);
			GUI.backgroundColor = Color.white;

			GUILayout.BeginArea(panelRect);
			GUILayout.BeginVertical();

			GUILayout.Label("Tiles", EditorStyles.boldLabel);

			float scrollBarWidth = 12f;
			Rect scrollRect = new Rect(0f, 25f, panelRect.width, panelRect.height - 25f);

			// Compute total content height explicitly
			int count = ResourceManager.Definitions.Count;
			float buttonHeight = 36f;
			float contentHeight = count * (buttonHeight + 4f);

			// LOCKED CONTENT WIDTH = VIEW WIDTH (THIS IS THE KEY)
			Rect contentRect = new Rect(0f, 0f, scrollRect.width - scrollBarWidth - 10, contentHeight);

			// Actual GUI scroll view (NOT GUILayout)
			scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, contentRect, false, true);

			float y = 0f;

			for (int i = 0; i < count; i++)
			{
				var def = ResourceManager.Definitions.ElementAt(i);
				string label = $"{def.id} ({def.texture})";

				GUI.backgroundColor = (def.id == selectedDefinitionId) ? new Color(0.3f, 0.8f, 1f, 0.9f) : Color.white;

				if (GUI.Button( new Rect(0f, y, contentRect.width, buttonHeight), label))
					SetSelectedDefinitionById(def.id);

				y += buttonHeight + 4f;
			}

			GUI.backgroundColor = Color.white;

			GUI.EndScrollView();

			// ---------------------------------------------------------------

			GUILayout.EndVertical();
			GUILayout.EndArea();
		}
	}
}