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

		// RMB preview control
		private bool isControllingPreviewWithRMB = false;
		private bool rmbDragStartedInPreview = false; // THIS FIXES THE FREEZE BUG

		private bool isMouseOverPreview = false;

		public override bool IsMouseOverModeGui()
		{
			isMouseOverPreview = false;

			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mp = Input.mousePosition;
				mp.y = Screen.height - mp.y;

				if (hitRect.Contains(mp))
				{
					isMouseOverPreview = true;
					return true; // blocks main camera scroll AND mouse wheel
				}
			}

			if (editorController.CurrentMode != EditorMode.Attachment) return false;
			float w = sidePanel.CurrentWidth;
			Rect panelRect = new Rect(Screen.width - w - 20f, 20f, w, Screen.height - 40f);
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return panelRect.Contains(mouse);
		}

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedAttachmentIndex = -1;
			EditorUtil.DestroyMarkerVisuals();
			EditorUtil.DestroyViewFrustumMarker();
			RebuildMarkers();

			viewPreview = ViewPreview.Create();
			viewPreview.Hide();

			isControllingPreviewWithRMB = false;
			rmbDragStartedInPreview = false;
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

			if (editorCamera == null || editorController.IsGuiControlActive() ||
				(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
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

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorMode.Attachment || editorCamera == null) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			sidePanel.Update();
			Rect panel = sidePanel.GetRect();

			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.95f);
			GUI.Box(panel, "");
			GUI.backgroundColor = Color.white;

			GUILayout.BeginArea(panel);
			GUILayout.BeginVertical();

			var attachments = map.attachments ?? System.Array.Empty<MapAttachment>();

			if (attachments.Length == 0)
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label("No attachments\nLeft-click map to add",
					new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
				GUILayout.FlexibleSpace();
			}
			else
			{
				DrawAttachmentList(attachments, panel.height);
			}

			GUILayout.EndVertical();
			GUILayout.EndArea();

			// PREVIEW WINDOW
			// PREVIEW WINDOW – FIXED RMB release + scroll + visual feedback
			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mp = Input.mousePosition;
				mp.y = Screen.height - mp.y;
				bool mouseOverPreview = hitRect.Contains(mp);

				// === RMB STATE TRACKING ===
				if (Input.GetMouseButtonDown(1))
				{
					rmbDragStartedInPreview = mouseOverPreview; // only remember where it STARTED
				}

				// Activate preview control ONLY if drag began inside the preview
				if (rmbDragStartedInPreview && Input.GetMouseButton(1))
				{
					isControllingPreviewWithRMB = true;
				}

				// ALWAYS deactivate on release – no matter where the mouse is now
				if (Input.GetMouseButtonUp(1))
				{
					isControllingPreviewWithRMB = false;
					// rmbDragStartedInPreview = false; // optional – reset for next press
				}

				// === MOUSE WHEEL ZOOM (smooth, original speed) ===
				if (mouseOverPreview && !editorController.IsGuiControlActive())
				{
					float scroll = Input.GetAxis("Mouse ScrollWheel");
					if (scroll != 0f)
					{
						viewPreview.previewCam.transform.Translate(0, 0, scroll * 120f * Time.deltaTime, Space.Self);
						SyncPreviewToSelectedView();
					}
				}

				// === VISUAL FEEDBACK ===
				if (isControllingPreviewWithRMB || mouseOverPreview)
				{
					Color border = isControllingPreviewWithRMB
						? new Color(0.3f, 0.9f, 1f, 1f)
						: new Color(0.7f, 0.9f, 1f, 0.7f);
					float t = isControllingPreviewWithRMB ? 3.5f : 1.8f;

					GUI.color = border;
					GUI.DrawTexture(new Rect(hitRect.x - t, hitRect.y - t, hitRect.width + t * 2, t), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(hitRect.x - t, hitRect.yMax, hitRect.width + t * 2, t), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(hitRect.x - t, hitRect.y, t, hitRect.height), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(hitRect.xMax, hitRect.y, t, hitRect.height), Texture2D.whiteTexture);
					GUI.color = Color.white;

					// "Preview Active" label only while RMB is actually held
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

		private GUIStyle leftButtonStyle;
		private void DrawAttachmentList(MapAttachment[] attachments, float panelHeight)
		{
			if (leftButtonStyle == null)
				leftButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 0, 0, 0) };

			const float reserved = 110f;
			const float top = 25f;
			float scrollH = Mathf.Max(0f, panelHeight - reserved);
			Rect panelRect = sidePanel.GetRect();
			Rect scrollRect = new Rect(0f, top, panelRect.width, scrollH);
			Rect viewRect = new Rect(0f, 0f, scrollRect.width - 22f, attachments.Length * 40f);

			scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, viewRect, false, true);

			float y = 0f;
			for (int i = 0; i < attachments.Length; i++)
			{
				var att = attachments[i];
				string extra = att is Emitter e && e.LookAt != null ? $" to {e.LookAt:F1}" : "";
				string label = $"ATT{i:00} [{att.tile}] {att.type}{extra}";

				GUI.backgroundColor = (i == SelectedAttachmentIndex) ? new Color(0.3f, 0.8f, 1f, 0.9f) : Color.white;
				if (GUI.Button(new Rect(0f, y, viewRect.width, 36f), label, leftButtonStyle))
					SelectAttachment(i);
				GUI.backgroundColor = Color.white;
				y += 40f;
			}

			GUI.EndScrollView();

			Rect tip = new Rect(0f, scrollRect.y + scrollRect.height + 6f, panelRect.width, reserved);
			GUILayout.BeginArea(tip);
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
