using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private string selectedDefinitionId = "tile_empty";
		public string SelectedDefinitionID => selectedDefinitionId;
		private List<string> definitionCycleList = new();
		private int cycleIndex = 0;

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override void Update()
		{
			base.Update();
			if (!camera || editorController.GetEditorUI().IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;//store mouse down position

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(selectedDefinitionId);//place a tile

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile();//remove a tile - null ID to remove

			var selectedDefinition = ResourceManager.GetDefinition(selectedDefinitionId);
			if (null != selectedDefinition)
				EditorUtil.UpdateGhostTile(camera, editorController.iMapManager, selectedDefinition);
		}

		private void EditMapTile(string defID = null)
		{
			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);

			if (null != defID)
			{
				var mapIndex = editorController.iMapManager.WorldToMapIndex(worldPos);
				if (-1 != mapIndex)
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
			//var resized = editorController.iMapManager.UpdateTileAt((int)Mathf.Floor(snappedPos.x), (int)Mathf.Floor(snappedPos.z), defID);
			//editorController.OnMapChanged(resized);

			// Clean, direct, future-proof
			editorController.iMapManager.UpdateTileAt(
				(int)Mathf.Floor(snappedPos.x),
				(int)Mathf.Floor(snappedPos.z),
				defID,
				expand: true,
				onEdited: editorController.OnMapChanged // now receives (resized, originDelta)
			);
		}

		public void SetSelectedDefinitionById(string id)
		{
			selectedDefinitionId = id;
			if (null == id)
			{
				Debug.LogError("null definition in EditorControllerPaint::SetSelectedDefinition");
				return;
			}

			definitionCycleList = ResourceManager.DefinitionNavGroup(selectedDefinitionId);
			cycleIndex = definitionCycleList.IndexOf(selectedDefinitionId);

			EditorUtil.DestroyGhostTile();
			var selectedDefinition = ResourceManager.GetDefinition(selectedDefinitionId);
			if (null != selectedDefinition)
				EditorUtil.UpdateGhostTile(camera, editorController.iMapManager, selectedDefinition);
			else
				EditorUtil.HideGhostTile();
		}
	}
}