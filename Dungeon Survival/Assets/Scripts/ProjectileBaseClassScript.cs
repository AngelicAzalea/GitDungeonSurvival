using System;
using UnityEngine;

/// <summary>
/// Universal projectile usable by all classes (Warrior, Ranger, Mage).
/// Configure via inspector or by calling Initialize(...) at spawn time.
/// Exposes simple lifecycle, collision -> damage dispatch, piercing and hit feedback.
/// This version automatically detects sprite "tip" side when assigning a sprite.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class ProjectileBaseClassScript : MonoBehaviour
{
	[Header("Movement")]
	[Tooltip("World-space direction the projectile will travel (normalized).")]
	public Vector2 initialDirection = Vector2.right;
	[Tooltip("Speed in units/second")]
	public float speed = 10f;
	[Tooltip("Use Rigidbody velocity instead of manual movement")]
	public bool usePhysicsVelocity = false;

	[Header("Stats")]
	[Tooltip("Base damage applied to targets that implement IDamageable")]
	public float damage = 10f;
	[Tooltip("How many hits this projectile can pierce through before being destroyed. 0 = destroy on first hit.")]
	[Min(0)]
	public int pierceCount = 0;
	public bool wasCritical = false;
	// critMultiplier applied when wasCritical==true
	public float critMultiplier = 1.5f;

	[Tooltip("If true this projectile will destroy other projectiles it collides with. If false, projectiles ignore each other.")]
	public bool destroyOtherProjectiles = false;

	// Optional override to control whether this instance uses physics velocity
	[NonSerialized] public bool physicsVelocityOverrideApplied = false;

	[Header("Lifetime / owner")]
	[Tooltip("Seconds before auto-destroy")]
	public float lifetime = 5f;
	[Tooltip("GameObject that fired this projectile (will be ignored by collision)")]
	public GameObject owner;
	[Tooltip("Owner class (optional) to allow class-specific behaviour)")]
	public CharacterClass ownerClass = CharacterClass.Warrior;

	[Header("Collision")]
	[Tooltip("Optional hit effect prefab (spawned at hit point)")]
	public GameObject hitEffectPrefab;
	[Tooltip("Layers that this projectile should consider as hittable. Use ~0 for all.")]
	public LayerMask hittableLayers = ~0;

	[Header("Visuals")]
	[Tooltip("Optional SpriteRenderer for the projectile. If not assigned, will attempt to find one on the GameObject.")]
	[SerializeField] private SpriteRenderer projectileSpriteRenderer;

	// Spin (degrees per second). If non-zero the projectile will rotate around its pivot while flying.
	[Header("Visual Effects")]
	[Tooltip("Degrees per second to rotate the projectile while in flight (positive = clockwise)")]
	public float angularSpeed =0f;
	
	// Internal
	private Rigidbody2D rb;
	private Collider2D coll;
	private int remainingPierces;
	private float spawnTime;

	// whether the current sprite artwork faces +X (right). Determined automatically when sprite assigned.
	public bool spriteFacesRight = true;

	// Public state
	public bool IsActive => gameObject.activeInHierarchy;

	// Simple damage interface that targets can implement (optional)
	public interface IDamageable
	{
		// amount = numeric damage to apply (already multiplied for crit if appropriate)
		// wasCritical = whether this hit was a critical
		void TakeDamage(float amount, bool wasCritical, CharacterClass sourceClass, GameObject source);
	}

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		coll = GetComponent<Collider2D>();
		coll.isTrigger = true; // projectile uses trigger collisions by default

		// If not assigned, try to find a SpriteRenderer on this object or children
		if (projectileSpriteRenderer == null)
		{
			projectileSpriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
		}
	}

	void OnEnable()
	{
		spawnTime = Time.time;
		remainingPierces = pierceCount;
		// Ensure direction is normalized
		initialDirection = initialDirection.normalized;
		// Apply physics velocity according to override if requested/applicable
		if (physicsVelocityOverrideApplied)
		{
			if (usePhysicsVelocity && rb != null)
				rb.linearVelocity = initialDirection * speed;
		}
		else
		{
			if (usePhysicsVelocity && rb != null)
				rb.linearVelocity = initialDirection * speed;
		}
	}

	void Update()
	{
		// Lifetime check
		if (Time.time - spawnTime >= lifetime)
		{
			Destroy(gameObject);
			return;
		}

		if (!usePhysicsVelocity)
		{
			// Simple kinematic movement (more deterministic)
			transform.position += (Vector3)(initialDirection * speed * Time.deltaTime);
		}

		// Apply angular spin if configured
		if (Mathf.Abs(angularSpeed) >0.001f)
		{
			transform.Rotate(0f,0f, angularSpeed * Time.deltaTime);
		}
	}

	/// <summary>
	/// Initialize the projectile programmatically after instantiation.
	/// Useful for pooled projectiles.
	/// - spriteOverride: if provided, the projectile will use this sprite.
	/// - classData: optional CharacterClassData; if provided and spriteOverride is null, classData.classProjectile will be used.
	/// This method will also orient the projectile transform so the sprite tip faces the firing direction.
	/// </summary>
	public void Initialize(
		GameObject owner,
		Vector2 worldDirection,
		float speedOverride = -1f,
		float damageOverride = -1f,
		float critChanceOverride = -1f,
		CharacterClass? ownerCls = null,
		int pierceOverride = -1,
		Sprite spriteOverride = null,
		CharacterClassData classData = null,
		bool wasCritical = false,
		float critMultiplierOverride =0f,
		bool? destroyOtherProjectilesOverride = null,
		bool? usePhysicsOverride = null,
		float? gravityScaleOverride = null,
		float? lifetimeOverride = null)
	{
		this.owner = owner;
		if (worldDirection != Vector2.zero) this.initialDirection = worldDirection.normalized;
		if (speedOverride >0f) this.speed = speedOverride;
		if (damageOverride >=0f) this.damage = damageOverride;
		if (ownerCls.HasValue) this.ownerClass = ownerCls.Value;
		if (pierceOverride >=0) this.pierceCount = pierceOverride;
		this.remainingPierces = this.pierceCount;
		spawnTime = Time.time;

		// apply lifetime override if provided
		if (lifetimeOverride.HasValue)
		{
			this.lifetime = lifetimeOverride.Value;
		}

		// Store crit info provided at spawn
		this.wasCritical = wasCritical;
		if (critMultiplierOverride >0f)
			this.critMultiplier = critMultiplierOverride;
		else if (classData != null)
			this.critMultiplier = classData.critMultiplier;

		// Pooling-aware reset: if caller provided an explicit destroyOtherProjectilesOverride use it,
		// otherwise reset to false to avoid pooled state leak.
		if (destroyOtherProjectilesOverride.HasValue)
		{
			this.destroyOtherProjectiles = destroyOtherProjectilesOverride.Value;
		}
		else
		{
			this.destroyOtherProjectiles = false;
		}

		// Pooling-aware physics/gravity overrides
		physicsVelocityOverrideApplied = false;
		if (usePhysicsOverride.HasValue)
		{
			physicsVelocityOverrideApplied = true;
			this.usePhysicsVelocity = usePhysicsOverride.Value;
		}
		if (gravityScaleOverride.HasValue && rb != null)
		{
			rb.gravityScale = gravityScaleOverride.Value;
		}

		// Sprite selection: explicit override wins, otherwise take from classData if provided.
		if (spriteOverride != null)
		{
			SetProjectileSprite(spriteOverride);
		}
		else if (classData != null && classData.classProjectile != null)
		{
			SetProjectileSprite(classData.classProjectile);
		}

		// After sprite is assigned (and spriteFacesRight computed), orient the transform to face initialDirection.
		OrientTransformToDirection(initialDirection);

		// Note: we defer applying physics velocity to OnEnable where it will be applied
	}

	/// <summary>
	/// Sets the projectile sprite immediately and auto-detects which side the visual "tip" is on.
	/// Detection uses texture pixel alpha sampling when possible (texture must be readable).
	/// If detection fails, it falls back to sprite pivot heuristic.
	/// </summary>
	public void SetProjectileSprite(Sprite sprite)
	{
		if (sprite == null) return;
		if (projectileSpriteRenderer == null)
		{
			// try to find one (best-effort)
			projectileSpriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
			if (projectileSpriteRenderer == null)
			{
				Debug.LogWarning("ProjectileBaseClassScript: no SpriteRenderer found to assign sprite.");
				return;
			}
		}

		projectileSpriteRenderer.sprite = sprite;

		// Attempt automatic detection of whether sprite tip faces right (+X)
		spriteFacesRight = DetectSpriteFacesRight(sprite);
	}

	// Try to detect which side of the sprite contains the 'tip' by sampling opaque pixels.
	// Requires the sprite.texture to be marked as readable in import settings to use pixel sampling.
	// Falls back to pivot heuristic if texture isn't readable.
	private bool DetectSpriteFacesRight(Sprite s)
	{
		if (s == null) return true;

		try
		{
			Texture2D tex = s.texture;
			Rect texRect = s.textureRect; // in pixels
			int x0 = Mathf.FloorToInt(texRect.x);
			int y0 = Mathf.FloorToInt(texRect.y);
			int w = Mathf.FloorToInt(texRect.width);
			int h = Mathf.FloorToInt(texRect.height);

			// If texture not readable, fall back to pivot heuristic
			if (!tex.isReadable || w <= 0 || h <= 0)
			{
				// pivot.x is in pixels within the rect
				return s.pivot.x >= (texRect.width * 0.5f);
			}

			Color[] pixels = tex.GetPixels(x0, y0, w, h);

			int leftMost = -1;
			int rightMost = -1;
			// scan left->right for first opaque column and right->left for first opaque column
			for (int cx = 0; cx < w; cx++)
			{
				for (int cy = 0; cy < h; cy++)
				{
					if (pixels[cy * w + cx].a > 0.1f)
					{
						leftMost = cx;
						break;
					}
				}
				if (leftMost != -1) break;
			}

			for (int cx = w - 1; cx >= 0; cx--)
			{
				for (int cy = 0; cy < h; cy++)
				{
					if (pixels[cy * w + cx].a > 0.1f)
					{
						rightMost = cx;
						break;
					}
				}
				if (rightMost != -1) break;
			}

			// If no opaque pixels found, fallback to pivot heuristic
			if (leftMost == -1 || rightMost == -1)
				return s.pivot.x >= (texRect.width * 0.5f);

			float center = (w - 1) * 0.5f;
			float distRight = Mathf.Abs(rightMost - center);
			float distLeft = Mathf.Abs(center - leftMost);

			// If the furthest opaque pixel is on the right side, the sprite tip likely faces right.
			return distRight >= distLeft;
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"ProjectileBaseClassScript: sprite direction detection failed ({ex.Message}), falling back to pivot heuristic.");
			return s.pivot.x >= (s.textureRect.width * 0.5f);
		}
	}

	// Orient the transform so the sprite "tip" points along direction.
	private void OrientTransformToDirection(Vector2 dir)
	{
		if (dir.sqrMagnitude <= 0.000001f) return;
		float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
		// If sprite tip is on left visually, rotate 180 degrees so tip points to dir
		if (!spriteFacesRight) angleDeg += 180f;
		transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		// ignore owner collisions
		if (owner != null && other.gameObject == owner) return;

		// If this collides with another projectile, handle according to destroyOtherProjectiles flags
		var otherProj = other.GetComponent<ProjectileBaseClassScript>();
		if (otherProj != null)
		{
			// ignore self
			if (otherProj == this) return;

			// If neither projectile is configured to destroy other projectiles, ignore collision entirely
			if (!destroyOtherProjectiles && !otherProj.destroyOtherProjectiles)
				return;

			// If both want to destroy other projectiles, destroy both
			if (destroyOtherProjectiles && otherProj.destroyOtherProjectiles)
			{
				Destroy(other.gameObject);
				Destroy(gameObject);
				return;
			}

			// If only this one destroys others, destroy the other and continue
			if (destroyOtherProjectiles)
			{
				Destroy(other.gameObject);
				return;
			}

			// Otherwise, the other projectile is responsible for destroying this one; do nothing here
			return;
		}

		// Layer filter
		if (((1 << other.gameObject.layer) & hittableLayers) ==0) return;

		// Try to apply damage
		var damageable = other.GetComponent<IDamageable>();
		if (damageable != null)
		{
			try
			{
				float delivered = damage;
				if (wasCritical)
					delivered = delivered * critMultiplier;

				// Debug trace for crit/damage
				Debug.Log($"Projectile hit '{other.name}' delivered={delivered} wasCritical={wasCritical} owner={owner?.name}");

				// Pass the numeric damage (already multiplied if crit) and the crit flag
				damageable.TakeDamage(delivered, wasCritical, ownerClass, owner);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Projectile: exception calling TakeDamage on {other.name}: {ex.Message}");
			}
		}

		// Optional hit effect
		if (hitEffectPrefab != null)
		{
			Instantiate(hitEffectPrefab, other.ClosestPoint(transform.position), Quaternion.identity);
		}

		// Handle pierce / destroy
		// Only allow piercing through objects that are tagged as enemies.
		// If the collider belongs to an enemy (by tag) and we have remaining pierces, decrement and continue.
		// Otherwise destroy the projectile.
		bool hitEnemy = other.gameObject.CompareTag("Enemy");
		if (hitEnemy)
		{
			if (remainingPierces >0)
			{
				remainingPierces--;
				// continue flying
				return;
			}
			// no remaining pierces -> destroy
			Destroy(gameObject);
			return;
		}

		// Not an enemy (e.g., dungeon tile or other world geometry) -> destroy immediately
		Destroy(gameObject);
	}

	// Safety: if physics is used, update velocity if direction changes
	public void SetDirection(Vector2 newDirection)
	{
		initialDirection = newDirection.normalized;
		if (usePhysicsVelocity && rb != null) rb.linearVelocity = initialDirection * speed;
	}

	// Optional: allow external systems to change damage at runtime
	public void SetDamage(float newDamage) => damage = newDamage;

	// Allow external tinting of projectile sprite
	public void SetTint(Color color)
	{
		if (projectileSpriteRenderer != null)
			projectileSpriteRenderer.color = color;
	}
}
