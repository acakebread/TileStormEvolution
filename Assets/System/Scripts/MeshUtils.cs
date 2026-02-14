using UnityEngine;

namespace MassiveHadronLtd
{
	// =========================================================================
	// Mesh utility methods - especially useful for runtime mesh manipulation
	// =========================================================================
	public static class MeshUtils
	{
		public static Mesh CreateWritableMeshCopyOrFallback(
			Mesh source,
			string nameSuffix = "_Writable",
			bool fallbackToQuadIfFailed = true)
		{
			if (source == null)
			{
				Debug.LogError("Source mesh is null");
				return fallbackToQuadIfFailed ? GenerateQuadXZ(1f) : null;
			}

			if (source.isReadable)
			{
				// Preferred: Bake via Skinned if skinned, else simple instantiate
				var tempGO = new GameObject("TempBaker") { hideFlags = HideFlags.HideAndDontSave };
				try
				{
					var tempSMR = tempGO.AddComponent<SkinnedMeshRenderer>();
					tempSMR.sharedMesh = source;

					var writable = new Mesh { name = source.name + nameSuffix };
					tempSMR.BakeMesh(writable, true);  // true = use scale

					if (writable.vertexCount > 0)
					{
						writable.MarkDynamic();
						writable.RecalculateBounds();
						Debug.Log($"Writable copy succeeded via BakeMesh ({writable.vertexCount} verts)");
						return writable;
					}
				}
				finally
				{
					UnityEngine.Object.DestroyImmediate(tempGO);
				}

				// Fallback for non-skinned readable meshes
				var simpleCopy = UnityEngine.Object.Instantiate(source);
				simpleCopy.name += nameSuffix;
				simpleCopy.MarkDynamic();
				return simpleCopy;
			}

			// Non-readable: cannot clone → fallback or error
			Debug.LogError($"Cannot create writable runtime copy of '{source.name}' — Read/Write is disabled. " +
						   "This fails in WebGL builds (and sometimes other platforms). " +
						   "Unity strips CPU data. Use procedural fallback or enable Read/Write on asset.");

			if (fallbackToQuadIfFailed)
			{
				var fallback = GenerateQuadXZ(1f); // or your oriented quad, plane, etc.
				fallback.name = source.name + nameSuffix + "_Fallback";
				return fallback;
			}

			return null;
		}
		/// <summary>
		/// Creates a fully readable/writable copy of a mesh using SkinnedMeshRenderer + BakeMesh.
		/// Most reliable method when original mesh has Read/Write disabled in import settings.
		/// </summary>
		public static Mesh CreateReadableCopyViaBake(Mesh original, string nameSuffix = "_RuntimeBaked")
		{
			if (original == null) return null;

			var temp = new GameObject("BakeHelper")
			{
				hideFlags = HideFlags.HideAndDontSave
			};

			try
			{
				var skinned = temp.AddComponent<SkinnedMeshRenderer>();
				skinned.sharedMesh = original;

				var baked = new Mesh();
				skinned.BakeMesh(baked, true); // true = use skinning (but works even without bones)

				baked.name = (original.name ?? "Mesh") + nameSuffix;
				baked.MarkDynamic();           // Hint: this mesh will be frequently updated

				baked.RecalculateBounds();     // Just in case
											   // baked.RecalculateNormals(); // Usually not needed after BakeMesh
											   // baked.RecalculateTangents(); // Uncomment if you need normal maps

				return baked;
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(temp);
			}
		}

		/// <summary>
		/// Alternative (lighter) method using CombineMeshes.
		/// Works in some cases, but less reliable than BakeMesh method.
		/// </summary>
		public static Mesh CreateReadableCopyViaCombine(Mesh original, string nameSuffix = "_RuntimeCopy")
		{
			if (original == null) return null;

			var copy = new Mesh { name = (original.name ?? "Mesh") + nameSuffix };

			var tempGO = new GameObject("TempMeshCopier")
			{
				hideFlags = HideFlags.HideAndDontSave
			};

			try
			{
				var tempFilter = tempGO.AddComponent<MeshFilter>();
				tempFilter.sharedMesh = original;

				tempGO.AddComponent<MeshRenderer>();

				var combine = new CombineInstance[1];
				combine[0].mesh = original;
				combine[0].transform = Matrix4x4.identity;

				copy.CombineMeshes(combine, true, false, false);

				copy.MarkDynamic();
				copy.RecalculateBounds();
				copy.RecalculateNormals();     // Often needed after CombineMeshes

				return copy;
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(tempGO);
			}
		}

		public static Mesh GenerateQuadXZ(float size = 1f, float uv_scale = 1f, string name = "Quad (default)")
		{
			var half = size;
			var mesh = new Mesh
			{
				name = "PreviewGroundMesh",
				vertices = new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half), },
				triangles = new[] { 0, 1, 2, 0, 2, 3 },
				uv = new[] { new Vector2(0, 0), new Vector2(0, uv_scale), new Vector2(uv_scale, uv_scale), new Vector2(uv_scale, 0) }
			};

			mesh.RecalculateNormals();
			return mesh;
		}

		/// <summary>
		/// Creates a quad facing in the direction of the given normal.
		/// 
		/// - If rightDirection is provided → used as the "right" edge direction
		/// - If rightDirection is NOT provided → tries to choose a sensible "right" vector
		///   perpendicular to normal (prefers world axes when possible)
		/// </summary>
		/// <param name="size">Full side length of the square quad</param>
		/// <param name="normal">Direction the quad should face (will be normalized)</param>
		/// <param name="rightDirection">Optional: explicit "right" direction (will be orthogonalized to normal)</param>
		/// <param name="uvScale">UV tiling scale</param>
		/// <param name="name">Name for the generated mesh</param>
		/// <returns>Quad mesh facing the requested direction</returns>
		public static Mesh GenerateOrientedQuad(
			float size = 1f,
			Vector3 normal = default,
			Vector3? rightDirection = null,
			float uvScale = 1f,
			string name = "OrientedQuad")
		{
			// Default to XY plane (facing +Z) if no normal is given
			if (normal == default)
			{
				normal = Vector3.forward;  // Z+ direction → classic 2D XY quad
			}

			normal = normal.normalized;

			// Determine right vector
			Vector3 right;

			if (rightDirection.HasValue && rightDirection.Value != Vector3.zero)
			{
				// Use provided direction, but make sure it's perpendicular to normal
				right = Vector3.ProjectOnPlane(rightDirection.Value, normal).normalized;
				if (right == Vector3.zero)
				{
					// If provided vector was parallel to normal → fallback
					right = GetDefaultRightVector(normal);
				}
			}
			else
			{
				// Auto choose a good perpendicular vector (tries to align with world axes)
				right = GetDefaultRightVector(normal);
			}

			Vector3 up = Vector3.Cross(right, normal); // consistent up direction

			float half = size * 0.5f;

			Vector3 center = Vector3.zero; // quad centered at origin

			Vector3[] vertices = new Vector3[4]
			{
				center - right * half - up * half,   // bottom-left
				center - right * half + up * half,   // top-left
				center + right * half + up * half,   // top-right
				center + right * half - up * half,   // bottom-right
			};

			int[] triangles = new[] { 0, 1, 2, 0, 2, 3 };

			Vector2[] uvs = new Vector2[4]
			{
				new Vector2(0,        0),
				new Vector2(0,        uvScale),
				new Vector2(uvScale,  uvScale),
				new Vector2(uvScale,  0)
			};

			var mesh = new Mesh
			{
				name = name,
				vertices = vertices,
				triangles = triangles,
				uv = uvs
			};

			mesh.RecalculateNormals();
			// mesh.RecalculateTangents();  // only if you need normal maps

			return mesh;
		}

		/// <summary>
		/// Helper: Picks a reasonable "right" vector perpendicular to the normal
		/// Tries to stay aligned with world axes when possible
		/// </summary>
		private static Vector3 GetDefaultRightVector(Vector3 normal)
		{
			normal = normal.normalized;

			// Try to use world axes that are least parallel to normal
			Vector3[] candidates = new[]
			{
				Vector3.right,
				Vector3.up,
				Vector3.forward
			};

			Vector3 best = Vector3.right;
			float maxDot = -1f;

			foreach (var candidate in candidates)
			{
				float dot = Mathf.Abs(Vector3.Dot(normal, candidate));
				if (dot < maxDot) continue;

				maxDot = dot;
				best = candidate;
			}

			// Project onto plane perpendicular to normal
			return Vector3.ProjectOnPlane(best, normal).normalized;
		}

		public static Renderer[] CollectAllRenderers(this GameObject root, bool includeInactive = true)
		{
			if (root == null) return System.Array.Empty<Renderer>();
			return root.GetComponentsInChildren<Renderer>(includeInactive);
		}

		public static void SetAllMaterials(
			this GameObject root,
			Material overrideMaterial,
			bool includeInactive = true,
			System.Action<Material> modifyCopy = null)   // optional: let caller tint / set properties
		{
			if (root == null || overrideMaterial == null) return;

			var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);

			int count = 0;

			foreach (var rend in renderers)
			{
				if (rend == null) continue;

				var original = rend.sharedMaterials;
				if (original == null || original.Length == 0)
				{
					rend.material = overrideMaterial;
					count++;
					continue;
				}

				var replacementArray = new Material[original.Length];

				for (int i = 0; i < original.Length; i++)
				{
					if (original[i] == null)
					{
						replacementArray[i] = null;
						continue;
					}

					var copy = new Material(overrideMaterial);

					modifyCopy?.Invoke(copy);

					replacementArray[i] = copy;
				}

				rend.materials = replacementArray;
				count++;
			}
		}


		/// <summary>
		/// Applies a tinted highlight to all renderers under this GameObject by creating copies
		/// of their current materials and multiplying the .color property (exactly like the original working code).
		/// Returns backups for later restoration.
		/// </summary>
		public static (Renderer renderer, Material[] originalMaterials)?[] ApplySelectionHighlight(
			this GameObject root,
			Color tintMultiplier,
			float brightnessMultiplier = 1.35f,
			bool includeInactive = true)
		{
			if (root == null) return null;

			var allRenderers = root.GetComponentsInChildren<Renderer>(includeInactive);
			if (allRenderers.Length == 0) return null;

			var backups = new (Renderer renderer, Material[] originalMaterials)?[allRenderers.Length];

			for (int i = 0; i < allRenderers.Length; i++)
			{
				var rend = allRenderers[i];
				if (rend == null) continue;

				var originals = rend.sharedMaterials;
				if (originals == null || originals.Length == 0) continue;

				backups[i] = (rend, (Material[])originals.Clone());

				var tinted = new Material[originals.Length];
				for (int m = 0; m < originals.Length; m++)
				{
					if (originals[m] == null)
					{
						tinted[m] = null;
						continue;
					}

					var copy = new Material(originals[m]);
					copy.color = originals[m].color * tintMultiplier * brightnessMultiplier;
					tinted[m] = copy;
				}

				rend.materials = tinted;
			}

			return backups;
		}

		/// <summary>
		/// Restores materials from the backup array created by ApplySelectionHighlight.
		/// Matches renderers by reference and index order — safe and exact to original logic.
		/// </summary>
		public static void RestoreSelectionHighlight(
			this GameObject root,
			(Renderer renderer, Material[] originalMaterials)?[] backups,
			bool includeInactive = true)
		{
			if (root == null || backups == null) return;

			var allRenderers = root.GetComponentsInChildren<Renderer>(includeInactive);

			for (int i = 0; i < backups.Length && i < allRenderers.Length; i++)
			{
				var backup = backups[i];
				if (!backup.HasValue) continue;

				var (expectedRenderer, originalMats) = backup.Value;
				if (expectedRenderer == null || originalMats == null) continue;

				if (allRenderers[i] == expectedRenderer)
				{
					allRenderers[i].materials = originalMats;
				}
			}
		}
	}
}
