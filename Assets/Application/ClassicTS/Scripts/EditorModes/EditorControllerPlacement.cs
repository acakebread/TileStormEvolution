using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPlacement : EditorControllerMovement
	{
		private enum ControllerMode
		{
			Idle,       // nothing selected, no placement active → can select, pan, start placing
			Placing,    // active tile placement from palette (ghost visible)
			Selected,   // tile is highlighted/selected → can start drag or deselect
			Dragging    // actively dragging a selected tile (mouse button held)
		}

		private ControllerMode mode = ControllerMode.Idle;
		private bool decisionPending;
		private float pressTime;

		// Selection
		private Vector3 selectedMapPos;//private Vector3 mouseWorldPosStart;

		private Variant selectedVariant = new(ResourceManager.DefaultHash);
		private (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

		public EditorControllerPlacement(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();

			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector == null)
			{
				Debug.LogError("TileSelector not found!");
				return;
			}

			tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			tileSelector.CanOpenPalette = () => mode == ControllerMode.Idle;

			selectedVariant = new Variant(ResourceManager.DefaultHash);
			SetMode(ControllerMode.Idle);
		}

		public override void OnDisable()
		{
			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector != null)
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;

			DeselectTile();
			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			DeselectTile();
			selectedVariant = new Variant(newHash);
			SetMode((newHash != ResourceManager.DefaultHash) ? ControllerMode.Placing : ControllerMode.Idle);
		}

		private void SetMode(ControllerMode newMode) => mode = newMode;

		public override void Update()
		{
			base.Update();
			UpdateGhostMesh(camera, iMap, selectedVariant);// Continuous ghost update in placing mode
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);
			if (!camera) return;

			// ── Discrete input ────────────────────────────────────────────────────────────────
			switch (mode)
			{
				case ControllerMode.Idle:
					if (Input.GetMouseButtonDown(0))
					{
						var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
						if (variant.IsDefaultEquivalent())
							StartPanning(); // immediate panning on default/empty
						else
						{
							pressTime = Time.time;
							decisionPending = true; // start the select timer
						}
					}

					if (staticClick)
					{
						if (Input.GetMouseButtonUp(0))
						{
							if (decisionPending)
							{
								decisionPending = false;
								SelectTile(Map.ScreenToWorld(camera, Input.mousePosition));
							}
						}

						// check the select timer
						if (decisionPending && Time.time - pressTime >= 0.25f)
						{
							decisionPending = false;
							if (!AttemptDrag())
								StartPanning();
						}

						if (Input.GetMouseButtonUp(1))
							EditMapTile(erase: true);
					}
					else
					{
						if (Input.GetMouseButton(0) && decisionPending)
						{
							decisionPending = false;
							StartPanning(); // immediate panning when moved during hold
						}
					}
					break;

				case ControllerMode.Placing:
					if (staticClick)
					{
						if (Input.GetMouseButtonUp(0))
							EditMapTile();

						if (Input.GetMouseButtonUp(1))
						{
							DeselectTile();

							if (selectedVariant.hash == ResourceManager.DefaultHash)
								EditMapTile(erase: true);
							else
							{
								selectedVariant = new Variant(ResourceManager.DefaultHash);
								SetMode(ControllerMode.Idle);
							}
						}
					}
					break;

				case ControllerMode.Selected:
					if (staticClick)
					{
						if (Input.GetMouseButtonDown(0))
						{
							if (!AttemptDrag())
								StartPanning();
						}

						if (Input.GetMouseButtonUp(0))
						{
							if (Map.ScreenToWorldSnapped(camera, Input.mousePosition) != selectedMapPos)
								DeselectTile();
						}

						if (Input.GetMouseButtonUp(1))
							DeselectTile();
					}
					break;

				case ControllerMode.Dragging:
					if (Input.GetMouseButton(0))
						UpdateDragPosition(Map.ScreenToWorld(camera, Input.mousePosition));// HalfSnapped);
					else
						EndDrag(Map.ScreenToWorld(camera, Input.mousePosition));
					break;
			}
		}

		private bool AttemptDrag()
		{
			if (SelectTile(Map.ScreenToWorld(camera, Input.mousePosition)))
			{
				StartDrag();
				return true;
			}
			return false;
		}

		private void StartDrag()
		{
			var index = iMap.WorldToMapIndex(selectedMapPos);
			selectedVariant = iMap.GetVariantAt(index);
			SetMode(ControllerMode.Dragging);
		}

		private void UpdateDragPosition(Vector3 position)
		{
			var tile = iMap.GetTile(selectedMapPos);
			if (null != tile.gameObject)
			{
				var snapped = Map.FloorVec(selectedMapPos);
				var drag = Map.HalfFloorVec(position) - (selectedVariant.HasNav ? Map.FloorVec(selectedMapPos) : Map.HalfFloorVec(selectedMapPos));
				var world_position = snapped + drag + selectedVariant.delta;
				var fullSnapped = Map.FloorVec(world_position);
				var halfSnapped = selectedVariant.HasNav ? fullSnapped : Map.HalfFloorVec(world_position);

				tile.gameObject.transform.position = Map.HalfSnappedMapPosition(halfSnapped);
			}
		}

		private void EndDrag(Vector3 position)
		{
			var snapped = Map.FloorVec(selectedMapPos);
			var drag = Map.HalfFloorVec(position) - (selectedVariant.HasNav ? Map.FloorVec(selectedMapPos) : Map.HalfFloorVec(selectedMapPos));
			var world_position = snapped + drag + selectedVariant.delta;
			var fullSnapped = Map.FloorVec(world_position);
			var halfSnapped = selectedVariant.HasNav ? fullSnapped : Map.HalfFloorVec(world_position);

			var delta = halfSnapped - fullSnapped;
			if (fullSnapped != selectedMapPos || delta != selectedVariant.delta)
			{
				selectedVariant.delta = delta;
				iMap.RemoveTileAt(selectedMapPos); // this will destroy the gameobject on the tile so defacto remove the highlight
				var index = iMap.UpdateTileAt(fullSnapped, selectedVariant.hash, selectedVariant.delta, selectedVariant.angle);
				if (-1 == index) return;//operation failed
				fullSnapped = iMap.TileNormalisedWorldPosition(index);
			}
			else
			{
				var oldIndex = iMap.WorldToMapIndex(selectedMapPos);
				var tile = iMap.GetTile(oldIndex);
				if (tile.gameObject != null)
					tile.gameObject.transform.position = selectedMapPos + selectedVariant.delta;
			}

			SelectTile(fullSnapped);
		}

		private Vector3 FullSnapped => Map.ScreenToWorldSnapped(camera, Input.mousePosition);
		private Vector3 HalfSnapped => selectedVariant.HasNav ? FullSnapped : Map.ScreenToWorldHalfSnapped(camera, Input.mousePosition);

		private bool SelectTile(Vector3 worldPos)
		{
			var variant = iMap.GetVariantAt(worldPos);
			if (variant.IsDefaultEquivalent())
				return false;

			DeselectTile();

			var tile = iMap.GetTile(worldPos);
			if (tile.gameObject == null) return false;

			const float SELECT_TINT_BRIGHTNESS = 1.35f;
			Color SELECT_TINT = new (1.4f, 1.25f, 0.85f, 1f);

			originalRenderersState = tile.gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);

			selectedMapPos = worldPos;
			SetMode(ControllerMode.Selected);

			return true;
		}

		private void DeselectTile()
		{
			var index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);
			if (null != tile.gameObject)
				tile.gameObject.RestoreSelectionHighlight(originalRenderersState);

			originalRenderersState = null;
			SetMode(ControllerMode.Idle);
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Variant variant)
		{
			var def = ResourceManager.GetDefinition(variant.hash);
			if (mode != ControllerMode.Placing || def == null || def.IsDefaultEquivalent())
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			var snapped = Map.ScreenToWorldSnapped(cam, Input.mousePosition);
			var mapIndex = map.WorldToMapIndex(snapped);

			selectedVariant.delta = Vector3.zero;
			selectedVariant.angle = 0f;

			if (mapIndex != -1)
			{
				var current = map.GetVariantAt(mapIndex);
				var isDefault = def.IsDefaultEquivalent(); // already checked above, but kept for clarity

				if (current.hash != 0 && !isDefault && current.hash == variant.hash)
				{
					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					int aIdx = Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;
					int dIdx = Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;

					aIdx = (aIdx + 1) % angles.Length;
					if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

					selectedVariant.delta = new Vector3(current.delta.x, deltas[dIdx], current.delta.z);
					selectedVariant.angle = angles[aIdx];
				}
			}

			EditorMeshUtil.UpdateGhostMesh(variant, snapped, mapIndex == -1);
		}

		private void EditMapTile(bool erase = false) => iMap.UpdateTileAt(FullSnapped, erase ? ResourceManager.DefaultHash : selectedVariant.hash, selectedVariant.delta, selectedVariant.angle);

		public override void OnDestroy()
		{
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}