using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private HashId selectedHashId;
		private float previewAngle = 0f;
		private Vector3 previewDelta = new Vector3();

		// ── Tile selection state ───────────────────────────────────────────────
		private int? selectedMapIndex = null;
		private Vector3 originalTileScale;
		private (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

		private const float SELECTED_SCALE_FACTOR = 1.12f;
		private const float SELECT_TINT_BRIGHTNESS = 1.35f;
		private static readonly Color SELECT_TINT = new Color(1.4f, 1.25f, 0.85f, 1f); // warm selection tint

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();

			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector == null)
			{
				Debug.LogError("TileSelector not found in scene!");
				return;
			}

			tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			tileSelector.CanOpenPalette = () => selectedHashId == ResourceManager.DefaultHash;

			selectedHashId = ResourceManager.DefaultHash;
			DeselectTile(); // ensure clean state
		}

		public override void OnDisable()
		{
			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector != null)
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;

			DeselectTile(); // restore visuals when mode changes

			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			selectedHashId = newHash;
			previewAngle = 0f;
			previewDelta = Vector3.zero;

			// Picking from palette → clear any map tile selection
			DeselectTile();
		}

		public override void Update()
		{
			base.Update();

			// When tile is selected → no ghost mesh
			if (selectedHashId == ResourceManager.DefaultHash && selectedMapIndex.HasValue)
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			// Normal placement preview
			var def = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, def);
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);

			if (!camera) return;

			if (Input.GetMouseButtonDown(0))
			{
				var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
				var selDef = ResourceManager.GetDefinition(variant.hash);
				var isDefault = selDef?.IsDefaultEquivalent() ?? true;
				if (isDefault)
					StartPanning();
			}

			if (!staticClick) return;

			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			int hitIndex = iMap.WorldToMapIndex(snapped);

			// ── Left mouse UP ──────────────────────────────────────────────────
			if (Input.GetMouseButtonUp(0))
			{
				if (selectedHashId == ResourceManager.DefaultHash)
				{
					// Selection / inspection mode
					if (hitIndex == -1)
					{
						DeselectTile();
						return;
					}

					var currentVariant = iMap.GetVariantAt(hitIndex);
					var currentDef = ResourceManager.GetDefinition(currentVariant.hash);

					if (currentDef != null && !currentDef.IsDefaultEquivalent())
					{
						SelectTile(hitIndex);
						return; // do NOT place / paint
					}
					else
					{
						DeselectTile();
					}
				}
				else
				{
					// Normal painting mode
					EditMapTile();
				}
			}

			// ── Right mouse UP ─────────────────────────────────────────────────
			if (Input.GetMouseButtonUp(1))
			{
				if (selectedHashId == ResourceManager.DefaultHash)
				{
					// In selection mode: right-click deselects
					DeselectTile();
					// Optional aggressive erase: EditMapTile(erase: true);
				}
				else
				{
					// Normal mode: reset to default tile type
					selectedHashId = ResourceManager.DefaultHash;
					previewAngle = 0f;
					previewDelta = Vector3.zero;
					DeselectTile();
				}
			}
		}

		// ───────────────────────────────────────────────────────────────────────
		// Selection visual feedback (multi-renderer / multi-material safe)
		// ───────────────────────────────────────────────────────────────────────

		private void SelectTile(int mapIndex)
		{
			if (mapIndex == selectedMapIndex) return;

			DeselectTile();

			var tile = iMap.GetTile(mapIndex);
			if (tile.gameObject == null) return;

			selectedMapIndex = mapIndex;

			var allRenderers = tile.gameObject.CollectAllRenderers(true);

			if (allRenderers.Length == 0) return;

			originalRenderersState = new (Renderer renderer, Material[] originalMaterials)?[allRenderers.Length];

			for (int i = 0; i < allRenderers.Length; i++)
			{
				var rend = allRenderers[i];
				if (rend == null) continue;

				var originals = rend.sharedMaterials;
				if (originals == null || originals.Length == 0) continue;

				// Backup
				originalRenderersState[i] = (rend, (Material[])originals.Clone());

				// Create tinted copies
				var tinted = new Material[originals.Length];
				for (int m = 0; m < originals.Length; m++)
				{
					if (originals[m] == null)
					{
						tinted[m] = null;
						continue;
					}

					var copy = new Material(originals[m]);
					copy.color = originals[m].color * SELECT_TINT * SELECT_TINT_BRIGHTNESS;

					// Optional: boost emission if material supports it
					// if (copy.HasProperty("_EmissionColor"))
					//     copy.SetColor("_EmissionColor", copy.GetColor("_EmissionColor") * 1.8f);

					tinted[m] = copy;
				}

				rend.materials = tinted;
			}

			// Slight scale bump on root transform
			originalTileScale = tile.gameObject.transform.localScale;
			tile.gameObject.transform.localScale = originalTileScale * SELECTED_SCALE_FACTOR;

			Debug.Log($"Tile selected at map index {mapIndex} – {allRenderers.Length} renderers affected");
		}

		private void DeselectTile()
		{
			if (!selectedMapIndex.HasValue) return;

			var tile = iMap.GetTile(selectedMapIndex.Value);
			if (tile.gameObject == null || originalRenderersState == null)
			{
				selectedMapIndex = null;
				return;
			}

			var allRenderers = tile.gameObject.CollectAllRenderers(true);

			for (int i = 0; i < originalRenderersState.Length && i < allRenderers.Length; i++)
			{
				var backup = originalRenderersState[i];
				if (!backup.HasValue) continue;

				var (expectedRend, originalMats) = backup.Value;
				var currentRend = allRenderers[i];

				if (currentRend != expectedRend || originalMats == null) continue;

				currentRend.materials = originalMats;
			}

			// Restore scale
			tile.gameObject.transform.localScale = originalTileScale;

			originalRenderersState = null;
			selectedMapIndex = null;

			Debug.Log("Tile deselected – visuals restored");
		}

		// ───────────────────────────────────────────────────────────────────────
		// Existing ghost + painting logic (unchanged)
		// ───────────────────────────────────────────────────────────────────────

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Definition def)
		{
			if (def == null)
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			var worldPos = Map.ScreenToWorld(cam, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			var mapIndex = map.WorldToMapIndex(snapped);

			previewAngle = 0f;
			previewDelta = Vector3.zero;

			if (mapIndex != -1)
			{
				var current = map.GetVariantAt(mapIndex);
				var selDef = ResourceManager.GetDefinition(selectedHashId);
				bool isDefault = selDef?.IsDefault() ?? false;

				if (current.hash != 0 && !isDefault && current.hash == selectedHashId)
				{
					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					int aIdx = Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;
					int dIdx = Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;

					aIdx = (aIdx + 1) % angles.Length;
					if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

					previewAngle = angles[aIdx];
					previewDelta = new Vector3(current.delta.x, deltas[dIdx], current.delta.z);
				}
			}

			bool oob = mapIndex == -1;
			var variant = new Variant(selectedHashId, previewDelta, previewAngle);
			EditorMeshUtil.UpdateGhostMesh(variant, snapped, oob);
		}

		private void EditMapTile(bool erase = false)
		{
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			int idx = iMap.WorldToMapIndex(snapped);

			if (idx == -1 || erase)
			{
				var hash = erase ? ResourceManager.DefaultHash : selectedHashId;
				iMap.UpdateTileAt(snapped, hash, Vector3.zero, 0f);
				return;
			}

			iMap.UpdateTileAt(snapped, selectedHashId, previewDelta, previewAngle);
		}

		public override void OnDestroy()
		{
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}