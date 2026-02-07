using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _rt;

		private static Mesh _gridMesh;
		private static Material _quadMaterial;
		private static Texture2D _quadTexture;

		private static Mesh _outlineMesh;
		private static Material _outlineMaterial;
		private static Texture2D _outlineTexture;

		private static int _lastColumns = -1;
		private static int _lastRows = -1;
		private static Vector2 _lastCoord = new Vector2(-999f, -999f);
		private static Vector2 _lastOutlineCoord = new Vector2(-999f, -999f);

		// Temporary storage during rebuild — logical centers before final projection
		private static Vector2[] _logicalQuadCenters;  // one per quad

		public static void SetTexture(Texture2D value) => _quadTexture = value;

		public static void SetOutlineTexture(Texture2D outlineTex)
		{
			_outlineTexture = outlineTex;
			if (_outlineTexture != null)
				_outlineTexture.filterMode = FilterMode.Bilinear;
		}

		public static void SetOutlineMaterial(Material value)
		{
			_outlineMaterial = value;
			_outlineTexture = (Texture2D)value.mainTexture;
		}

		private static void LazyInitResources()
		{
			if (_quadMaterial == null)
			{
				var shader = Shader.Find("Sprites/Default")
							 ?? Shader.Find("Hidden/Internal-Colored")
							 ?? Shader.Find("Particles/Standard Unlit")
							 ?? Shader.Find("Universal Render Pipeline/Unlit");

				if (shader == null)
				{
					Debug.LogError("[ScreenSpaceUtil] No usable shader found!");
					return;
				}

				_quadMaterial = new Material(shader);
			}

			if (_quadTexture == null)
			{
				_quadTexture = TextureUtils.GeneratePerlinNoiseTexture();
				if (_quadTexture != null) _quadTexture.filterMode = FilterMode.Point;
			}

			if (_outlineMaterial == null)
			{
				var shader = Shader.Find("Sprites/Default")
							 ?? Shader.Find("Hidden/Internal-Colored")
							 ?? Shader.Find("Particles/Standard Unlit")
							 ?? Shader.Find("Universal Render Pipeline/Unlit");

				if (shader == null)
				{
					Debug.LogError("[ScreenSpaceUtil] No usable shader found!");
					return;
				}

				_outlineMaterial = new Material(shader);
			}

			if (_outlineTexture == null)
			{
				_outlineTexture = TextureUtils.GenerateXorTexture256();
				if (_outlineTexture != null) _outlineTexture.filterMode = FilterMode.Point;
			}
		}

		private static void RebuildGridMeshIfNeeded(int numColumns, int numRows, Vector2 center)
		{
			if (numColumns < 1) numColumns = 1;
			if (numRows < 1) numRows = 1;

			var needsRebuild = _gridMesh == null ||
							   _lastColumns != numColumns ||
							   _lastRows != numRows ||
							   Vector2.Distance(_lastCoord, center) > 0.0005f;

			if (!needsRebuild) return;

			if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);

			_gridMesh = new Mesh { name = $"DeformGrid_{numColumns}x{numRows}" };

			var totalCells = numColumns * numRows;
			var totalVerts = totalCells * 4;
			var totalIndices = totalCells * 6;

			var vertices = new Vector3[totalVerts];
			var uvs = new Vector2[totalVerts];
			var colors = new Color[totalVerts];
			var indices = new int[totalIndices];

			var cellWidth = 1f;
			var cellHeight = 1f;
			var uvScaleX = 1f / numColumns;
			var uvScaleY = 1f / numRows;

			var centerLogical = new Vector2(center.x / uvScaleX, center.y / uvScaleY);
			var falloffRadius = 0.3f / uvScaleY;
			var maxDisplacement = 1.25f;

			_logicalQuadCenters = new Vector2[totalCells];  // reset

			// Phase 1: Build deformed quads + store logical center
			for (var qy = 0; qy < numRows; qy++)
			{
				for (var qx = 0; qx < numColumns; qx++)
				{
					var qIdx = qy * numColumns + qx;
					var x0 = qx * cellWidth;
					var y0 = qy * cellHeight;
					var x1 = x0 + cellWidth;
					var y1 = y0 + cellHeight;

					Vector2[] corners = { new(x0, y0), new(x0, y1), new(x1, y1), new(x1, y0) };

					var d0 = (new Vector2(x0, y0) - centerLogical).sqrMagnitude;
					var d1 = (new Vector2(x0, y1) - centerLogical).sqrMagnitude;
					var d2 = (new Vector2(x1, y1) - centerLogical).sqrMagnitude;
					var d3 = (new Vector2(x1, y0) - centerLogical).sqrMagnitude;

					var quadCenter = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
					_logicalQuadCenters[qIdx] = quadCenter;  // save pre-deformation center

					var centreDelta = quadCenter - centerLogical;
					var centreDist = centreDelta.magnitude;
					var centreDir = centreDist > 1e-6f ? centreDelta.normalized : Vector2.zero;

					var dt = (d0 + d1 + d2 + d3) * 0.25f;
					var sqrRadius = falloffRadius * falloffRadius;
					var scaleStrength = dt < sqrRadius ? 1f - dt / sqrRadius : 0f;
					scaleStrength *= scaleStrength;

					var scaleX = 1f + scaleStrength * maxDisplacement;
					var scaleY = 1f + scaleStrength * maxDisplacement;

					int baseVert = qIdx * 4;

					for (int i = 0; i < 4; i++)
					{
						var pos = corners[i];
						var delta = pos - centerLogical;
						vertices[baseVert] = centerLogical + new Vector2(delta.x * scaleX, delta.y * scaleY);
						uvs[baseVert] = new Vector2(pos.x * uvScaleX, pos.y * uvScaleY);
						colors[baseVert] = Color.white;
						baseVert++;
					}
				}
			}

			// Phase 2: Snap quads to squares (vertices updated in-place)
			for (var qy = 0; qy < numRows; qy++)
			{
				for (var qx = 0; qx < numColumns; qx++)
				{
					var vertIdx = (qy * numColumns + qx) * 4;

					var bl = vertices[vertIdx + 0];
					var tl = vertices[vertIdx + 1];
					var tr = vertices[vertIdx + 2];
					var br = vertices[vertIdx + 3];

					var left = Mathf.Min(bl.x, tl.x);
					var right = Mathf.Max(tr.x, br.x);
					var bottom = Mathf.Min(bl.y, br.y);
					var top = Mathf.Max(tl.y, tr.y);

					var side = Mathf.Max(right - left, top - bottom);
					var cx = (left + right) * 0.5f;
					var cy = (bottom + top) * 0.5f;

					left = cx - side * 0.5f;
					right = cx + side * 0.5f;
					bottom = cy - side * 0.5f;
					top = cy + side * 0.5f;

					vertices[vertIdx + 0] = new Vector3(left, bottom, 0f);
					vertices[vertIdx + 1] = new Vector3(left, top, 0f);
					vertices[vertIdx + 2] = new Vector3(right, top, 0f);
					vertices[vertIdx + 3] = new Vector3(right, bottom, 0f);
				}
			}

			// Phase 3: Sort quads by size ascending
			var quads = new System.Collections.Generic.List<(float area, int baseVert, int originalIdx)>(totalCells);
			for (var q = 0; q < totalCells; q++)
			{
				var baseVert = q * 4;
				var bl = vertices[baseVert + 0];
				var tr = vertices[baseVert + 2];
				var area = (tr.x - bl.x) * (tr.y - bl.y);
				quads.Add((area, baseVert, q));  // keep original index
			}
			quads.Sort((a, b) => a.area.CompareTo(b.area));

			var sortedVerts = new Vector3[vertices.Length];
			var sortedUVs = new Vector2[uvs.Length];
			var sortedColors = new Color[colors.Length];
			var sortedIndices = new int[totalIndices];

			for (var q = 0; q < quads.Count; q++)
			{
				var oldBase = quads[q].baseVert;
				var newBase = q * 4;

				for (var i = 0; i < 4; i++)
				{
					sortedVerts[newBase + i] = vertices[oldBase + i];
					sortedUVs[newBase + i] = uvs[oldBase + i];
					sortedColors[newBase + i] = colors[oldBase + i];
				}

				int idxOffset = q * 6;
				sortedIndices[idxOffset + 0] = newBase + 0;
				sortedIndices[idxOffset + 1] = newBase + 1;
				sortedIndices[idxOffset + 2] = newBase + 2;
				sortedIndices[idxOffset + 3] = newBase + 0;
				sortedIndices[idxOffset + 4] = newBase + 2;
				sortedIndices[idxOffset + 5] = newBase + 3;
			}

			// Phase 4: Project to final space
			for (var n = 0; n < sortedVerts.Length; n++)
			{
				sortedVerts[n] = new Vector3(sortedVerts[n].x * uvScaleX, sortedVerts[n].y * uvScaleY, sortedVerts[n].z);
			}

			_gridMesh.vertices = sortedVerts;
			_gridMesh.uv = sortedUVs;
			_gridMesh.colors = sortedColors;
			_gridMesh.triangles = sortedIndices;

			_lastColumns = numColumns;
			_lastRows = numRows;
			_lastCoord = center;
		}

		private static void RebuildOutlineMeshIfNeeded(int numColumns, int numRows, Vector2 coord)
		{
			bool needsRebuild = _outlineMesh == null ||
								Vector2.Distance(_lastOutlineCoord, coord) > 0.0005f ||
								_lastColumns != numColumns ||
								_lastRows != numRows;

			if (!needsRebuild) return;

			if (_outlineMesh != null)
			{
				Object.DestroyImmediate(_outlineMesh);
				_outlineMesh = null;
			}

			if (_logicalQuadCenters == null || _logicalQuadCenters.Length == 0) return;

			var centerLogical = new Vector2(coord.x * numColumns, coord.y * numRows);

			int qx = Mathf.FloorToInt(centerLogical.x);
			int qy = Mathf.FloorToInt(centerLogical.y);

			if (qx < 0 || qx >= numColumns || qy < 0 || qy >= numRows) return;

			int closestOriginalIdx = qy * numColumns + qx;

			// Compute deformed center for matching
			float cellWidth = 1f;
			float cellHeight = 1f;
			float uvScaleX = 1f / numColumns;
			float uvScaleY = 1f / numRows;
			float falloffRadius = 0.3f / uvScaleY;
			float maxDisplacement = 1.25f;

			float x0 = qx * cellWidth;
			float y0 = qy * cellHeight;
			float x1 = x0 + cellWidth;
			float y1 = y0 + cellHeight;

			float d0 = (new Vector2(x0, y0) - centerLogical).sqrMagnitude;
			float d1 = (new Vector2(x0, y1) - centerLogical).sqrMagnitude;
			float d2 = (new Vector2(x1, y1) - centerLogical).sqrMagnitude;
			float d3 = (new Vector2(x1, y0) - centerLogical).sqrMagnitude;

			float dt = (d0 + d1 + d2 + d3) * 0.25f;
			float sqrRadius = falloffRadius * falloffRadius;
			float scaleStrength = dt < sqrRadius ? 1f - dt / sqrRadius : 0f;
			scaleStrength *= scaleStrength;

			float scale = 1f + scaleStrength * maxDisplacement;

			Vector2 quadCenter = _logicalQuadCenters[closestOriginalIdx];
			Vector2 deformedQuadCenterLogical = centerLogical + (quadCenter - centerLogical) * scale;
			Vector3 deformedQuadCenterFinal = new Vector3(
				deformedQuadCenterLogical.x * uvScaleX,
				deformedQuadCenterLogical.y * uvScaleY,
				0f
			);

			// Now find the final quad index by matching deformed centers
			Vector3[] verts = _gridMesh.vertices;
			if (verts == null || verts.Length == 0) return;

			int closestFinalIdx = -1;
			float minDistSqr = float.MaxValue;

			for (int q = 0; q < numColumns * numRows; q++)
			{
				int baseV = q * 4;
				Vector3 bl = verts[baseV + 0];
				Vector3 tr = verts[baseV + 2];
				Vector3 center = (bl + tr) * 0.5f;

				float distSqr = (center - deformedQuadCenterFinal).sqrMagnitude;
				if (distSqr < minDistSqr)
				{
					minDistSqr = distSqr;
					closestFinalIdx = q;
				}
			}

			if (closestFinalIdx < 0) return;

			int baseVert = closestFinalIdx * 4;

			Vector3 v0 = verts[baseVert + 0];
			Vector3 v1 = verts[baseVert + 1];
			Vector3 v2 = verts[baseVert + 2];
			Vector3 v3 = verts[baseVert + 3];

			// Optional: slight outset for better visibility (adjust factor as needed)
			float outset = 0.06f;
			Vector3 dir0 = (v0 - (v0 + v1 + v2 + v3) * 0.25f).normalized * outset;
			Vector3 dir1 = (v1 - (v0 + v1 + v2 + v3) * 0.25f).normalized * outset;
			Vector3 dir2 = (v2 - (v0 + v1 + v2 + v3) * 0.25f).normalized * outset;
			Vector3 dir3 = (v3 - (v0 + v1 + v2 + v3) * 0.25f).normalized * outset;

			Vector3[] outlineVerts = {
				v0 + dir0,
				v1 + dir1,
				v2 + dir2,
				v3 + dir3
			};

			Vector2[] outlineUVs = {
				new Vector2(0f, 0f),
				new Vector2(0f, 1f),
				new Vector2(1f, 1f),
				new Vector2(1f, 0f)
			};

			int[] outlineTris = { 0, 1, 2, 0, 2, 3 };

			_outlineMesh = new Mesh
			{
				vertices = outlineVerts,
				uv = outlineUVs,
				triangles = outlineTris
			};
			_outlineMesh.name = "OutlineQuad_MatchDeformed";

			_lastOutlineCoord = coord;
		}

		private static void DrawGridToRT(int numColumns, int numRows, Vector2 coord)
		{
			LazyInitResources();

			int pw = numColumns * 64;
			int ph = numRows * 64;

			if (_rt == null || _rt.width != pw || _rt.height != ph || !_rt.IsCreated())
			{
				if (_rt != null) _rt.Release();
				_rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Point,
					antiAliasing = 1
				};
				_rt.Create();
			}

			RebuildGridMeshIfNeeded(numColumns, numRows, coord);
			RebuildOutlineMeshIfNeeded(numColumns, numRows, coord);

			var oldRT = RenderTexture.active;
			RenderTexture.active = _rt;

			GL.Clear(true, true, new Color(0.12f, 0.14f, 0.16f));

			GL.PushMatrix();
			GL.LoadOrtho();

			if (_gridMesh != null)
			{
				_quadMaterial.mainTexture = _quadTexture;
				_quadMaterial.color = Color.white;
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);
			}

			//if (_outlineMesh != null && _outlineTexture != null)
			//{
			//	_outlineMaterial.mainTexture = _outlineTexture;
			//	_outlineMaterial.color = Color.white;
			//	_outlineMaterial.SetPass(0);
			//	Graphics.DrawMeshNow(_outlineMesh, Matrix4x4.identity);
			//}

			if (_outlineMesh != null && _outlineMaterial != null)
			{
				_outlineMaterial.SetPass(0);
				Graphics.DrawMeshNow(_outlineMesh, Matrix4x4.identity);
			}


			GL.PopMatrix();
			RenderTexture.active = oldRT;
		}

		public static RenderTexture GetRenderTexture(int numColumns = 8, int numRows = 8, Vector2 coord = default)
		{
			if (coord == default) coord = new Vector2(0.5f, 0.5f);
			DrawGridToRT(numColumns, numRows, coord);
			return _rt;
		}

		public static void Cleanup()
		{
			if (_rt != null) { _rt.Release(); _rt = null; }
			if (_gridMesh != null) { Object.DestroyImmediate(_gridMesh); _gridMesh = null; }
			if (_outlineMesh != null) { Object.DestroyImmediate(_outlineMesh); _outlineMesh = null; }
			if (_quadMaterial != null) { Object.DestroyImmediate(_quadMaterial); _quadMaterial = null; }
			if (_outlineMaterial != null) { Object.DestroyImmediate(_outlineMaterial); _outlineMaterial = null; }
			_quadTexture = null;
			_outlineTexture = null;
			_logicalQuadCenters = null;
			_lastColumns = _lastRows = -1;
			_lastCoord = new Vector2(-999f, -999f);
			_lastOutlineCoord = new Vector2(-999f, -999f);
		}
	}
}