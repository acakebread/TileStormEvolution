using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class ParticleThreeSlice : Particle { }

	public class ParticleSystemThreeSlice : ParticleSystem
	{
		private const int VerticesPerParticle = 8;
		private const int TrianglesPerParticle = 6;

		public ParticleSystemThreeSlice(Material particleMaterial, ParticleController controller)
			: base(particleMaterial, controller)
		{
		}

		protected override void Initialize()
		{
			base.Initialize();
			InitializeBuffers();
		}

		protected override void InitializeBuffers()
		{
			vertices = new List<Vector3>(MaxParticles * VerticesPerParticle);
			triangles = new List<int>(MaxParticles * TrianglesPerParticle);
			colors = new List<Color>(MaxParticles * VerticesPerParticle);
			uvs = new List<Vector2>(MaxParticles * VerticesPerParticle);

			for (int i = 0; i < MaxParticles; i++)
			{
				int offset = i * VerticesPerParticle;
				vertices.AddRange(new Vector3[VerticesPerParticle]);
				triangles.AddRange(new[]
				{
					offset+0,offset+1,offset+2, offset+2,offset+1,offset+3,
					offset+4,offset+2,offset+3, offset+4,offset+3,offset+5,
					offset+6,offset+4,offset+5, offset+6,offset+5,offset+7
				});
				colors.AddRange(new Color[VerticesPerParticle]);
				uvs.AddRange(new[]
				{
					new Vector2(0,1), new Vector2(1,1), new Vector2(0,0.5f), new Vector2(1,0.5f),
					new Vector2(0,0.5f), new Vector2(1,0.5f), new Vector2(0,0), new Vector2(1,0)
				});
			}
		}

		public override Particle AllocateParticle()
		{
			if (freeParticleIndices.Count == 0) return null;

			int idx = freeParticleIndices[freeParticleIndices.Count - 1];
			freeParticleIndices.RemoveAt(freeParticleIndices.Count - 1);

			var p = new ParticleThreeSlice
			{
				vertexIndex = idx * VerticesPerParticle,
				poolIndex = idx,
				life = -1f,
				position = Vector3.zero,
				oldPosition = Vector3.zero,
				radius = 0f,
				color = Color.clear
			};
			particlePool[idx] = p;
			activeParticles.Add(p);
			return p;
		}

		protected override void DeactivateParticle(Particle p, int activeIndex)
		{
			int v = p.vertexIndex;
			for (int i = 0; i < VerticesPerParticle; i++)
			{
				vertices[v + i] = Vector3.zero;
				colors[v + i] = Color.clear;
			}
			base.DeactivateParticle(p, activeIndex);
		}

		protected override void UpdateColors()
		{
			for (int i = 0; i < activeParticles.Count; i++)
			{
				Particle p = activeParticles[i];
				if (p.life <= 0f) continue;
				int v = p.vertexIndex;
				for (int j = 0; j < VerticesPerParticle; j++)
					colors[v + j] = p.color;
			}
		}

		protected override void UpdateMesh(Camera renderingCamera, List<Vector3> verts, List<Color> cols)
		{
			Matrix4x4 view = renderingCamera.worldToCameraMatrix;
			Matrix4x4 camToWorld = view.inverse;
			Vector3 camPos = camToWorld.MultiplyPoint(Vector3.zero);
			Vector3 camUp = camToWorld.MultiplyVector(Vector3.up).normalized;

			for (int i = 0; i < activeParticles.Count; i++)
			{
				Particle p = activeParticles[i];
				if (p.life <= 0f) continue;

				Vector3 pos = p.position;
				Vector3 toCam = (pos - camPos).normalized;
				Vector3 delta = p.delta; // full head-to-tail vector

				Vector3 tangent;
				if (delta.sqrMagnitude > 0.000001f)
					tangent = Vector3.Cross(delta, toCam).normalized;
				else
				{
					tangent = Vector3.Cross(camUp, toCam).normalized;
					if (tangent.sqrMagnitude < 0.01f)
						tangent = Vector3.Cross(Vector3.up, toCam).normalized;
				}

				// Project delta onto plane perpendicular to view
				float dot = Vector3.Dot(delta, toCam);
				Vector3 perp = delta - dot * toCam;
				float tang = perp.magnitude;

				// Key fix: only apply correction if tang < radius
				// And crucially: extend the *half* delta, not the full
				Vector3 halfDelta = delta * 0.5f;
				Vector3 halfPerp = perp * 0.5f;
				float halfTang = tang * 0.5f;

				if (halfTang < p.radius)
				{
					Vector3 cross = Vector3.Cross(toCam, tangent).normalized;
					halfDelta += (p.radius - halfTang) * cross;
				}

				// Velocity stretch: extra length beyond spherical part
				float velComp = halfTang > 0.0001f ? Mathf.Max(0f, halfTang - p.radius) / halfTang : 0f;
				Vector3 stretchedHalf = velComp * halfDelta;

				Vector3 head = pos + halfDelta;
				Vector3 tail = pos - halfDelta;
				Vector3 headB = pos + stretchedHalf;
				Vector3 tailB = pos - stretchedHalf;

				Vector3 rad = tangent * p.radius;

				int v = p.vertexIndex;

				verts[v + 0] = head - rad; verts[v + 1] = head + rad;
				verts[v + 2] = headB - rad; verts[v + 3] = headB + rad;
				verts[v + 4] = tailB - rad; verts[v + 5] = tailB + rad;
				verts[v + 6] = tail - rad; verts[v + 7] = tail + rad;
			}
		}
	}
}



//my fix - not sure if better or worse than current version|

//protected override void UpdateMesh(Camera renderingCamera, List<Vector3> verts, List<Color> cols)
//{
//	Matrix4x4 view = renderingCamera.worldToCameraMatrix;
//	Matrix4x4 camToWorld = view.inverse;
//	Vector3 camPos = camToWorld.MultiplyPoint(Vector3.zero);
//	Vector3 camUp = camToWorld.MultiplyVector(Vector3.up).normalized;

//	for (int i = 0; i < activeParticles.Count; i++)
//	{
//		Particle p = activeParticles[i];
//		if (p.life <= 0f) continue;

//		Vector3 pos = p.position;
//		Vector3 toCam = (pos - camPos).normalized;
//		Vector3 delta = p.delta * 0.5f;//hate this
//		var radius_double = p.radius * 2f;//and hate this

//		Vector3 tangent;
//		if (delta.sqrMagnitude > 0.000001f)
//			tangent = Vector3.Cross(delta, toCam).normalized;
//		else
//		{
//			tangent = Vector3.Cross(camUp, toCam).normalized;
//			if (tangent.sqrMagnitude < 0.01f)
//				tangent = Vector3.Cross(Vector3.up, toCam).normalized;
//		}

//		float dot = Vector3.Dot(delta, toCam);
//		float tang = (delta - dot * toCam).magnitude;
//		if (tang < radius_double)
//		{
//			Vector3 cross = Vector3.Cross(toCam, tangent);
//			delta += (radius_double - tang) * cross;
//		}

//		float velComp = tang > 0.0001f ? Mathf.Max(0, tang - radius_double) / tang : 0f;
//		Vector3 half = velComp * delta;
//		Vector3 headB = pos + half;
//		Vector3 tailB = pos - half;

//		Vector3 head = pos + delta;
//		Vector3 tail = pos - delta;
//		Vector3 rad = tangent * radius_double;
//		int v = p.vertexIndex;

//		verts[v + 0] = head - rad; verts[v + 1] = head + rad;
//		verts[v + 2] = headB - rad; verts[v + 3] = headB + rad;
//		verts[v + 4] = tailB - rad; verts[v + 5] = tailB + rad;
//		verts[v + 6] = tail - rad; verts[v + 7] = tail + rad;
//	}
//}
