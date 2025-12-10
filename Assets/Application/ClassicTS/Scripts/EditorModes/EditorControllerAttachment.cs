using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;
using static ClassicTilestorm.EditorController;
using UnityEditor;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		public int SelectedAttachmentIndex { get; private set; } = -1;

		private int draggingTile = -1;
		private MapAttachment[] draggedAttachments = System.Array.Empty<MapAttachment>();

		private int pendingTile = -1;
		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		private Vector3 clickStartPos;
		private int clickStartTile = -1;

		private ViewPreview viewPreview;

		// RMB preview control
		private bool isControllingPreviewWithRMB = false;
		private bool rmbDragStartedInPreview = false; // THIS FIXES THE FREEZE BUG

		//private bool isMouseOverPreview = false;

		public override bool IsMouseOverGui()
		{
			// Check preview first
			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mp = Input.mousePosition;
				mp.y = Screen.height - mp.y;

				if (hitRect.Contains(mp))
					return true; // block main camera scroll AND mouse wheel
			}

			// Only relevant in Attachment mode
			if (editorController.CurrentMode != EditorMode.Attachment) return false;

			// Use the panel's actual rect
			Rect panelRect = sidePanel.GetPanelRect();
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;

			return panelRect.Contains(mouse);
		}


		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		private readonly AutoHidePanelV2 sidePanel = new AutoHidePanelV2(
			collapsed: 120f,
			expanded: 340f,
			delay: 1.5f,
			animDur: 0.25f,
			defaultPos: new Vector2(0f, 40f)
		);

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedAttachmentIndex = -1;
			EditorUtil.DestroyMarkerVisuals();
			EditorUtil.DestroyViewFrustumMarker();
			RebuildMarkers();

			viewPreview = ViewPreview.Create();
			viewPreview.Hide();

			sidePanel.List.Clear(); // clear old items
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorMode.Attachment || editorCamera == null) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			sidePanel.Update();

			// Draw panel background
			Rect panelRect = sidePanel.GetPanelRect();
			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.95f);
			GUI.Box(panelRect, "");
			GUI.backgroundColor = Color.white;

			GUILayout.BeginArea(panelRect);
			GUILayout.BeginVertical();

			// Populate ListView with attachments
			sidePanel.List.Clear();
			var attachments = map.attachments ?? System.Array.Empty<MapAttachment>();

			foreach (var att in attachments)
			{
				string extra = att is Emitter e && e.LookAt != null ? $" to {e.LookAt:F1}" : "";
				string label = $"{att.GetType().Name} [{att.tile}]{extra}";

				// Create a ListViewItem and add it
				sidePanel.List.AddItem(new ListViewItem(
					label, // label text
					() =>
					{
						int index = System.Array.IndexOf(map.attachments, att);
						SelectAttachment(index);
					},
					selected: System.Array.IndexOf(map.attachments, att) == SelectedAttachmentIndex // selected state
				));
			}

			// Draw the ListView
			sidePanel.List.Draw(new Rect(0, 0, panelRect.width, panelRect.height - 40f));

			// Draw footer text
			GUILayout.FlexibleSpace();
			GUILayout.Label("Hold RMB on preview to orbit • Scroll to zoom\nLMB: place/move • RMB on tile: delete",
				new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });

			GUILayout.EndVertical();
			GUILayout.EndArea();

			// Draw popups
			if (pendingAction == PendingAction.Add) DrawAddPopup();
			if (pendingAction == PendingAction.Delete) DrawDeletePopup();
		}


		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyMarkerVisuals();
			pendingAction = PendingAction.None;
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();

			viewPreview?.Hide();
			if (viewPreview != null) Object.Destroy(viewPreview.gameObject);

			isControllingPreviewWithRMB = false;
			rmbDragStartedInPreview = false;
		}

		public void OnMapChanged()
		{
			if (editorController.CurrentMode != EditorMode.Attachment) return;
			RebuildMarkers();
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();
		}

		private void RebuildMarkers()
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var tiles = map.attachments?
				.Where(a => a.tile >= 0)
				.Select(a => a.tile)
				.Distinct()
				.ToArray() ?? System.Array.Empty<int>();

			EditorUtil.UpdateMapMarkers(editorController.iMapManager, tiles, SelectedAttachmentIndex, EditorUtil.MarkerType.Attachment);
		}

		private void SelectAttachment(int index)
		{
			SelectedAttachmentIndex = index;
			RebuildMarkers();
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview.Hide();

			var map = editorController?.iMapManager?.CurrentMap;
			if (map?.attachments == null || index < 0 || index >= map.attachments.Length) return;

			if (map.attachments[index] is View view)
			{
				SnapViewDistanceToGround(view, editorController.iMapManager);
				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
				viewPreview.Show(view, editorController.iMapManager);
			}
		}

		// ONLY block main camera when RMB drag actually started inside preview
		protected override bool ShouldUseMainCameraThisFrame()
		{
			return !(isControllingPreviewWithRMB && rmbDragStartedInPreview);
		}

		public override void Update()
		{
			// Main camera movement (includes mouse wheel zoom)
			if (ShouldUseMainCameraThisFrame())
				base.Update();

			// Gizmo handling
			if (EditorTransformUtil.HandleTransformGizmoInput(editorCamera))
			{
				if (SelectedAttachmentIndex >= 0 && editorController.currentMap?.attachments?[SelectedAttachmentIndex] is View view)
				{
					SnapViewDistanceToGround(view, editorController.iMapManager);
				}
			}

			if (editorCamera == null || IsGuiControlActive())
				return;

			// Preview camera control (RMB orbit + WASD)
			if (isControllingPreviewWithRMB && viewPreview?.previewCam != null)
			{
				EditorCameraMovement.UpdateCamera(viewPreview.previewCam.transform);
				SyncPreviewToSelectedView();
			}

			// Tile picking
			Vector3 mouseWorld = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);
			Vector3 snapped = editorController.iMapManager.SnappedMapPosition(mouseWorld);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

			// LMB down
			if (Input.GetMouseButtonDown(0))
			{
				clickStartPos = Input.mousePosition;
				clickStartTile = tileUnderMouse;

				int hitTile = HitTile(Input.mousePosition);
				var atts = GetAttachmentsOnTile(hitTile);
				if (atts != null)
				{
					draggingTile = hitTile;
					draggedAttachments = editorController.iMapManager.CurrentMap.attachments
						.Where(x => x.tile == hitTile).ToArray();

					if (draggedAttachments.Length > 0)
						SelectAttachment(System.Array.IndexOf(editorController.iMapManager.CurrentMap.attachments, draggedAttachments[0]));
				}
			}

			// LMB drag
			if (Input.GetMouseButton(0) && draggingTile >= 0 && tileUnderMouse >= 0 && tileUnderMouse != draggingTile)
			{
				bool moved = false;
				foreach (var att in draggedAttachments)
				{
					if (att.tile == draggingTile)
					{
						att.tile = tileUnderMouse;
						if (att is View v)
						{
							SnapViewDistanceToGround(v, editorController.iMapManager);
							EditorUtil.UpdateViewFrustumMarker(v, editorController.iMapManager);
							EditorTransformUtil.ShowTransformGizmo(v, editorController.iMapManager, editorCamera);
							viewPreview.Show(v, editorController.iMapManager);
						}
						moved = true;
					}
				}
				if (moved)
				{
					draggingTile = tileUnderMouse;
					RebuildMarkers();
				}
			}

			// LMB up — add new
			if (Input.GetMouseButtonUp(0))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;
				if (wasClick && clickStartTile >= 0 && GetAttachmentsOnTile(HitTile(Input.mousePosition)) == null)
				{
					pendingTile = clickStartTile;
					pendingAction = PendingAction.Add;
					var wp = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
					var sp = editorCamera.WorldToScreenPoint(wp);
					sp.y = Screen.height - sp.y;
					pendingPopupScreenPos = sp;
				}

				draggingTile = -1;
				draggedAttachments = System.Array.Empty<MapAttachment>();
			}

			// RMB click — delete
			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, clickStartPos) < 6f)
			{
				int hitTile = HitTile(clickStartPos);
				if (hitTile != -1)
				{
					pendingTile = hitTile;
					pendingAction = PendingAction.Delete;
					var wp = editorController.iMapManager.TileWorldPosition(hitTile) + Vector3.up * 0.6f;
					var sp = editorCamera.WorldToScreenPoint(wp);
					sp.y = Screen.height - sp.y;
					pendingPopupScreenPos = sp;
				}
			}
		}

		private void SyncPreviewToSelectedView()
		{
			if (SelectedAttachmentIndex < 0) return;
			var map = editorController.iMapManager.CurrentMap;
			if (map?.attachments == null || SelectedAttachmentIndex >= map.attachments.Length) return;

			if (map.attachments[SelectedAttachmentIndex] is View view)
			{
				Vector3 wp = viewPreview.previewCam.transform.position;
				view.Position = wp - editorController.iMapManager.TileWorldPosition(view.tile);
				view.Rotation = viewPreview.previewCam.transform.rotation;

				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				EditorTransformUtil.UpdateTransformGizmoVisuals(editorCamera);
			}
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = editorController.currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex)) return null;
			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		private void AddAttachmentAtTileWithType(int tile, System.Type type)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			MapAttachment newAtt = type.Name switch
			{
				"Emitter" => new Emitter { tile = tile, Position = Vector3.up, LookAt = Vector3.up },
				"View" => new View { tile = tile, Position = (Vector3.up + Vector3.back) * 8, LookAt = (Vector3.forward + Vector3.down) * 4 },
				"Pickup" => new Pickup { tile = tile },
				_ => null
			};

			if (newAtt == null) return;

			map.AddAttachment(newAtt);
			RebuildMarkers();
			editorController.OnMapChanged();

			if (newAtt is View view)
			{
				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
				viewPreview.Show(view, editorController.iMapManager);
				SelectAttachment(System.Array.IndexOf(map.attachments, newAtt));
			}
		}

		private void DrawAddPopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 120;
			sp.y -= 140;

			var items = new List<PopupItem>
			{
				new PopupItem("Emitter", () =>
				{
					AddAttachmentAtTileWithType(pendingTile, typeof(Emitter));
					editorController.OnMapChanged();
					RebuildMarkers();
				}),

				new PopupItem("View", () =>
				{
					AddAttachmentAtTileWithType(pendingTile, typeof(View));
					editorController.OnMapChanged();
					RebuildMarkers();
				}),


				new PopupItem("Pickup", () =>
				{
					AddAttachmentAtTileWithType(pendingTile, typeof(Pickup));
					editorController.OnMapChanged();
					RebuildMarkers();
				}),

				PopupItem.Spacer(),

				// no null needed
				new PopupItem("Cancel", colorOverride: Color.yellow)
			};

			bool closed = PopupMenu.Show(sp, "Add Attachment", items);

			if (closed)
				pendingAction = PendingAction.None;
		}

		private void DrawDeletePopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 140;
			sp.y -= 120;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var attsOnTile = map.GetAttachmentsOnTile(pendingTile);
			if (attsOnTile.Length == 0) return;

			var items = new List<PopupItem>();

			// 1. Individual delete items
			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
				var localAtt = att; // capture for action

				items.Add(new PopupItem(label, () =>
				{
					map.RemoveAttachment(localAtt);
					RebuildMarkers();
					editorController.OnMapChanged();
				}));
			}

			// 2. Spacer
			items.Add(PopupItem.Spacer());

			// 3. Delete All button
			items.Add(new PopupItem("Delete All", () =>
			{
				map.RemoveAllAttachmentsOnTile(pendingTile);

				EditorUtil.DestroyViewFrustumMarker();
				EditorTransformUtil.HideTransformGizmo();

				RebuildMarkers();
				editorController.OnMapChanged();
			}, colorOverride: Color.red));

			// 4. Cancel
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));


			// Show popup
			bool closed = PopupMenu.Show(sp, "Delete Attachment(s)", items);

			if (closed)
				pendingAction = PendingAction.None;
		}

		public static void SnapViewDistanceToGround(View view, IMapManager mapManager)
		{
			if (view == null || mapManager == null) return;

			var origin = mapManager.TileWorldPosition(view.tile) + view.Position;
			var forward = view.Rotation * Vector3.forward;
			var ray = new Ray(origin, forward);

			if (MapManager.RayToWorld(ray, out Vector3 result))
			{
				float distance = (result - origin).magnitude;
				if (distance > 0.1f)
				{
					view.Distance = Mathf.Min(distance, View.MAX_DISTANCE);
					return;
				}
			}

			view.Distance = View.MAX_DISTANCE;
		}
	}
}
