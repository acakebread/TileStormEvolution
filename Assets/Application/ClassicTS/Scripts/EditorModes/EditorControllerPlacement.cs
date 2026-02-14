using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public enum ControllerMode
	{
		Idle,
		Placing,
		Selected,
		Dragging
	}

	public class EditorControllerPlacement : EditorControllerMovement
	{
		private ControllerMode mode = ControllerMode.Idle;

		// ── Fields for delayed select vs immediate pan ───────────────────────────────
		private float pressStartTime = -1f;
		private const float DELAY_BEFORE_SELECT_AND_DRAG = 0.25f;
		private bool isDecisionPending;

		// Selection
		private Variant selectedVariant = new(ResourceManager.DefaultHash);
		private Vector3 selectedMapPos;
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
			UpdateGhostMesh(camera, iMap, selectedVariant);

			// Safety: if button released during decision window, cancel it
			if (isDecisionPending && !Input.GetMouseButton(0))
			{
				isDecisionPending = false;
				pressStartTime = -1f;
			}
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);
			if (!camera) return;

			// Handle active drag first (highest priority)
			if (mode == ControllerMode.Dragging)
			{
				if (Input.GetMouseButton(0))
				{
					UpdateDragPosition();
				}
				else
				{
					EndDrag();
				}
				return;
			}

			// ── Decision window logic ────────────────────────────────────────────────
			if (isDecisionPending)
			{
				// Movement detected → pan immediately
				if (!staticClick)
				{
					isDecisionPending = false;
					pressStartTime = -1f;
					StartPanning();
					return;
				}

				// Still static after delay → try to select + drag
				if (Time.time - pressStartTime >= DELAY_BEFORE_SELECT_AND_DRAG)
				{
					isDecisionPending = false;
					pressStartTime = -1f;

					if (!TrySelectAndStartDrag())
					{
						// No tile under cursor (or default) → pan
						StartPanning();
					}
					// If successful → mode is now Dragging, next frames will handle it
				}

				// Still waiting and static → do nothing this frame
				return;
			}

			// ── Normal discrete input ────────────────────────────────────────────────
			switch (mode)
			{
				case ControllerMode.Idle:
				case ControllerMode.Selected:
					if (staticClick)
					{
						if (Input.GetMouseButtonDown(0))
						{
							pressStartTime = Time.time;
							isDecisionPending = true;
							// Optional: visual feedback (cursor change, subtle glow, …)
						}

						if (Input.GetMouseButtonUp(1))
						{
							isDecisionPending = false;
							pressStartTime = -1f;
							EditMapTile(erase: true);
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
			}
		}

		private bool TrySelectAndStartDrag()
		{
			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			int hitIndex = iMap.WorldToMapIndex(snapped);

			if (hitIndex == -1) return false;

			var variant = iMap.GetVariantAt(hitIndex);
			if (variant.IsDefaultEquivalent()) return false;

			SelectTile(snapped);
			StartDrag();           // → sets mode = Dragging
			return true;
		}

		private void StartDrag()
		{
			int index = iMap.WorldToMapIndex(selectedMapPos);
			selectedVariant = iMap.GetVariantAt(index);
			SetMode(ControllerMode.Dragging);
		}

		private void UpdateDragPosition()
		{
			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);
			if (tile.gameObject == null) return;

			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			tile.gameObject.transform.position = snapped + selectedVariant.delta;
		}

		private void EndDrag()
		{
			var newSnapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			var shouldMove = newSnapped != selectedMapPos;

			if (shouldMove)
			{
				iMap.RemoveTileAt(selectedMapPos);
				iMap.UpdateTileAt(newSnapped, selectedVariant.hash, selectedVariant.delta, selectedVariant.angle);
			}
			else
			{
				var oldIndex = iMap.WorldToMapIndex(selectedMapPos);
				var tile = iMap.GetTile(oldIndex);
				if (tile.gameObject != null)
					tile.gameObject.transform.position = selectedMapPos;
			}

			SetMode(ControllerMode.Idle);
		}

		private void SelectTile(Vector3 worldPos)
		{
			int mapIndex = iMap.WorldToMapIndex(worldPos);

			DeselectTile();

			var tile = iMap.GetTile(mapIndex);
			if (tile.gameObject == null) return;

			const float SELECT_TINT_BRIGHTNESS = 1.35f;
			Color SELECT_TINT = new Color(1.4f, 1.25f, 0.85f, 1f);

			originalRenderersState = tile.gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);

			selectedMapPos = worldPos;
			SetMode(ControllerMode.Selected);
		}

		private void DeselectTile()
		{
			if (originalRenderersState == null) return;

			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);
			if (tile.gameObject != null)
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
				var isDefault = def.IsDefaultEquivalent();

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

		private void EditMapTile(bool erase = false)
		{
			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			iMap.UpdateTileAt(snapped,
							  erase ? ResourceManager.DefaultHash : selectedVariant.hash,
							  selectedVariant.delta,
							  selectedVariant.angle);
		}

		public override void OnDestroy()
		{
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}