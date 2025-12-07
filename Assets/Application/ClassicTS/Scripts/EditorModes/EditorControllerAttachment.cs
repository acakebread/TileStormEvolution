// File: EditorControllerAttachment.cs
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

		private int draggingIndex = -1;

		private int pendingTile = -1;
		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		private Vector3 clickStartPos;
		private int clickStartTile = -1;
		private int potentialAttachmentHit = -1;

		private Vector2 scrollPos = Vector2.zero;

		private readonly AutoHidePanel sidePanel = new(
			collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f);

		public override bool IsMouseOverModeGui()
		{
			if (editorController.CurrentMode != EditorMode.Attachment) return false;
			float w = sidePanel.CurrentWidth;
			var rect = new Rect(Screen.width - w - 20f, 20f, w, Screen.height - 40f);
			var mouse = Input.mousePosition; mouse.y = Screen.height - mouse.y;
			return rect.Contains(mouse);
		}

		public EditorControllerAttachment(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedAttachmentIndex = -1;
			EditorUtil.DestroyAttachmentVisuals();
			EditorUtil.DestroyViewFrustumMarker();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyAttachmentVisuals();
			pendingAction = PendingAction.None;
			EditorUtil.DestroyViewFrustumMarker();
		}

		public void OnMapChanged()
		{
			if (editorController.CurrentMode != EditorMode.Attachment) return;
			RebuildMarkers();
			EditorUtil.DestroyViewFrustumMarker();
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

			EditorUtil.UpdateAttachmentMarkers(editorController.iMapManager, tiles, SelectedAttachmentIndex);
		}

		private void SelectAttachment(int index)
		{
			SelectedAttachmentIndex = index;
			RebuildMarkers();

			EditorUtil.DestroyViewFrustumMarker();

			var map = editorController?.iMapManager?.CurrentMap;
			if (map?.attachments != null && index >= 0 && index < map.attachments.Length)
			{
				if (map.attachments[index] is View view)
				{
					EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				}
			}
		}

		public override void Update()
		{
			base.Update();

			if (camera == null || editorController.IsGuiControlActive() ||
				(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
				return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				clickStartPos = Input.mousePosition;
				clickStartTile = tileUnderMouse;
				potentialAttachmentHit = TryHitAttachment(out int hitIndex) ? hitIndex : -1;
			}

			// ================== LEFT CLICK RELEASE ==================
			if (Input.GetMouseButtonUp(0))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				// ←←← ONLY THIS PART WAS REMOVED (the revert-to-originalTile block) ←←←
				// We no longer need to snap back because the attachment never goes to invalid tiles
				draggingIndex = -1;

				if (wasClick && clickStartTile >= 0)
				{
					pendingTile = clickStartTile;
					pendingAction = PendingAction.Add;

					var _worldPos = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
					var sp = camera.WorldToScreenPoint(_worldPos);
					sp.y = Screen.height - sp.y;
					pendingPopupScreenPos = sp;
				}
			}

			if (Input.GetMouseButtonUp(1))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				if (wasClick && potentialAttachmentHit >= 0)
				{
					SelectAttachment(potentialAttachmentHit);

					var map = editorController.iMapManager.CurrentMap;
					if (map?.attachments != null && potentialAttachmentHit < map.attachments.Length)
					{
						pendingTile = map.attachments[potentialAttachmentHit].tile;
						pendingAction = PendingAction.Delete;

						var _worldPos = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
						var sp = camera.WorldToScreenPoint(_worldPos);
						sp.y = Screen.height - sp.y;
						pendingPopupScreenPos = sp;
					}
				}
			}

			// ================== LIVE DRAGGING (HELD) ==================
			if (Input.GetMouseButton(0) && draggingIndex >= 0)
			{
				var map = editorController.iMapManager.CurrentMap;
				if (map?.attachments == null || draggingIndex >= map.attachments.Length) return;

				var attachment = map.attachments[draggingIndex];

				if (tileUnderMouse >= 0)
				{
					if (attachment.tile != tileUnderMouse)
					{
						attachment.tile = tileUnderMouse;

						if (attachment is View view)
						{
							Vector3 newWorldPos = editorController.iMapManager.TileWorldPosition(tileUnderMouse);
							Vector3 oldWorldPos = editorController.iMapManager.TileWorldPosition(attachment.tile);

							Vector3 localOffset = view.Position - oldWorldPos;
							view.Position = newWorldPos + localOffset;

							EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
						}

						RebuildMarkers();
					}
				}
				// mouse over invalid area → do nothing, attachment stays on last valid tile
			}

			// ================== START DRAG ==================
			if (Input.GetMouseButtonDown(0) && potentialAttachmentHit >= 0)
			{
				SelectAttachment(potentialAttachmentHit);
				draggingIndex = potentialAttachmentHit;
				pendingAction = PendingAction.None;
			}
		}

		private bool TryHitAttachment(out int index)
		{
			index = -1;
			var ray = camera.ScreenPointToRay(Input.mousePosition);
			if (!Physics.Raycast(ray, out RaycastHit hit)) return false;
			if (!hit.collider.name.StartsWith("WP")) return false;
			return int.TryParse(hit.collider.name.Substring(2), out index);
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorMode.Attachment || camera == null) return;

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

			GUILayout.Label("Attachments", EditorStyles.boldLabel);

			if (attachments.Length == 0)
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label("No attachments\nLeft-click map to add",
					new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
				GUILayout.FlexibleSpace();
			}
			else DrawAttachmentList(attachments, panel.height);

			GUILayout.EndVertical();
			GUILayout.EndArea();

			if (pendingAction == PendingAction.Add)
				DrawAddPopup();

			if (pendingAction == PendingAction.Delete)
				DrawDeletePopup();
		}

		// ──────────────────────────────────────────────────────────────
		// GUI DRAWING METHODS (unchanged – copied exactly from your code)
		// ──────────────────────────────────────────────────────────────

		private GUIStyle leftButtonStyle;
		private void DrawAttachmentList(MapAttachment[] attachments, float panelHeight)
		{
			if (leftButtonStyle == null)
			{
				leftButtonStyle = new GUIStyle(GUI.skin.button);
				leftButtonStyle.alignment = TextAnchor.MiddleLeft;
				leftButtonStyle.padding.left = 12;
			}

			const float reservedBottom = 110f;
			const float topOffset = 25f;
			float scrollHeight = Mathf.Max(0f, panelHeight - reservedBottom);

			Rect panelRect = sidePanel.GetRect();

			Rect scrollRect = new Rect(0f, topOffset, panelRect.width, scrollHeight);
			float scrollBarWidth = 12f;
			Rect contentRect = new Rect(0f, 0f, scrollRect.width - scrollBarWidth - 10, attachments.Length * 40f);

			scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, contentRect, false, true);

			float y = 0f;
			for (int i = 0; i < attachments.Length; i++)
			{
				var att = attachments[i];
				string typeLabel = att.type;
				string extra = "";
				if (att is Emitter emitter && emitter.LookAt != null)
					extra = $" → {emitter.LookAt:F1}";

				string label = $"ATT{i:00} [{att.tile}] {typeLabel}{extra}";

				GUI.backgroundColor = (i == SelectedAttachmentIndex)
					? new Color(0.3f, 0.8f, 1f, 0.9f)
					: Color.white;

				if (GUI.Button(new Rect(0f, y, contentRect.width, 36f), label, leftButtonStyle))
					SelectAttachment(i);

				GUI.backgroundColor = Color.white;
				y += 40f;
			}

			GUI.EndScrollView();

			Rect buttonsArea = new Rect(0f, scrollRect.y + scrollRect.height + 6f, panelRect.width, reservedBottom);
			GUILayout.BeginArea(buttonsArea);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.Space(4);
			GUILayout.Label("Tip: Left-click map to add • Right-click attachment to delete",
				new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
			GUILayout.EndArea();
		}

		private void AddAttachmentAtTileWithType(int tile, System.Type type)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			MapAttachment newAtt = type.Name switch
			{
				"Emitter" => new Emitter { tile = tile, LookAt = Vector3.forward },
				"View" => new View { tile = tile },
				"Pickup" => new Pickup { tile = tile },
				_ => null
			};

			if (newAtt == null) return;

			map.AddAttachment(newAtt);

			RebuildMarkers();
			editorController.OnMapChanged();

			if (newAtt is View view)
				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
		}

		private void DrawAddPopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 120;
			sp.y -= 140;

			bool popupClosed = PopupAttachmentAdd.Show(sp, pendingTile, choice =>
			{
				// This callback runs when user clicks an option
				if (choice >= 0 && choice <= 2)
				{
					System.Type type = choice switch
					{
						0 => typeof(Emitter),
						1 => typeof(View),
						2 => typeof(Pickup),
						_ => null
					};

					if (type != null)
					{
						AddAttachmentAtTileWithType(pendingTile, type);
						editorController.OnMapChanged(); // CRITICAL: tell editor map changed
						RebuildMarkers();                 // CRITICAL: refresh visuals
					}
				}
				// choice == -1 → cancel → do nothing

				pendingAction = PendingAction.None; // close popup
			});

			// Only close popup automatically if user clicked outside
			if (popupClosed && pendingAction == PendingAction.Add)
		    {
				pendingAction = PendingAction.None;
			}
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

			if (PopupAttachmentDelete.Show(sp, pendingTile, attsOnTile.Length, choice =>
			{
				if (choice >= 0 && choice < attsOnTile.Length)
				{
					// Delete single
					map.RemoveAttachment(attsOnTile[choice]);
				}
				else if (choice == -2)
				{
					// Delete all
					map.RemoveAllAttachmentsOnTile(pendingTile);
					EditorUtil.DestroyViewFrustumMarker();
				}

				RebuildMarkers();
				editorController.OnMapChanged();
				pendingAction = PendingAction.None;
			}))
			{
				pendingAction = PendingAction.None;
			}
		}
	}
}