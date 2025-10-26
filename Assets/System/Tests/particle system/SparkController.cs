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
			public float lifetimeVariation = 0.5f; // New: Random lifetime variation (±seconds)
			public float width = 0.02f; // Controls size for simple particles and body width for three-zone
			public bool decay = true; // Shrink width with age
			public Color color = Color.white;
			public float gravity = 10f; // Y-axis damping
			public float moveScale = 1f; // Velocity scale
			public float bounceDamping = 0.8f; // Velocity damping on collision
			public float groundHeight = 0f; // Ground plane Y position
			public bool useGlobalGroundPlane = true;
		}

		[SerializeField] private Material particleMaterial; // Material with custom AdditiveParticles shader
		[SerializeField] private bool useThreeZoneSlicing = false;
		[SerializeField] private bool useAdditiveBlending = true;
		[SerializeField] public SparkSettings defaultSettings;
		[SerializeField] private bool updateSparks = true;
		private readonly float simSpeed = 1f;
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

			customParticleSystem = new ParticleSystem(particleMaterial, useThreeZoneSlicing, useAdditiveBlending);
		}

		void FixedUpdate()
		{
			if (updateSparks)
				UpdateSparks();
		}

		void Update()
		{
			customParticleSystem.Render();
		}

		public void SpawnSpark(Vector3 position, Vector3 velocity, SparkSettings settings = null)
		{
			if (!updateSparks) return;
			if (settings == null) settings = defaultSettings;

			// Apply random lifetime variation
			float lifetime = settings.lifetime + Random.Range(-settings.lifetimeVariation, settings.lifetimeVariation);
			lifetime = Mathf.Max(0.1f, lifetime); // Ensure lifetime isn't negative or too short

			var particleSettings = new ParticleSystem.ParticleSettings
			{
				lifetime = lifetime, // Use modified lifetime
				width = settings.width,
				decay = settings.decay,
				color = settings.color
			};

			int poolIndex = customParticleSystem.SpawnParticle(position, velocity * settings.speed, particleSettings);
			if (poolIndex == -1) return;

			SparkData spark = new SparkData
			{
				poolIndex = poolIndex,
				position = position,
				velocity = velocity * settings.speed,
				lifetime = lifetime, // Use modified lifetime
				maxLifetime = lifetime,
				color = settings.color,
				width = settings.width,
				initialWidth = settings.width,
				tipSize = settings.width / 2f,
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

				if (!useAdditiveBlending)
				{
					spark.color.a = spark.lifetime / spark.maxLifetime;
				}
				else
				{
					spark.color.a = 1f; // Alpha modulates intensity for additive
				}

				if (defaultSettings.decay)
				{
					float decayFactor = spark.lifetime / spark.maxLifetime;
					spark.width = spark.initialWidth * decayFactor;
					spark.tipSize = spark.width / 2f;
				}

				spark.velocity.y -= defaultSettings.gravity * deltaTime * simSpeed;
				spark.position += spark.velocity * defaultSettings.moveScale * deltaTime * simSpeed;

				float currentY = spark.position.y;
				float groundY = defaultSettings.groundHeight;
				if (!defaultSettings.useGlobalGroundPlane)
				{
					groundY = transform.position.y + defaultSettings.groundHeight;
				}

				if (spark.velocity.y < 0 && currentY <= groundY)
				{
					spark.position.y = groundY;
					spark.velocity.y = -spark.velocity.y * defaultSettings.bounceDamping;
				}

				customParticleSystem.UpdateParticle(spark.poolIndex, spark.position, spark.velocity, spark.lifetime, spark.width, spark.tipSize, spark.color);
			}
		}
	}
}