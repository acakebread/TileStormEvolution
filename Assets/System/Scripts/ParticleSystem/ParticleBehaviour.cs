using UnityEngine;

namespace MassiveHadronLtd
{
	// --------------------------------------------------------------------
	// Base class – unchanged signature, but now works with a LIST of behaviours
	// --------------------------------------------------------------------
	public abstract class ParticleBehaviour
	{
		protected ParticleBehaviour() { }
		public virtual void Initialize(Particle p) { }
		public virtual void Update(Particle particle, float deltaTime = 0f) { }
	}

	// --------------------------------------------------------------------
	// 1. Colour – pulls the alpha-fade logic out of Particle.Update()
	// --------------------------------------------------------------------
	public class ParticleBehaviourColour : ParticleBehaviour
	{
		public float fadeStartTime = 1f;               // 0-1 normalised lifetime

		public override void Update(Particle particle, float deltaTime = 0f)
		{
			float norm = 1f - particle.life / particle.duration;
			float alpha = (norm < fadeStartTime || Mathf.Approximately(fadeStartTime, 1f))
				? 1f
				: Mathf.Clamp01(1f - ((norm - fadeStartTime) / (1f - fadeStartTime)));

			// keep RGB, only touch A
			particle.color.a = alpha;
		}
	}

	// --------------------------------------------------------------------
	// 2. Scale – evaluates the shared scale table (or a curve if you prefer)
	// --------------------------------------------------------------------
	public class ParticleBehaviourScale : ParticleBehaviour
	{
		public float initialRadius;
		public ParticleController.ScaleTable scaleTable;

		public override void Update(Particle particle, float deltaTime = 0f)
		{
			float norm = 1f - particle.life / particle.duration;
			particle.radius = initialRadius * scaleTable.Evaluate(norm);
		}
	}

	// --------------------------------------------------------------------
	// Existing physics behaviour – unchanged (just for reference)
	// --------------------------------------------------------------------
	public class ParticlePhysicsBehaviour : ParticleBehaviour
	{
		public Vector3 velocity;
		public float gravity;
		public float friction;

		public override void Update(Particle p, float deltaTime)
		{
			// 1. Store old position (ONLY physics does this)
			p.oldPosition = p.position;

			// 2. Apply forces
			velocity.y -= gravity * deltaTime;
			velocity *= 1f - friction;

			// 3. Integrate
			p.position += velocity * deltaTime;
		}
	}

	public class ParticleGroundCollisionBehaviour : ParticleBehaviour
	{
		public float restitution = 0.8f;
		public float groundHeight = 0f;

		private ParticlePhysicsBehaviour _physics;

		public override void Initialize(Particle p)
		{
			_physics = p.GetBehaviour<ParticlePhysicsBehaviour>();
		}

		public override void Update(Particle p, float deltaTime)
		{
			if (_physics == null) return;

			// Clamp to ground
			if (p.position.y < groundHeight)
				p.position.y = groundHeight;

			// Bounce
			if (p.position.y <= groundHeight + 0.001f && _physics.velocity.y < 0f)
			{
				p.position.y = groundHeight;
				_physics.velocity.y = -_physics.velocity.y * restitution;
			}

			// delta remains correct — collision correction is part of motion
		}
	}
}