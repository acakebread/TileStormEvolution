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

		// Delayed select vs pan decision
		private bool decisionPending;
		private float pressTime;
		private const float DELAY_BEFORE_DRAG = 0.25f;

		// Selection state
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
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);
			if (!camera) return;

			// Active drag takes priority
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

			switch (mode)
			{
				case ControllerMode.Idle:
				case ControllerMode.Selected:

					if (Input.GetMouseButtonDown(0))
					{
						pressTime = Time.time;
						decisionPending = true;
					}

					// ── Quick static release → just select ───────────────────────
					else if (Input.GetMouseButtonUp(0))
					{
						if (decisionPending)
						{
							decisionPending = false;

							// Quick click: select now, no drag
							var pos = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
							int idx = iMap.WorldToMapIndex(pos);

							if (idx != -1)
							{
								var v = iMap.GetVariantAt(idx);
								if (!v.IsDefaultEquivalent())
								{
									// Different tile or no selection yet → select it
									if (mode != ControllerMode.Selected || selectedMapPos != pos)
									{
										SelectTile(pos);
									}
									// same tile already selected → nothing (or you could toggle/deselect)
								}
								else
								{
									DeselectTile();
								}
							}
							else
							{
								DeselectTile();
							}
						}
					}

					// ── Long static hold → select + drag ─────────────────────────
					else if (decisionPending && staticClick && Time.time - pressTime >= DELAY_BEFORE_DRAG)
					{
						decisionPending = false;

						var pos = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
						int idx = iMap.WorldToMapIndex(pos);

						if (idx != -1)
						{
							var v = iMap.GetVariantAt(idx);
							if (!v.IsDefaultEquivalent())
							{
								if (mode != ControllerMode.Selected || selectedMapPos != pos)
								{
									SelectTile(pos);
								}
								StartDrag();   // ← this is the only place we enter Dragging
							}
							else
							{
								DeselectTile();
								StartPanning();
							}
						}
						else
						{
							StartPanning();
						}
					}

					// ── Moved during hold → pan immediately ───────────────────────
					else if (decisionPending && !staticClick)
					{
						decisionPending = false;
						StartPanning();
					}

					// Right click
					if (Input.GetMouseButtonUp(1))
					{
						decisionPending = false;
						EditMapTile(erase: true);
					}

					break;

				case ControllerMode.Placing:
					// your existing placing code, unchanged
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
		private void TrySelect(bool startDrag)
		{
			var pos = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			int idx = iMap.WorldToMapIndex(pos);

			if (idx == -1 || iMap.GetVariantAt(idx).IsDefaultEquivalent())
			{
				DeselectTile();
				return;
			}

			// Already selected this exact position → no-op
			if (mode == ControllerMode.Selected && selectedMapPos == pos)
			{
				if (startDrag) StartDrag();
				return;
			}

			SelectTile(pos);

			if (startDrag)
				StartDrag();
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