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
		private float cachedContentHeight = -1f;
		private int cachedCount = -1;

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override void Update()
		{
			base.Update();
			if (!camera || editorController.GetEditorUI().IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

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
			Rect panel = sidePanel.GetRect(20f, 20f);

			GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
			GUI.Box(panel, "Tile Selector");
			GUI.backgroundColor = Color.white;

			GUILayout.BeginArea(panel);
			GUILayout.BeginVertical();
			GUILayout.Label("Tiles", EditorStyles.boldLabel);

			int count = ResourceManager.Definitions.Count;
			if (cachedCount != count)
			{
				cachedCount = count;
				cachedContentHeight = count * 40f;
			}

			scrollPos = GUILayout.BeginScrollView(scrollPos);

			for (int i = 0; i < count; i++)
			{
				var def = ResourceManager.Definitions.ElementAt(i);
				string label = $"{def.id} ({def.texture})";

				GUILayout.BeginHorizontal();

				GUI.backgroundColor = (def.id == selectedDefinitionId)
					? new Color(0.3f, 0.8f, 1f, 0.9f)
					: Color.white;

				if (GUILayout.Button(label, GUILayout.Height(36)))
					SetSelectedDefinitionById(def.id);

				GUI.backgroundColor = Color.white;
				GUILayout.EndHorizontal();
			}

			GUILayout.EndScrollView();
			GUILayout.EndVertical();
			GUILayout.EndArea();
		}
	}
}