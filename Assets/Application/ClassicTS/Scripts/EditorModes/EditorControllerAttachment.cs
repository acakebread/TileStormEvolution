// File: EditorControllerAttachment.cs
// FINAL — Fully working with RMB preview control + mouse wheel + fixed MapAttachments

using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;
using static ClassicTilestorm.EditorController;
using UnityEditor;
using UnityEngine.EventSystems;

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

		private Vector2 scrollPos = Vector2.zero;

		private ViewPreview viewPreview;

		private readonly AutoHidePanel sidePanel = new(
			collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f);

		// RMB control over preview camera
		private bool isControllingPreviewWithRMB = false;

		// Add this field at the top of your class (with the others)
		private bool isMouseOverPreview = false;

		public override bool IsMouseOverModeGui()
		{
			// First: check preview window
			isMouseOverPreview = false;
			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y;
				if (hitRect.Contains(mousePos))
				{
					isMouseOverPreview = true;
					return true; // This blocks main camera scroll
				}
			}

			// Then: side panel
			if (editorController.CurrentMode != EditorMode.Attachment) return false;
			float w = sidePanel.CurrentWidth;
			var rect = new Rect(Screen.width - w - 20f, 20f, w, Screen.height - 40f);
			var mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return rect.Contains(mouse);
		}

		public EditorControllerAttachment(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedAttachmentIndex = -1;
			EditorUtil.DestroyMarkerVisuals();
			EditorUtil.DestroyViewFrustumMarker();
			RebuildMarkers();

			viewPreview = ViewPreview.Create();
			viewPreview.Hide();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyMarkerVisuals();
			pendingAction = PendingAction.None;
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview?.Hide();
			isControllingPreviewWithRMB = false;
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
			if (map?.attachments != null && index >= 0 && index < map.attachments.Length)
			{
				if (map.attachments[index] is View view)
				{
					SnapViewDistanceToGround(view, editorController.iMapManager);
					EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
					EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
					viewPreview.Show(view, editorController.iMapManager);
				}
			}
		}

		protected override bool ShouldUseMainCameraThisFrame()
		{
			return !isControllingPreviewWithRMB;
		}

		public override void Update()
		{
			if (!IsMouseOverModeGui())
				base.Update();

			// Handle transform gizmo (position/rotation via LMB on gizmo)
			if (EditorTransformUtil.HandleTransformGizmoInput(editorCamera))
			{
				var map = editorController.currentMap;
				if (map?.attachments != null && SelectedAttachmentIndex >= 0 && SelectedAttachmentIndex < map.attachments.Length)
				{
					if (map.attachments[SelectedAttachmentIndex] is View view)
						SnapViewDistanceToGround(view, editorController.iMapManager);
				}
			}

			if (editorCamera == null || editorController.IsGuiControlActive() ||
				(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
				return;

			// === PREVIEW CAMERA CONTROL + SYNC TO SELECTED VIEW ===
			if (isControllingPreviewWithRMB && viewPreview?.previewCam != null)
			{
				EditorCameraMovement.UpdateCamera(viewPreview.previewCam.transform);

				// Apply preview camera transform back to the selected View
				if (SelectedAttachmentIndex >= 0)
				{
					var map = editorController.iMapManager.CurrentMap;
					if (map?.attachments != null && SelectedAttachmentIndex < map.attachments.Length &&
						map.attachments[SelectedAttachmentIndex] is View selectedView)
					{
						Vector3 previewWorldPos = viewPreview.previewCam.transform.position;
						selectedView.Position = previewWorldPos - editorController.iMapManager.TileWorldPosition(selectedView.tile);
						selectedView.Rotation = viewPreview.previewCam.transform.rotation;

						// Keep visual markers in sync
						EditorUtil.UpdateViewFrustumMarker(selectedView, editorController.iMapManager);
						EditorTransformUtil.UpdateTransformGizmoVisuals(editorCamera);
					}
				}
			}

			// Mouse-to-world for tile picking
			Vector3 mouseWorldPos = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);
			Vector3 snappedPos = editorController.iMapManager.SnappedMapPosition(mouseWorldPos);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snappedPos);

			// === LMB: Start dragging attachments ===
			if (Input.GetMouseButtonDown(0))
			{
				clickStartPos = Input.mousePosition;
				clickStartTile = tileUnderMouse;

				int hitTile = HitTile(Input.mousePosition);
				var attachments = GetAttachmentsOnTile(hitTile);
				if (attachments != null)
				{
					var map = editorController.iMapManager.CurrentMap;
					draggingTile = hitTile;
					draggedAttachments = map.attachments.Where(x => x.tile == hitTile).ToArray();
					if (draggedAttachments.Length > 0)
						SelectAttachment(System.Array.IndexOf(map.attachments, draggedAttachments[0]));
				}
			}

			// === LMB Drag: Move attachments ===
			if (Input.GetMouseButton(0) && draggingTile >= 0 && tileUnderMouse >= 0 && tileUnderMouse != draggingTile)
			{
				var map = editorController.iMapManager.CurrentMap;
				if (map?.attachments == null) return;

				bool moved = false;
				foreach (var att in draggedAttachments)
				{
					if (att.tile == draggingTile)
					{
						att.tile = tileUnderMouse;
						if (att is View view)
						{
							SnapViewDistanceToGround(view, editorController.iMapManager);
							EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
							EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
							viewPreview.Show(view, editorController.iMapManager);
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

			// === LMB Up: Add new attachment if clicked empty tile ===
			if (Input.GetMouseButtonUp(0))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;
				var attachments = GetAttachmentsOnTile(HitTile(Input.mousePosition));

				if (wasClick && clickStartTile >= 0 && attachments == null)
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

			// === RMB Up: Delete attachment ===
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

		//public override void Update()
		//{
		//	base.Update(); // Moves main editor camera only if not controlling preview

		//	if (EditorTransformUtil.HandleTransformGizmoInput(editorCamera))
		//	{
		//		var map = editorController.currentMap;
		//		if (map?.attachments != null && SelectedAttachmentIndex >= 0 && SelectedAttachmentIndex < map.attachments.Length)
		//		{
		//			if (map.attachments[SelectedAttachmentIndex] is View view)
		//				SnapViewDistanceToGround(view, editorController.iMapManager);
		//		}
		//	}

		//	if (editorCamera == null || editorController.IsGuiControlActive() ||
		//		(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
		//		return;

		//	// === PREVIEW CAMERA CONTROL (RMB + mouse wheel) ===
		//	if (isControllingPreviewWithRMB && viewPreview?.previewCam != null)
		//	{
		//		EditorCameraMovement.UpdateCamera(viewPreview.previewCam.transform, allowInput: true);
		//	}

		//	var worldPos = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);
		//	var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
		//	int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

		//	// === LMB: Start dragging attachments ===
		//	if (Input.GetMouseButtonDown(0))
		//	{
		//		clickStartPos = Input.mousePosition;
		//		clickStartTile = tileUnderMouse;

		//		var hitTile = HitTile(Input.mousePosition);
		//		var attachments = GetAttachmentsOnTile(hitTile);
		//		if (attachments != null)
		//		{
		//			var map = editorController.iMapManager.CurrentMap;
		//			draggingTile = hitTile;
		//			draggedAttachments = map.attachments.Where(x => x.tile == hitTile).ToArray();
		//			if (draggedAttachments.Length > 0)
		//				SelectAttachment(System.Array.IndexOf(map.attachments, draggedAttachments[0]));
		//		}
		//	}

		//	// === LMB Drag: Move attachments ===
		//	if (Input.GetMouseButton(0) && draggingTile >= 0 && tileUnderMouse >= 0 && tileUnderMouse != draggingTile)
		//	{
		//		var map = editorController.iMapManager.CurrentMap;
		//		if (map?.attachments == null) return;

		//		bool moved = false;
		//		foreach (var att in draggedAttachments)
		//		{
		//			if (att.tile == draggingTile)
		//			{
		//				att.tile = tileUnderMouse;
		//				if (att is View view)
		//				{
		//					SnapViewDistanceToGround(view, editorController.iMapManager);
		//					EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
		//					EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
		//					viewPreview.Show(view, editorController.iMapManager);
		//				}
		//				moved = true;
		//			}
		//		}
		//		if (moved)
		//		{
		//			draggingTile = tileUnderMouse;
		//			RebuildMarkers();
		//		}
		//	}

		//	// === LMB Up: Add new attachment if clicked empty tile ===
		//	if (Input.GetMouseButtonUp(0))
		//	{
		//		bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;
		//		var attachments = GetAttachmentsOnTile(HitTile(Input.mousePosition));

		//		if (wasClick && clickStartTile >= 0 && attachments == null)
		//		{
		//			pendingTile = clickStartTile;
		//			pendingAction = PendingAction.Add;

		//			var wp = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
		//			var sp = editorCamera.WorldToScreenPoint(wp);
		//			sp.y = Screen.height - sp.y;
		//			pendingPopupScreenPos = sp;
		//		}

		//		draggingTile = -1;
		//		draggedAttachments = System.Array.Empty<MapAttachment>();
		//	}

		//	// === RMB Up: Delete attachment ===
		//	if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, clickStartPos) < 6f)
		//	{
		//		var hitTile = HitTile(clickStartPos);
		//		if (hitTile != -1)
		//		{
		//			pendingTile = hitTile;
		//			pendingAction = PendingAction.Delete;

		//			var wp = editorController.iMapManager.TileWorldPosition(hitTile) + Vector3.up * 0.6f;
		//			var sp = editorCamera.WorldToScreenPoint(wp);
		//			sp.y = Screen.height - sp.y;
		//			pendingPopupScreenPos = sp;
		//		}
		//	}
		//}

		// Helper: Replaces old MapAttachments() call
		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = editorController.currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex))
				return null;

			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorMode.Attachment || editorCamera == null) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;
			var attachments = map.attachments ?? System.Array.Empty<MapAttachment>();

			sidePanel.Update();
			Rect panel = sidePanel.GetRect();

			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.95f);
			GUI.Box(panel, "");
			GUI.backgroundColor = Color.white;

			GUILayout.BeginArea(panel);
			GUILayout.BeginVertical();

			if (attachments.Length == 0)
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label("No attachments\nLeft-click map to add",
					new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
				GUILayout.FlexibleSpace();
			}
			else
				DrawAttachmentList(attachments, panel.height);

			GUILayout.EndVertical();
			GUILayout.EndArea();

			// === PREVIEW WINDOW RMB CONTROL + CLEAN VISUAL FEEDBACK ===
			// === PREVIEW WINDOW: RMB CONTROL + ALWAYS-ACTIVE MOUSE WHEEL ZOOM ===
			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y;
				bool mouseOverPreview = hitRect.Contains(mousePos);

				// === RMB CONTROL (orbit + WASD) ===
				if (!isControllingPreviewWithRMB && Input.GetMouseButtonDown(1) && mouseOverPreview)
				{
					isControllingPreviewWithRMB = true;
				}
				if (isControllingPreviewWithRMB && Input.GetMouseButtonUp(1))
				{
					isControllingPreviewWithRMB = false;
				}

				// === MOUSE WHEEL ZOOM: ALWAYS ACTIVE WHEN HOVERING (even without RMB) ===
				if (mouseOverPreview && !editorController.IsGuiControlActive())
				{
					float scroll = Input.GetAxis("Mouse ScrollWheel");
					if (scroll != 0f)
					{
						// Apply zoom directly to preview camera
						viewPreview.previewCam.transform.Translate(0, 0, scroll * 120f * Time.deltaTime, Space.Self);

						// Sync back to selected View (only if one is selected)
						if (SelectedAttachmentIndex >= 0)
						{
							var _map = editorController.iMapManager.CurrentMap;
							if (_map?.attachments != null && SelectedAttachmentIndex < _map.attachments.Length &&
								_map.attachments[SelectedAttachmentIndex] is View selectedView)
							{
								Vector3 newWorldPos = viewPreview.previewCam.transform.position;
								selectedView.Position = newWorldPos - editorController.iMapManager.TileWorldPosition(selectedView.tile);

								EditorUtil.UpdateViewFrustumMarker(selectedView, editorController.iMapManager);
								EditorTransformUtil.UpdateTransformGizmoVisuals(editorCamera);
							}
						}
					}
				}

				// === VISUAL FEEDBACK (clean glowing border) ===
				if (isControllingPreviewWithRMB || mouseOverPreview)
				{
					Color borderColor = isControllingPreviewWithRMB
						? new Color(0.3f, 0.9f, 1f, 1f)     // Bright cyan when active
						: new Color(0.7f, 0.9f, 1f, 0.7f);   // Soft blue on hover

					float thickness = isControllingPreviewWithRMB ? 3.5f : 1.8f;

					GUI.color = borderColor;
					GUI.DrawTexture(new Rect(hitRect.x - thickness, hitRect.y - thickness, hitRect.width + thickness * 2, thickness), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(hitRect.x - thickness, hitRect.yMax, hitRect.width + thickness * 2, thickness), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(hitRect.x - thickness, hitRect.y, thickness, hitRect.height), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(hitRect.xMax, hitRect.y, thickness, hitRect.height), Texture2D.whiteTexture);
					GUI.color = Color.white;

					// Optional label only when actively controlling
					if (isControllingPreviewWithRMB)
					{
						GUI.color = Color.black;
						GUI.Label(new Rect(hitRect.xMax - 179, hitRect.y - 29, 170, 22), " Preview Active");
						GUI.color = new Color(0.3f, 0.9f, 1f);
						GUI.Label(new Rect(hitRect.xMax - 180, hitRect.y - 30, 170, 22), " Preview Active");
						GUI.color = Color.white;
					}
				}
			}

			if (pendingAction == PendingAction.Add) DrawAddPopup();
			if (pendingAction == PendingAction.Delete) DrawDeletePopup();
		}

		private GUIStyle leftButtonStyle;
		private void DrawAttachmentList(MapAttachment[] attachments, float panelHeight)
		{
			if (leftButtonStyle == null)
			{
				leftButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 0, 0, 0) };
			}

			const float reservedBottom = 110f;
			const float topOffset = 25f;
			float scrollHeight = Mathf.Max(0f, panelHeight - reservedBottom);

			Rect panelRect = sidePanel.GetRect();
			Rect scrollRect = new Rect(0f, topOffset, panelRect.width, scrollHeight);
			Rect contentRect = new Rect(0f, 0f, scrollRect.width - 22f, attachments.Length * 40f);

			scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, contentRect, false, true);

			float y = 0f;
			for (int i = 0; i < attachments.Length; i++)
			{
				var att = attachments[i];
				string extra = att is Emitter e && e.LookAt != null ? $" to {e.LookAt:F1}" : "";
				string label = $"ATT{i:00} [{att.tile}] {att.type}{extra}";

				GUI.backgroundColor = (i == SelectedAttachmentIndex) ? new Color(0.3f, 0.8f, 1f, 0.9f) : Color.white;

				if (GUI.Button(new Rect(0f, y, contentRect.width, 36f), label, leftButtonStyle))
					SelectAttachment(i);

				GUI.backgroundColor = Color.white;
				y += 40f;
			}

			GUI.EndScrollView();

			Rect tipArea = new Rect(0f, scrollRect.y + scrollRect.height + 6f, panelRect.width, reservedBottom);
			GUILayout.BeginArea(tipArea);
			GUILayout.Label("Hold RMB on preview to orbit • Scroll to zoom\nLMB: place/move • RMB on tile: delete",
				new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
			GUILayout.EndArea();
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
			sp.x -= 120; sp.y -= 140;

			bool closed = PopupAttachmentAdd.Show(sp, pendingTile, choice =>
			{
				if (choice >= 0 && choice <= 2)
				{
					System.Type t = choice switch { 0 => typeof(Emitter), 1 => typeof(View), 2 => typeof(Pickup), _ => null };
					if (t != null)
					{
						AddAttachmentAtTileWithType(pendingTile, t);
						editorController.OnMapChanged();
						RebuildMarkers();
					}
				}
				pendingAction = PendingAction.None;
			});

			if (closed && pendingAction == PendingAction.Add)
				pendingAction = PendingAction.None;
		}

		private void DrawDeletePopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 140; sp.y -= 120;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var attsOnTile = map.GetAttachmentsOnTile(pendingTile);
			if (attsOnTile.Length == 0) return;

			var types = attsOnTile.Select(a => a.GetType().Name).ToArray();

			bool closed = PopupAttachmentDelete.Show(sp, pendingTile, types, choice =>
			{
				if (choice >= 0 && choice < attsOnTile.Length)
					map.RemoveAttachment(attsOnTile[choice]);
				else if (choice == -2)
				{
					map.RemoveAllAttachmentsOnTile(pendingTile);
					EditorUtil.DestroyViewFrustumMarker();
					EditorTransformUtil.HideTransformGizmo();
				}

				RebuildMarkers();
				editorController.OnMapChanged();
				pendingAction = PendingAction.None;
			});

			if (closed && pendingAction == PendingAction.Delete)
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
