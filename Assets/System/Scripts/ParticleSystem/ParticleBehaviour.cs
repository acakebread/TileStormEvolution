using UnityEngine;

namespace MassiveHadronLtd
{
	// --------------------------------------------------------------------
	// Base class
	// --------------------------------------------------------------------
	public abstract class ParticleBehaviour
	{
		protected ParticleBehaviour() { }
		public virtual void Initialize(Particle p) { }
		public virtual void Update(Particle p, float deltaTime = 0f) { }
	}

	public class ParticleBehaviourColour : ParticleBehaviour
	{
		public float fadeStartTime = 1f;// 0-1 normalised lifetime

		public override void Update(Particle p, float deltaTime = 0f)
		{
			float norm = 1f - p.life / p.duration;
			p.color.a = (norm < fadeStartTime || fadeStartTime >= 1f) ? 1f : Mathf.Clamp01(1f - ((norm - fadeStartTime) / (1f - fadeStartTime)));// keep RGB, only touch A
		}
	}

	public class ParticleBehaviourScale : ParticleBehaviour
	{
		public float initialRadius;
		public ParticleController.ScaleTable scaleTable;

		public override void Initialize(Particle p) => Update(p);

		public override void Update(Particle p, float deltaTime = 0f)
		{
			float norm = 1f - p.life / p.duration;
			p.radius = initialRadius * scaleTable.Evaluate(norm);
		}
	}

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
		public float friction = 0.2f;
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
				_physics.velocity.y = -_physics.velocity.y * (1f - friction);
			}
		}
	}
}