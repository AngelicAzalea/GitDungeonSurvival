using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Pool;

public enum EnemyType { Skeleton, Slime }

[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(PathfinderComponent))]
public class EnemyBaseClass : MonoBehaviour, ProjectileBaseClassScript.IDamageable
{
	[Header("Identity")] public EnemyType enemyType = EnemyType.Skeleton;
	[Tooltip("If true this is an elite variant and may have modified visuals / stats")] public bool isElite = false;

	[Header("Stats")] public float maxHealth =50f;
	public float currentHealth =50f;
	public float damage =10f;
	public float moveSpeed =3f;

	// store base/original stats so runtime scaling doesn't accumulate
	private float baseMaxHealth;
	private float baseDamage;
	private float baseMoveSpeed;

	[Header("Detection")]
	[Tooltip("Distance at which the enemy will start actively moving toward the player")] public float detectionRange =50f;
	[Tooltip("Distance at which the enemy will consider itself in attack range")] public float attackRange =1.5f;

	[Header("References")] public Transform playerTarget; // assigned by spawner
	public SpriteRenderer spriteRenderer;
	public Animator animator;
	public PathfinderComponent pathfinderComponent;


	[Header("Pooling")]
	[Tooltip("When enabled enemy will be deactivated and returned to pool instead of being destroyed.")]
	public bool usePooling = true;

	[Header("Rewards")]
	[Tooltip("Base EXP awarded to the player when this enemy dies")]
	public float expReward =10f;
	[Tooltip("Multiplier applied to expReward when enemy is elite")]
	public float expRewardEliteMultiplier =1.5f;
	[Tooltip("If true, grant EXP to the player on death (uses playerTarget if set, otherwise GameObject tagged 'Player')")]
	public bool grantExpOnDeath = true;

	// Internal
	public event Action<EnemyBaseClass> OnDeath;

	// Global event raised when any enemy dies (useful for scoring systems)
	public static event Action<EnemyBaseClass> OnAnyEnemyKilled;

	protected Collider2D coll;
	protected Rigidbody2D rb;

	// visual damage flash
	private Color originalSpriteColor = Color.white;
	private Coroutine damageFlashRoutine = null;
	[Tooltip("How long to hold the red tint before restoring, in seconds")]
	public float damageFlashDuration =0.12f;

	protected float lastAttackTime =0f;
	public float attackCooldown =1.2f;

	// Ranged-attack settings moved into behaviour components (e.g. SkeletonBehaviour)

	private bool isDying = false;
	private bool bIsMoving = false;

	// Minimal flag used by inspector to detect whether a shared grid has been assigned

	public Vector2Int playerGridPosition;


	private float pathUpdateTimer =0f;
	private float pathUpdateInterval =0.7f; // seconds between path recalculations
	private List<int> currentPath = new List<int>();

	// New: desired velocity computed by PathFind, applied in LateUpdate
	private Vector2 desiredVelocity = Vector2.zero;

	[Header("Behaviour")]
	[Tooltip("Optional EnemyBehaviour component attached to the enemy; if null, one will be added based on enemyType")]
	public EnemyBehaviour behaviour;

	// New fields for death fade
	[Header("Death")]
	[Tooltip("Fade duration in seconds when enemy dies before returning to pool / destruction")]
	public float deathFadeDuration =0.5f;

	protected virtual void Awake()
	{
		if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
		if (animator == null) animator = GetComponent<Animator>();
		coll = GetComponent<Collider2D>();
		rb = GetComponent<Rigidbody2D>();
		currentHealth = maxHealth;
		if (pathfinderComponent == null) pathfinderComponent = GetComponent<PathfinderComponent>();

		// capture base/original stats so ApplyScaling can set absolute values
		baseMaxHealth = maxHealth;
		baseDamage = damage;
		baseMoveSpeed = moveSpeed;
		if (spriteRenderer != null) originalSpriteColor = spriteRenderer.color;

		// attach behaviour component based on enemyType if none assigned
		if (behaviour == null)
		{
			switch (enemyType)
			{
				case EnemyType.Skeleton:
					behaviour = GetComponent<SkeletonBehaviour>();
					if (behaviour == null) behaviour = gameObject.AddComponent<SkeletonBehaviour>();
					break;
				case EnemyType.Slime:
					// SlimeBehaviour can be added similarly when implemented
					break;
			}
		}
		if (behaviour != null) behaviour.Initialize(this);
		// Note: actual tilemaps will be provided by spawner via OnSpawn during activation.
	}

	public void Update()
	{
		// freeze all enemy activity when game is over
		if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
			return;

		// don't run behaviour while dying
		if (!isDying)
			behaviour?.OnBehaviourUpdate();
		PathFind();
	}

	public void FixedUpdate()
	{
		// stop movement when game over
		if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
		{
			if (rb != null) rb.linearVelocity = Vector2.zero;
			return;
		}

		if (rb != null)
		{
			rb.linearVelocity = desiredVelocity;
		}
		else
		{
			// desiredVelocity is in units/sec; multiply by deltaTime for transform changes
			transform.position += (Vector3)desiredVelocity * Time.deltaTime;
		}
	}

	public void LateUpdate()
	{
		// Apply movement decided in PathFind here so animations and movement are in sync.
		if (isDying)
		{
			desiredVelocity = Vector2.zero;
			bIsMoving = false;
		}

		// Update moving flag and animator
		bool movingNow = desiredVelocity.sqrMagnitude >0.0001f;
		if (bIsMoving != movingNow)
		{
			bIsMoving = movingNow;
			if (animator != null)
			{
				// Keep compatibility with different animator parameter names used in various controllers.
				// Some controllers use "bIsMoving" (player style) others use "isMoving".
				animator.SetBool("bIsMoving", bIsMoving);
			}
		}

		// Also update a generic speed parameter so animator graphs can use blending if available
		if (animator != null)
		{
			//float speedVal = desiredVelocity.magnitude;
			//animator.SetFloat("speed", speedVal);
		}
	}

	/// <summary>
	/// TO BE WORKED ON
	/// </summary>
	public virtual void PathFind()
	{
		//Check if we have a PathfinderComponent assigned

		if (pathfinderComponent == null) return;
		if (playerTarget == null) return;
		if (isDying) return;

		// also freeze when game over
		if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
		{
			desiredVelocity = Vector2.zero;
			return;
		}

		//Get the Grid position of the player
		//Convert the players world position to grid position
		float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);

		if (distToPlayer > detectionRange)
		{
			// stop movement
			desiredVelocity = Vector2.zero;
			return;
		}

		if (distToPlayer <= attackRange)
		{
			desiredVelocity = Vector2.zero;
			AttackPlayer();
			return;
		}

		Tilemap refMap = pathfinderComponent != null && pathfinderComponent.NonCollisionTilemap != null ? pathfinderComponent.NonCollisionTilemap : pathfinderComponent?.CollisionTilemap;
		if (refMap == null)
		{
			// No tilemap available; simple direct-chase fallback
			Vector2 dirFallback = (playerTarget.position - transform.position).normalized;
			desiredVelocity = dirFallback * moveSpeed;
			return;
		}

		Vector3Int playerCell = refMap.WorldToCell(playerTarget.position);
		Vector3Int myCell = refMap.WorldToCell(transform.position);

		pathUpdateTimer += Time.deltaTime;
		if (pathUpdateTimer >= pathUpdateInterval)
		{
			pathUpdateTimer =0f;
			currentPath.Clear();
			// Attempt to find path on the grid (start = myCell, end = playerCell)
			bool found = pathfinderComponent.FindPath(myCell.x, myCell.y, playerCell.x, playerCell.y);
			if (found)
			{
				currentPath = pathfinderComponent.GetPath(playerCell.x, playerCell.y);
			}
		}

		if (currentPath != null && currentPath.Count >0)
		{
			int nextIndex = currentPath[currentPath.Count -1]; // peek last element
			Vector3 targetWorld = pathfinderComponent.IndexToWorldPos(nextIndex, refMap);
			Vector2 dir = (targetWorld - transform.position);
			if (dir.sqrMagnitude >0.001f)
			{
				Vector2 moveDir = dir.normalized;
				// Do not apply movement here, only compute desired velocity
				desiredVelocity = moveDir * moveSpeed;

				//This should be moved into Late Update
			}
			else
			{
				// reached this path node; pop it so next call advances
				currentPath.RemoveAt(currentPath.Count -1);
				desiredVelocity = Vector2.zero;
			}
		}
		else
		{
			// no path found -> fallback to simple direct movement toward player (best-effort)
			Vector2 dirFallback = (playerTarget.position - transform.position).normalized;
			desiredVelocity = dirFallback * moveSpeed;
		}
	}

	public void PrepareForSpawn(float initialDelay =0f)
	{
		// placeholder for compatibility with spawner; no-op
	}

	// Called when the spawner activates this enemy from the pool. Use this to set up refs/decisions.
	public virtual void OnSpawn(Transform playerTarget, Tilemap collisionTilemap, Tilemap nonCollisionTilemap)
	{
		this.playerTarget = playerTarget;
		isDying = false;

		// Apply any scaling after resetting base values
		maxHealth = baseMaxHealth;
		damage = baseDamage;
		moveSpeed = baseMoveSpeed;

		currentHealth = maxHealth;
		if (coll != null) coll.enabled = true;
		if (rb != null) rb.linearVelocity = Vector2.zero;
		if (animator != null) { animator.Rebind(); animator.Update(0f); }

		// Ensure pathfinder has current tilemaps
		if (pathfinderComponent != null)
			pathfinderComponent.InitializeFromTilemaps(collisionTilemap, nonCollisionTilemap);

		// notify behaviour
		behaviour?.OnSpawn();
	}

	public virtual void ResetForReuse()
	{
		isDying = false;
		// restore base stats
		maxHealth = baseMaxHealth;
		damage = baseDamage;
		moveSpeed = baseMoveSpeed;

		currentHealth = maxHealth;
		desiredVelocity = Vector2.zero;
		if (coll != null) coll.enabled = true;
		if (rb != null) rb.linearVelocity = Vector2.zero;
		if (animator != null) { animator.Rebind(); animator.Update(0f); }
		// stop any ongoing damage flash and restore original color
		if (damageFlashRoutine != null)
		{
			StopCoroutine(damageFlashRoutine);
			damageFlashRoutine = null;
		}
		if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;

		// notify behaviour reset
		behaviour?.OnReset();
	}

	// Combat / damage
	protected virtual void AttackPlayer()
	{
		if (isDying) return; // don't attack while dying

		// If a behaviour is attached, delegate attack handling to it
		if (behaviour != null)
		{
			behaviour.AttackPlayer();
			return;
		}

		// Implement simple ranged attack for skeletons (fallback)
		if (enemyType == EnemyType.Skeleton && playerTarget != null)
		{
			float now = Time.time;
			if (now - lastAttackTime >= attackCooldown)
			{
				lastAttackTime = now;
				// Fallback to simple direct attack
				var dmgTarget = playerTarget.GetComponent<ProjectileBaseClassScript.IDamageable>();
				if (dmgTarget != null)
				{
					dmgTarget.TakeDamage(damage, false, CharacterClass.Unknown, gameObject);
				}
			}
			return;
		}

		// melee or other types: default behavior can be expanded
	}

	public virtual void TakeDamage(float amount, bool wasCritical, CharacterClass sourceClass, GameObject source)
	{
		Debug.Log($"Enemy.TakeDamage received amount={amount} wasCritical={wasCritical} from={sourceClass} source={source?.name}");



		//turn amount into an integer for display purposes / rounding
		int intAmount = Mathf.CeilToInt(amount);

		currentHealth -= intAmount;
		SpawnsDamagePopups.Instance.DamageDone(intAmount, transform.position, wasCritical);

		// flash red on hit
		if (spriteRenderer != null)
		{
			// stop any existing flash so we restart
			if (damageFlashRoutine != null) StopCoroutine(damageFlashRoutine);
			damageFlashRoutine = StartCoroutine(DamageFlashCoroutine());
		}

		if (currentHealth <=0f)
		{
			Die();
		}
	}

	private IEnumerator DamageFlashCoroutine()
	{
		if (spriteRenderer == null) yield break;

		// set to a bright red color
		spriteRenderer.color = Color.red;
		// wait for the duration
		yield return new WaitForSeconds(damageFlashDuration);
		// restore original color
		spriteRenderer.color = originalSpriteColor;
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier)
	{
		// Use base values so scaling is absolute and doesn't accumulate
		maxHealth = baseMaxHealth * healthMultiplier;
		currentHealth = maxHealth;
		damage = baseDamage * damageMultiplier;
		moveSpeed = baseMoveSpeed * speedMultiplier;
	}

	public virtual void ApplyScaling(float healthMultiplier)
	{
		ApplyScaling(healthMultiplier, healthMultiplier,1f);
	}

	public virtual void ApplyScalingFromGameMultiplier()
	{
		float m = GameManager.Instance != null ? GameManager.Instance.DifficultyMultiplier :1f;
		ApplyScaling(m, m,1f);
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier)
	{
		ApplyScaling(healthMultiplier, damageMultiplier,1f);
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier, bool preserveCurrentHealth)
	{
		// preserveCurrentHealth not used for now; for future flexibility
		ApplyScaling(healthMultiplier, damageMultiplier, speedMultiplier);
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier, bool preserveCurrentHealth, bool resetBase)
	{
		ApplyScaling(healthMultiplier, damageMultiplier, speedMultiplier);
	}

	public virtual void ApplyScaling() { ApplyScalingFromGameMultiplier(); }

	public virtual void ApplyScaling(bool fromGameManager)
	{
		if (fromGameManager) ApplyScalingFromGameMultiplier();
	}

	public virtual void ApplyScaling(float healthMultiplier, bool preserveCurrentHealth)
	{
		ApplyScaling(healthMultiplier, healthMultiplier,1f);
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier, bool preserveCurrentHealth, bool someFlag)
	{
		ApplyScaling(healthMultiplier, damageMultiplier,1f);
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier, bool preserveCurrentHealth, bool someFlag, bool another)
	{
		ApplyScaling(healthMultiplier, damageMultiplier, speedMultiplier);
	}

	public virtual void ApplyScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier, bool preserveCurrentHealth, bool someFlag, bool another, int extra)
	{
		ApplyScaling(healthMultiplier, damageMultiplier, speedMultiplier);
	}

	protected virtual void Die()
	{
		if (isDying) return;
		isDying = true;
		if (animator != null) //animator.SetTrigger("Die");
		if (coll != null) coll.enabled = false;
		if (rb != null) rb.linearVelocity = Vector2.zero;

		// stop any ongoing damage flash immediately
		if (damageFlashRoutine != null)
		{
			StopCoroutine(damageFlashRoutine);
			damageFlashRoutine = null;
		}

		// signal global kill event for scoring
		try { OnAnyEnemyKilled?.Invoke(this); } catch { }

		// Add to player's score via GameManager when this enemy dies
		try
		{
			if (GameManager.Instance != null)
			{
				GameManager.Instance.AddScore(GameManager.Instance.pointsPerKill);
			}
		}
		catch { }

		// Grant EXP to player
		if (grantExpOnDeath)
		{
			try { AwardExpToPlayer(); } catch { }
		}

		// start fade then return/destroy
		StartCoroutine(FadeOutAndRelease(deathFadeDuration));
	}

	private void AwardExpToPlayer()
	{
		float reward = expReward * (isElite ? expRewardEliteMultiplier :1f);
		if (reward <=0f) return;

		// Prefer playerTarget if assigned by spawner
		CharacterInput ci = null;
		if (playerTarget != null)
		{
			ci = playerTarget.GetComponent<CharacterInput>();
		}

		// fallback to GameObject tagged "Player"
		if (ci == null)
		{
			var playerGo = GameObject.FindGameObjectWithTag("Player");
			if (playerGo != null) ci = playerGo.GetComponent<CharacterInput>();
		}

		if (ci != null)
		{
			ci.AddExp(reward);
			if (ci.StatSystem != null && ci.StatSystem.debugStatSystem)
			Debug.Log($"EnemyBaseClass: Granted {reward} EXP to player {ci.gameObject.name}");
		}
	}

	private IEnumerator FadeOutAndRelease(float duration)
	{
		if (spriteRenderer != null)
		{
			Color start = spriteRenderer.color;
			float startA = start.a;
			float t =0f;
			while (t < duration)
			{
				t += Time.deltaTime;
				float f = Mathf.Clamp01(t / duration);
				Color c = start;
				c.a = Mathf.Lerp(startA,0f, f);
				spriteRenderer.color = c;
				yield return null;
			}
			// ensure fully transparent
			Color endc = spriteRenderer.color;
			endc.a =0f;
			spriteRenderer.color = endc;
		}
		else
		{
			// small wait so death feels noticeable if no renderer
			yield return new WaitForSeconds(duration);
		}

		// notify pool/spawner via OnDeath event so pooling system can reclaim
		try { OnDeath?.Invoke(this); } catch { }

		if (!usePooling)
		{
			Destroy(gameObject);
		}
	}

	public void ForceDestroy() { Destroy(gameObject); }
}
