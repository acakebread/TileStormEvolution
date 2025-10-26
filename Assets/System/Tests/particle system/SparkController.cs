using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class SparkController : MonoBehaviour
	{
		[System.Serializable]
		public class SparkSettings
		{
			public float speed = 4f;
			public float lifetime = 1f;
			public float lifetimeVariation = 0.5f; // Random lifetime variation (±seconds)
			public float width = 0.02f; // Controls size for simple particles and body width for three-zone
			public bool decay = true; // Shrink width with age
			public Color color = Color.white;
			public float gravity = 10f; // Y-axis damping
			public float bounceDamping = 0.8f; // Velocity damping on collision
			public float groundHeight = 0f; // Ground plane Y position
			public bool useGlobalGroundPlane = true;
			public bool useThreeZoneSlicing = false;
			public bool updateSparks = true;
		}

		[SerializeField] private Material particleMaterial;
		[SerializeField] private SparkSettings settings;

		private ParticleSystem customParticleSystem;
		private class SparkData
		{
			public int poolIndex;
			public Vector3 position;
			public Vector3 velocity;
			public float lifetime;
			public float maxLifetime;
			public Color color;
			public float width;
			public float initialWidth;
			public float tipSize;
			public bool isActive;
		}

		private List<SparkData> activeSparks;

		void Awake()
		{
			activeSparks = new List<SparkData>();
			if (particleMaterial == null)
			{
				Debug.LogError("SparkController: particleMaterial is not assigned! Please assign a material with the 'MassiveHadronLtd/Unlit/AdditiveParticles' shader and a spark texture.");
				enabled = false;
				return;
			}
			if (settings == null)
			{
				Debug.LogError("SparkController: settings is not assigned! Please assign SparkSettings in the Inspector.");
				enabled = false;
				return;
			}

			customParticleSystem = new ParticleSystem(particleMaterial, settings.useThreeZoneSlicing);
		}

		void FixedUpdate()
		{
			if (settings.updateSparks)
				UpdateSparks();
		}

		void Update()
		{
			customParticleSystem.Render();
		}

		public void SpawnSpark(Vector3 position, Vector3 velocity, float? lifetimeVariation = null, SparkSettings customSettings = null)
		{
			if (!settings.updateSparks) return;
			var activeSettings = customSettings ?? settings;
			float variation = lifetimeVariation ?? activeSettings.lifetimeVariation;

			float lifetime = activeSettings.lifetime + Random.Range(-variation, variation);
			lifetime = Mathf.Max(0.1f, lifetime);

			var particleSettings = new ParticleSystem.ParticleSettings
			{
				lifetime = lifetime,
				width = activeSettings.width,
				decay = activeSettings.decay,
				color = activeSettings.color
			};

			int poolIndex = customParticleSystem.SpawnParticle(position, velocity * activeSettings.speed, particleSettings);
			if (poolIndex == -1) return;

			SparkData spark = new SparkData
			{
				poolIndex = poolIndex,
				position = position,
				velocity = velocity * activeSettings.speed,
				lifetime = lifetime,
				maxLifetime = lifetime,
				color = activeSettings.color,
				width = activeSettings.width,
				initialWidth = activeSettings.width,
				tipSize = activeSettings.width * 0.54f,
				isActive = true
			};

			activeSparks.Add(spark);
		}

		private void UpdateSparks()
		{
			float deltaTime = Time.deltaTime;

			for (int i = activeSparks.Count - 1; i >= 0; i--)
			{
				SparkData spark = activeSparks[i];
				if (!spark.isActive)
				{
					activeSparks.RemoveAt(i);
					continue;
				}

				spark.lifetime -= deltaTime;

				if (spark.lifetime <= 0f)
				{
					spark.isActive = false;
					activeSparks.RemoveAt(i);
					customParticleSystem.UpdateParticle(spark.poolIndex, spark.position, spark.velocity, 0f, spark.width, spark.tipSize, spark.color);
					continue;
				}

				spark.color.a = spark.lifetime / spark.maxLifetime; // Modulate alpha for intensity

				if (settings.decay)
				{
					float decayFactor = spark.lifetime / spark.maxLifetime;
					spark.width = spark.initialWidth * decayFactor;
					spark.tipSize = spark.width * 0.54f;
				}

				spark.velocity.y -= settings.gravity * deltaTime;
				spark.position += spark.velocity * deltaTime;

				float currentY = spark.position.y;
				float groundY = settings.groundHeight;
				if (!settings.useGlobalGroundPlane)
				{
					groundY = transform.position.y + settings.groundHeight;
				}

				if (spark.velocity.y < 0 && currentY <= groundY)
				{
					spark.position.y = groundY;
					spark.velocity.y = -spark.velocity.y * settings.bounceDamping;
				}

				customParticleSystem.UpdateParticle(spark.poolIndex, spark.position, spark.velocity, spark.lifetime, spark.width, spark.tipSize, spark.color);
			}
		}
	}
}