using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour, IEditorScreenUI, ITileSelector
	{
		public IMapEdit iMap;
		public Camera _camera => GetComponent<MainCameraController>()?.activeSystem?.camera;

		// ─── input state ───────────────────────────────────────
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition, editAltitude);
		private Vector3 workingCurrentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition, ActiveWorkingPlaneHeight);
		private Vector3 beginWorkingWorld => new(beginWorld.x + beginWorldAdjustment.x, ActiveWorkingPlaneHeight, beginWorld.z + beginWorldAdjustment.z);
		private Vector3 lastSnap = Vector3.zero;
		private Vector3 beginWorldAdjustment = Vector3.zero;
		private float? workingPlaneHeight = null;

		private ISelectable[] _selection = null;
		private ISelectable[] selection
		{
			get => _selection;

			set
			{
				var oldItems = _selection ?? Array.Empty<ISelectable>();
				var newItems = value ?? Array.Empty<ISelectable>();

				// 1. Deselect items that are no longer wanted
				var leaving = oldItems.Except(newItems).ToList();

				// Pass 1: only leaving cells → cleanup + deselect
				foreach (var iter in leaving)
				{
					if (iter is Cell cell && cell.position != cell.origin)
						iMap.RemoveTileAt(cell.origin);
					iter.Deselect(this);
				}

				// Pass 2: all cells that should now be visible
				foreach (var iter in leaving)
				{
					if (iter is Cell cell)
						iMap.UpdateTileAt(cell.position, cell.variant);
				}

				// Preserve original null-when-empty convention
				_selection = newItems.Length == 0 ? null : newItems;

				// 2. Select newly added items
				foreach (var iter in newItems.Except(oldItems))
					iter.Select(this);

				// 3. Update items that were already selected and still are
				foreach (var iter in oldItems.Intersect(newItems))
					iter.Update(this);
			}
		}

		public bool IsSingleSelect => selection?.Length == 1;
		public bool IsMultiSelect => selection?.Length > 1;
		public bool HasSelection => selection?.Length > 0;
		private Cell[] selectedCells => selection?.OfType<Cell>().ToArray() ?? Array.Empty<Cell>();

		private float editAltitude = 0f;
		private Variant atlasVariant = default;
		private float ActiveWorkingPlaneHeight => workingPlaneHeight ?? editAltitude;

		// ─── Tile / Attachment state ─────────────────────────────────────────
		private enum ControllerMode
		{
			Idle,
			Evaluate,
			PlacingTile,
			SelectTile,
			DragTile,
			SelectAttachment,
			DragAttachment
		}

		private ISelectable[] GetAttachmentsAsSelectables(int index, Type[] filter = null)
			=> iMap.GetAttachments(index, filter).Cast<ISelectable>().ToArray();

		private struct PickResult
		{
			public readonly Vector3 world;
			public readonly bool hitModel;
			public readonly float planeHeight;
			public readonly int logicalIndex;

			public PickResult(Vector3 world, bool hitModel, float planeHeight, int logicalIndex)
			{
				this.world = world;
				this.hitModel = hitModel;
				this.planeHeight = planeHeight;
				this.logicalIndex = logicalIndex;
			}
		}

		private void ClearWorkingPlane()
		{
			workingPlaneHeight = null;
			beginWorldAdjustment = Vector3.zero;
		}

		private bool TryGetModelPick(out PickResult result)
		{
			result = default;
			if (_camera == null || iMap == null) return false;

			var ray = _camera.ScreenPointToRay(InputX.mousePosition);
			if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
				return false;

			var info = hit.collider.GetComponentInParent<TileColliderInfo>();
			if (info == null || info.Map != iMap)
				return false;

			var hitWorld = TileOriginShift.AdjustRaycastResult(hit.point);
			var planeHeight = hitWorld.y;
			var logicalIndex = -1;
			if (!iMap.TryGetHitTile(_camera, InputX.mousePosition, out logicalIndex, out _))
				logicalIndex = iMap.VectorToIndex(hitWorld);

			result = new PickResult(new Vector3(hitWorld.x, planeHeight, hitWorld.z), true, planeHeight, logicalIndex);
			return true;
		}

		private PickResult GetPickWorld(bool useWorkingPlaneFallback = false)
		{
			if (TryGetModelPick(out var hit))
				return hit;

			var fallbackHeight = useWorkingPlaneFallback ? ActiveWorkingPlaneHeight : editAltitude;
			var fallbackWorld = Map.ScreenToWorld(_camera, InputX.mousePosition, fallbackHeight);
			return new PickResult(fallbackWorld, false, fallbackHeight, iMap.VectorToIndex(fallbackWorld));
		}

		private int GetHitIndex(bool useWorkingPlaneFallback = false) => GetPickWorld(useWorkingPlaneFallback).logicalIndex;

		private void BeginPick(PickResult pick)
		{
			beginWorld = currentWorld;
			beginWorldAdjustment = new Vector3(pick.world.x - beginWorld.x, 0f, pick.world.z - beginWorld.z);
			workingPlaneHeight = pick.hitModel ? pick.planeHeight : null;
		}

		private ControllerMode mode = ControllerMode.Idle;
		private void SetMode(ControllerMode value) => mode = value;

		public bool CanOpenPalette() => mode == ControllerMode.Idle;

		public void OnTileSelected(HashId newHash)
		{
			atlasVariant = new Variant(newHash);
			SetMode(newHash != ResourceManager.DefaultHash ? ControllerMode.PlacingTile : ControllerMode.Idle);
		}

		public void OnAltitudeChanged(float value)
		{
			AdjustSelectionOrigin(Vector3.up * (value - editAltitude));
			editAltitude = value;
			GridLinesUtil.UpdateOffset(TileOriginShift.AdjustVisualOffset(Vector3.up * editAltitude));
		}

		private void AdjustSelectionOrigin(Vector3 delta)
		{
			if (delta == Vector3.zero) return;
			foreach (var cell in selectedCells)
				cell.ApplyDelta(this, delta, true);
		}

		// ─── Unity / lifecycle ───────────────────────────────────────────────
		private void Awake()
		{
			ReadyCallbackRegistry.RegisterFor<EditorScreenUI>(ui => ui.Receiver = this);
			ReadyCallbackRegistry.RegisterFor<TileSelector>(sel => sel.Receiver = this);
			GridLinesUtil.Initialise(transform, offset: TileOriginShift.AdjustVisualOffset(Vector3.up * editAltitude));
			OptionsPanel.onGridlinesToggle += value => GridLinesUtil.Enabled = value & isActiveAndEnabled;
		}

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += (Map map, bool resized, Vector3 originDelta) =>
			{
				ResourceManager.ApplyMapChanges(map);
				if (resized)
				{
					GridLinesUtil.UpdateSize(map.width, map.height);
					AdjustSelectionOrigin(originDelta);
				}
			};

			GridLinesUtil.UpdateSize(iMap?.Width ?? 32, iMap?.Height ?? 32);
			if (!isActiveAndEnabled) return;
			ClearSelection();
			ClearWorkingPlane();
			EditorAttachmentUI.ClearPending();
			EditorMarkerUtil.ClearMapMarkers();
			SetMode(ControllerMode.Idle);
		}

		public void Reset()
		{
			ClearSelection();
			ClearWorkingPlane();
		}

		private void OnEnable()
		{
			GetComponent<MainCameraController>()?.SelectCameraSystem(CameraModeRegistry.Editor, false);
			UIController.OpenPanel<EditorScreenUI>();

			GridLinesUtil.Enabled = ApplicationSettings.ShowEditorGrid & isActiveAndEnabled;
			ClearWorkingPlane();
			SetMode(ControllerMode.Idle);
		}

		private void OnDisable()
		{
			UIController.HidePanel<EditorScreenUI>();

			ClearSelection();
			ClearWorkingPlane();
			GridLinesUtil.Enabled = false;
			EditorAttachmentUI.ClearPending();
			EditorMarkerUtil.ClearMapMarkers();
		}

		private void Update()
		{
			if (!_camera) return;

			var mouseOverGUI = (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
				|| GUIUtility.hotControl != 0
				|| PlaceholderUI.IsMouseOverGui()
				|| EditorAttachmentUI.sidePanel.IsMouseOver;

			ViewPreviewUtil.Update();
			EditorCameraMovement.UpdateCamera(
				ViewPreviewUtil.IsInFocus ? ViewPreviewUtil.PreviewCamera : _camera,
				currentWorld,
				inFocus: !mouseOverGUI);

			if (!ViewPreviewUtil.IsInFocus && mouseOverGUI) return;
			if (IsSingleSelect && selection[0].OnGizmoInput(this)) return;
			if (ViewPreviewUtil.IsInFocus) return;

			switch (mode)
			{
				case ControllerMode.Idle:
					if (InputX.GetMouseButtonDown(0))
						SetMode(ControllerMode.Evaluate);
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
							iMap.InsertTileAt(currentWorld, new Variant(ResourceManager.DefaultHash));
					}
					break;

				case ControllerMode.Evaluate:
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
						{
							EvaluateAttachments();
							SetMode(ControllerMode.SelectAttachment);
						}
						if (InputX.GetMouseButtonHeld(0))
						{
							if (StartTileDrag())
								SetMode(ControllerMode.DragTile);
							else
							{
								EditorCameraMovement.StartPanning(currentWorld);
								SetMode(ControllerMode.Idle);
							}
						}
					}
					else
					{
						if (InputX.GetMouseButton(0))
						{
							EditorCameraMovement.StartPanning(currentWorld);
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.PlacingTile:
					var variant = MapUtils.NextVariantOnMap(iMap, currentWorld, atlasVariant);
					if (InputX.staticClick && InputX.GetMouseButtonUp(0))
						iMap.InsertTileAt(Map.FullFloorVec(currentWorld), variant);
					GhostMeshUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), variant, false);

					if (InputX.staticClick && InputX.GetMouseButtonUp(1))
					{
						GhostMeshUtil.HideGhostMesh();
						SetMode(ControllerMode.Idle);
					}
					break;

				case ControllerMode.SelectTile:
					if (InputX.GetMouseButtonDown(0))
					{
						if (StartTileDrag(InputX.GetKey(KeyCode.LeftControl) || InputX.GetKey(KeyCode.RightControl)))
							SetMode(ControllerMode.DragTile);
						else
							EditorCameraMovement.StartPanning(currentWorld);
					}
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
						{
							ClearSelection(true);
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.DragTile:
					if (InputX.GetMouseButton(0))
						UpdateTileDrag();
					if (InputX.GetMouseButtonUp(0))
						SetMode(ControllerMode.SelectTile);
					break;

				case ControllerMode.SelectAttachment:
					if (InputX.GetMouseButtonDown(0))
					{
						EditorAttachmentUI.ClearPending();
						if (StartAttachmentDrag())
							SetMode(ControllerMode.DragAttachment);
						else
							EditorCameraMovement.StartPanning(currentWorld);
					}
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
						{
							if (CancelAttachmentMode())
								SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.DragAttachment:
					if (InputX.GetMouseButton(0))
						UpdateAttachmentDrag();
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
						{
							EvaluateAttachments();
							SetMode(ControllerMode.SelectAttachment);
						}
					}
					else
					{
						if (InputX.GetMouseButtonUp(0))
							SetMode(ControllerMode.SelectAttachment);
					}
					break;
			}
		}

		private void OnGUI()
		{
			ViewPreviewUtil.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, iMap.VectorToIndex(beginWorld), selectable => SelectAttachments(selectable));
		}

		// ─── All helper methods ──────────────────────────────────────────────

		private void ClearSelection(bool crop = false)
		{
			selection = null;
			if (crop) iMap.ResizeMap(iMap.ContentBounds());
		}

		private bool StartTileDrag(bool combine = false)
		{
			lastSnap = Vector3.zero;
			var pick = GetPickWorld();
			BeginPick(pick);

			var index = pick.logicalIndex;
			if (index == -1) return false;

			var isAlreadySelected = selection?.Any(s => s is Cell c && iMap.VectorToIndex(c.position) == index) == true;

			if (isAlreadySelected)
			{
				if (combine)
				{
					selection = selection.Where(s => s is not Cell c || iMap.VectorToIndex(c.position) != index).ToArray();
					return false;
				}
				return true;
			}

			var originalMesh = iMap.GetTile(index).gameObject;
			if (null != originalMesh && false == originalMesh.activeSelf && !combine) return false;

			if (iMap.GetVariantAt(index).IsDefaultEquivalent) return false;

			if (!combine) ClearSelection();
			var tileWorld = iMap.IndexToVector(index);
			var selectionWorld = new Vector3(tileWorld.x, currentWorld.y, tileWorld.z);
			var newCell = new Cell(iMap, selectionWorld);
			selection = selection == null ? new[] { newCell } : selection.Append(newCell).ToArray();
			return true;
		}

		private void UpdateTileDrag()
		{
			var cells = selectedCells;
			if (0 == cells.Length) return;

			var snappedDelta = selection?.Any(s => s is Cell c && c.variant.HasNav) == true ?
				Map.FullFloorVec(workingCurrentWorld) - Map.FullFloorVec(beginWorkingWorld) :
				Map.HalfFloorVec(workingCurrentWorld) - Map.HalfFloorVec(beginWorkingWorld);
			snappedDelta.y = 0f;

			if (Vector3LexComparer.ApproximatelyEqual(snappedDelta, lastSnap))
				return;

			var delta = snappedDelta - lastSnap;
			lastSnap = snappedDelta;

			var oldGridPoints = cells.Select(c => new Vector2Int(Mathf.FloorToInt(c.position.x), Mathf.FloorToInt(c.position.z)));
			var oldExtents = GeomUtils.GetBoundingRect(oldGridPoints, iMap.ContentBounds());

			foreach (var cell in cells)
				cell.ApplyDelta(this, delta);

			var gridPoints = cells.Select(c => new Vector2Int(Mathf.FloorToInt(c.position.x), Mathf.FloorToInt(c.position.z)));
			var extents = GeomUtils.GetBoundingRect(gridPoints, iMap.ContentBounds());

			if (Map.ValidExtents(extents))
			{
				iMap.ResizeMap(extents);
				lastSnap -= new Vector3(extents.x, 0f, extents.y);
				if (extents != oldExtents)
					foreach (var cell in cells) cell.Update(this);
			}
			else
				foreach (var cell in cells) cell.Revert(this);
		}

		private void EvaluateAttachments()
		{
			var pick = GetPickWorld();
			BeginPick(pick);
			var cursorTile = pick.logicalIndex;
			var attachmentsOnTile = GetAttachmentsAsSelectables(index: cursorTile);
			if (EditorAttachmentUI.EvaluateSelection(attachmentsOnTile, cursorTile))
				SelectAttachments(attachmentsOnTile);
			MapUtils.RebuildMarkers(iMap, selection);
		}

		private bool StartAttachmentDrag()
		{
			var pick = GetPickWorld();
			BeginPick(pick);
			var cursorTile = pick.logicalIndex;
			if (!HasSelection || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
				SelectAttachments(GetAttachmentsAsSelectables(index: cursorTile));
			return HasSelection;
		}

		private void UpdateAttachmentDrag()
		{
			var cursorTile = GetHitIndex(useWorkingPlaneFallback: true);
			if (-1 == cursorTile) return;

			if (HasSelection && selection[0] is MapAttachment first && first.tile == cursorTile) return;

			foreach (var iter in selection ?? Array.Empty<ISelectable>())
			{
				if (iter is MapAttachment att) att.tile = cursorTile;
				iter.Update(this);
			}

			iMap.RefreshAttachments(selection.Cast<MapAttachment>().ToArray());
			MapUtils.RebuildMarkers(iMap, selection);
		}

		private bool CancelAttachmentMode()
		{
			ClearWorkingPlane();
			var currentHit = GetHitIndex();
			var ClickedOnActive = HasSelection && selection[0] is MapAttachment ma && ma.tile == currentHit;

			if (HasSelection && !ClickedOnActive)
			{
				ClearSelection();
				EditorMarkerUtil.ClearMapMarkers();
				return false;
			}

			SelectAttachments(GetAttachmentsAsSelectables(index: GetHitIndex()));
			if (HasSelection)
			{
				EditorAttachmentUI.RequestDelete();
				return false;
			}
			EditorMarkerUtil.ClearMapMarkers();
			return true;
		}

		private void SelectAttachments(ISelectable[] value = null)
		{
			selection = value;
			MapUtils.RebuildMarkers(iMap, selection);
		}
	}
}
