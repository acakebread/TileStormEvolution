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

		private int draggingTile = -1;
		private int[] draggedAttachmentIndices = System.Array.Empty<int>();

		private int pendingTile = -1;
		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		private Vector3 clickStartPos;
		private int clickStartTile = -1;

		private Vector2 scrollPos = Vector2.zero;

		private readonly AutoHidePanel sidePanel = new(
			collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f);

		public override bool IsMouseOverModeGui()
		{
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
					EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
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

				if (TryHitAttachmentMarker(out int hitTile))
				{
					var map = editorController.iMapManager.CurrentMap;

					if (Input.GetMouseButtonDown(0))
					{
						draggingTile = hitTile;
						draggedAttachmentIndices = map.attachments
							.Select((a, i) => new { a, i })
							.Where(x => x.a.tile == hitTile)
							.Select(x => x.i)
							.ToArray();

						if (draggedAttachmentIndices.Length > 0)
							SelectAttachment(draggedAttachmentIndices[0]);
					}
				}
			}

			if (Input.GetMouseButton(0) && draggingTile >= 0 && tileUnderMouse >= 0 && tileUnderMouse != draggingTile)
			{
				var map = editorController.iMapManager.CurrentMap;
				if (map?.attachments == null) return;

				bool moved = false;

				foreach (int idx in draggedAttachmentIndices)
				{
					var att = map.attachments[idx];
					if (att.tile == draggingTile)
					{
						att.tile = tileUnderMouse;

						if (att is View view)
							EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);

						moved = true;
					}
				}

				if (moved)
				{
					draggingTile = tileUnderMouse;
					RebuildMarkers();
				}
			}

			if (Input.GetMouseButtonUp(0))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				if (wasClick && clickStartTile >= 0 && !TryHitAttachmentMarker(out _))
				{
					pendingTile = clickStartTile;
					pendingAction = PendingAction.Add;

					var wp = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
					var sp = camera.WorldToScreenPoint(wp);
					sp.y = Screen.height - sp.y;
					pendingPopupScreenPos = sp;
				}

				draggingTile = -1;
				draggedAttachmentIndices = System.Array.Empty<int>();
			}

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, clickStartPos) < 6f)
			{
				var hitTile = HitTile(clickStartPos);
				if (-1 != hitTile)
				{
					pendingTile = hitTile;
					pendingAction = PendingAction.Delete;

					var wp = editorController.iMapManager.TileWorldPosition(hitTile) + Vector3.up * 0.6f;
					var sp = camera.WorldToScreenPoint(wp);
					sp.y = Screen.height - sp.y;
					pendingPopupScreenPos = sp;
				}
			}
		}

		private int HitTile(Vector3 mousePos)
		{
			var tileIndex = -1;
			var ray = camera.ScreenPointToRay(mousePos);
			if (!Physics.Raycast(ray, out RaycastHit hit)) return -1;

			if (hit.collider.name.StartsWith("ATT_"))
			{
				string num = hit.collider.name.Substring(4);
				if (int.TryParse(num, out tileIndex))
					return tileIndex;
			}
			return -1;
		}

		private bool TryHitAttachmentMarker(out int tileIndex)
		{
			tileIndex = -1;
			var ray = camera.ScreenPointToRay(Input.mousePosition);
			if (!Physics.Raycast(ray, out RaycastHit hit)) return false;

			if (hit.collider.name.StartsWith("ATT_"))
			{
				string num = hit.collider.name.Substring(4);
				return int.TryParse(num, out tileIndex);
			}
			return false;
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
			else
				DrawAttachmentList(attachments, panel.height);

			GUILayout.EndVertical();
			GUILayout.EndArea();

			if (pendingAction == PendingAction.Add) DrawAddPopup();
			if (pendingAction == PendingAction.Delete) DrawDeletePopup();
		}

		private GUIStyle leftButtonStyle;
		private void DrawAttachmentList(MapAttachment[] attachments, float panelHeight)
		{
			if (leftButtonStyle == null)
			{
				leftButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = { left = 12 } };
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
				"View" => new View { tile = tile, Position = (Vector3.up + Vector3.back) * 8, LookAt = (Vector3.forward + Vector3.down) * 4 },
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
			sp.x -= 140;
			sp.y -= 120;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var attsOnTile = map.GetAttachmentsOnTile(pendingTile);
			if (attsOnTile.Length == 0) return;

			bool closed = PopupAttachmentDelete.Show(sp, pendingTile, attsOnTile.Length, choice =>
			{
				if (choice >= 0 && choice < attsOnTile.Length)
					map.RemoveAttachment(attsOnTile[choice]);
				else if (choice == -2)
				{
					map.RemoveAllAttachmentsOnTile(pendingTile);
					EditorUtil.DestroyViewFrustumMarker();
				}

				RebuildMarkers();
				editorController.OnMapChanged();
				pendingAction = PendingAction.None;
			});

			if (closed && pendingAction == PendingAction.Delete)
				pendingAction = PendingAction.None;
		}
	}
}