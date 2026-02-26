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
		private bool holdSelect;
		private float holdTime;

		// Selection
		private Vector3 startWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(camera, InputX.mousePosition);

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
			{
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;
				tileSelector.CanOpenPalette = () => false;
			}

			DeselectTile();
			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			DeselectTile();
			selectedVariant = new Variant(newHash);
			SetMode((newHash != ResourceManager.DefaultHash) ? ControllerMode.Placing : ControllerMode.Idle);
		}

		private void SetMode(ControllerMode value) => mode = value;

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);
			if (!camera) return;

			switch (mode)
			{
				case ControllerMode.Idle:
					if (InputX.GetMouseButtonDown(0))
					{
						var variant = iMap.CameraHitVariant(camera, InputX.mousePosition);
						if (variant.IsDefaultEquivalent)
							StartPanning(); // immediate panning on default/empty
						else
						{
							holdTime = Time.time;
							holdSelect = true; // start the hold timer
						}
					}

					if (staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
						{
							if (holdSelect)
							{
								holdSelect = false;
								SelectTile(currentWorld);
							}
						}

						// check the hold timer
						if (holdSelect && Time.time - holdTime >= 0.25f)
						{
							holdSelect = false;
							if (!StartDrag())
								StartPanning();
						}

						if (InputX.GetMouseButtonUp(1))
							EditMapTile(erase: true);
					}
					else
					{
						if (InputX.GetMouseButton(0) && holdSelect)
						{
							holdSelect = false;
							StartPanning(); // immediate panning when moved during hold
						}
					}
					break;

				case ControllerMode.Placing:
					UpdateGhostMesh(camera, iMap, selectedVariant);// Continuous ghost update in placing mode

					if (staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							EditMapTile();

						if (InputX.GetMouseButtonUp(1))
						{
							DeselectTile();
							EditorMeshUtil.HideGhostMesh();
							selectedVariant = new Variant(ResourceManager.DefaultHash);
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.Selected:
					if (staticClick)
					{
						if (InputX.GetMouseButtonDown(0))
						{
							if (!StartDrag())
								StartPanning();
						}

						if (InputX.GetMouseButtonUp(1))
							DeselectTile();
					}
					break;

				case ControllerMode.Dragging:
					if (InputX.GetMouseButtonDown(0))
					{
						if (!StartDrag())
							SetMode(ControllerMode.Idle);
					}
					else if (InputX.GetMouseButton(0))
						UpdateDrag();
					else if (InputX.GetMouseButtonUp(0))
						EndDrag();
					break;
			}

			void EditMapTile(bool erase = false) => iMap.UpdateTileAt(currentWorld, erase ? ResourceManager.DefaultHash : selectedVariant.hash, selectedVariant.delta, selectedVariant.angle);
		}

		private bool StartDrag()
		{
			if (!SelectTile(currentWorld))
				return false;
			var index = iMap.VectorToIndex(startWorld);
			selectedVariant = iMap.GetVariantAt(index);
			SetMode(ControllerMode.Dragging);
			return true;
		}

		private void UpdateDrag()
		{
			var tile = iMap.GetTile(startWorld);

			if (null == tile.gameObject) return;

			var worldPos = Map.FullFloorVec(startWorld) + currentWorld - startWorld + selectedVariant.delta;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = selectedVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;//the future selectedVariant.delta

			tile.gameObject.transform.position = Map.WorldToRender(snapped) + delta;//update selection visual
		}

		private void EndDrag()
		{
			var worldPos = Map.FullFloorVec(startWorld) + currentWorld - startWorld + selectedVariant.delta;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = selectedVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;//the future selectedVariant.delta

			if (snapped == Map.FullFloorVec(startWorld) && delta == selectedVariant.delta) return;//no change so ok to just exit

			selectedVariant.delta = delta;
			iMap.RemoveTileAt(startWorld); // this will destroy the gameobject on the tile so defacto remove the highlight
			var index = iMap.UpdateTileAt(snapped, selectedVariant);
			if (-1 == index) return;//operation failed
			SelectTile(iMap.IndexToVector(index));
			//SetMode(ControllerMode.Idle);
		}

		private bool SelectTile(Vector3 worldPos)
		{
			DeselectTile();

			var variant = iMap.GetVariantAt(worldPos);
			if (variant.IsDefaultEquivalent)
				return false;

			var tile = iMap.GetTile(worldPos);
			if (tile.gameObject == null) return false;

			Color SELECT_TINT = new (1.4f, 1.25f, 0.85f, 1f);
			const float SELECT_TINT_BRIGHTNESS = 1.35f;

			originalRenderersState = tile.gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);

			startWorld = variant.HasNav ? Map.FullFloorVec(worldPos) : Map.HalfFloorVec(worldPos);
			SetMode(ControllerMode.Selected);

			return true;
		}

		private void DeselectTile()
		{
			var index = iMap.VectorToIndex(startWorld);
			var tile = iMap.GetTile(index);
			if (null != tile.gameObject)
				tile.gameObject.RestoreSelectionHighlight(originalRenderersState);

			originalRenderersState = null;
			SetMode(ControllerMode.Idle);
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Variant variant)
		{
			var def = ResourceManager.GetDefinition(variant.hash);
			if (def == null || def.IsDefaultEquivalent())
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			var snapped = Map.WorldToRender(Map.FullFloorVec(Map.ScreenToWorld(cam, InputX.mousePosition)));
			var mapIndex = map.VectorToIndex(snapped);

			selectedVariant.delta = Vector3.zero;
			selectedVariant.angle = 0f;

			if (mapIndex != -1)
			{
				var current = map.GetVariantAt(mapIndex);

				if (current.hash != 0 && current.hash == variant.hash)
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

		public override void OnDestroy()
		{
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}