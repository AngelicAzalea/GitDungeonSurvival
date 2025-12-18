using System;
using System.Collections;
using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class CharacterInput : MonoBehaviour, ProjectileBaseClassScript.IDamageable
{
	// Global reference to the main/local player instance (convenience for UI binding)
	public static CharacterInput LocalPlayer;

	[Header("Stats")]
	public float baseMoveSpeed =5f;
	public int baseMaxHealth =100;
	public int baseAttack =10;
	public float baseCritChance =10f;
	public float baseAttackSpeed =1.0f; // attacks per second
	public float baseDamageMultiplier =1.0f;
	public float baseExpGain =1.0f;
	public float baseHealthRegen =0f; // per second

	[Header("Runtime")]
	public float moveSpeed { get; private set; }
	public int MaximumHealth { get; private set; }
	public int CurrentHealth { get; private set; }
	public int Attack { get; private set; }
	public float CritChance { get; private set; }
	public float AttackSpeed { get; private set; }
	public float DamageMultiplier { get; private set; }
	public float ExpGainRate { get; private set; }
	public float HealthRegen { get; private set; }

	// Serialized mirror fields so runtime values are visible in the Inspector
	[Header("Runtime (Inspector)")]
	[SerializeField] private float inspector_moveSpeed;
	[SerializeField] private int inspector_MaximumHealth;
	[SerializeField] private int inspector_CurrentHealth;
	[SerializeField] private int inspector_Attack;
	[SerializeField] private float inspector_CritChance;
	[SerializeField] private float inspector_AttackSpeed;
	[SerializeField] private float inspector_DamageMultiplier;
	[SerializeField] private float inspector_ExpGainRate;
	[SerializeField] private float inspector_HealthRegen;

	// Progression / EXP
	[Header("Progression")]
	public int Level { get; private set; } =1;
	public float CurrentExp { get; private set; } =0f;

	// events for UI or other systems
	public event Action<int> OnLevelUp;
	public event Action<float, float> OnExpChanged; // (currentExp, expToNext)

	// Progression inspector mirrors
	[SerializeField] private int inspector_Level;
	[SerializeField] private float inspector_CurrentExp;
	[SerializeField] private float inspector_ExpToNext;


	private Rigidbody2D rb;
	private Vector2 movement;
	public SpriteRenderer spriteRenderer;
	public Animator animator;

	[Header("Class")]
	public CharacterClass selectedClass = CharacterClass.Ranger;
	public CharacterClassData classData;

	[Header("Hero Profile (optional)")]
	[Tooltip("Optional HeroProfile to override base class data and starting level/exp")]
	public HeroProfile heroProfile;

	[Header("Behavior")]
	// Optional behaviour component attached to the player that implements class-specific
	// input and abilities (e.g. RangerBehaviour). Can be assigned in inspector or
	// discovered at runtime via GetComponent.
	public PlayerClassBehaviour classBehaviour;

	[Header("UI")]
	// Health UI refs used by CharacterInput to draw player health
	public PlayerHealthContainerUI playerHealthContainerUISystem;
	public PlayerHealthFillUI playerHealthFillUISystem;

	private StatSystem statSystem;
	private float healthRegenAccumulator = 0f;

	// expose stat system to behaviours
	public StatSystem StatSystem => statSystem;

	// Damage tinting
	private Color originalSpriteColor = Color.white;
	private Coroutine damageFlashRoutine = null;
	[Tooltip("How long the player sprite stays red when damaged (seconds)")]
	public float playerDamageFlashDuration = 0.12f;

	// Local flag to prevent repeated death handling while dying animation plays
	private bool isDying = false;

	[Header("ProjectileSpawnpoint")]
	public GameObject projectileSpawnpointGameObject;
	public Transform projectileSpawnpointTransform;
	public float maxProjectileSpawnDistance = 2.0f;
	public float minProjectileSpawnDistance = 0.5f;

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		statSystem = FindObjectOfType<StatSystem>();

		// If this GameObject is tagged as the Player, register as LocalPlayer for UI bindings
		if (this.gameObject != null && this.gameObject.CompareTag("Player"))
		{
			LocalPlayer = this;
		}

		// try to find behaviour component if not assigned
		if (classBehaviour == null)
			classBehaviour = GetComponent<PlayerClassBehaviour>();

		if (spriteRenderer != null) originalSpriteColor = spriteRenderer.color;
	}

	void OnEnable()
	{
		if (statSystem != null) statSystem.OnInventoryChanged += ApplyStatModifiers;
		if (GameManager.Instance != null) GameManager.Instance.OnRunStarted += HandleRunStarted;
	}
	void OnDisable()
	{
		if (statSystem != null) statSystem.OnInventoryChanged -= ApplyStatModifiers;
		if (GameManager.Instance != null) GameManager.Instance.OnRunStarted -= HandleRunStarted;
	}

	private void HandleRunStarted()
	{
		// reset dying flag at start of each run
		isDying = false;
	}

	void Start()
	{
		// If a HeroProfile is provided, allow it to override starting class data and starting progression
		if (heroProfile != null)
		{
			// If profile has baseClassOverrides, use it as classData
			if (heroProfile.baseClassOverrides != null)
			{
				classData = heroProfile.baseClassOverrides;
			}
			// Try to load persistent progression for this profile
			if (HeroProgressionManager.Instance != null)
			{
				if (HeroProgressionManager.Instance.LoadForProfile(heroProfile, out int savedLevel, out float savedExp))
				{
					Level = Mathf.Max(1, savedLevel);
					CurrentExp = Mathf.Max(0f, savedExp);
				}
				else
				{
					// fallback to profile defaults
					Level = Mathf.Max(1, heroProfile.startingLevel);
					CurrentExp = Mathf.Max(0f, heroProfile.startingExp);
				}
			}
			else
			{
				Level = Mathf.Max(1, heroProfile.startingLevel);
				CurrentExp = Mathf.Max(0f, heroProfile.startingExp);
			}
		}

		if (classData == null)
		{
			switch (selectedClass)
			{
				case CharacterClass.Warrior: classData = Resources.Load<CharacterClassData>("ClassData/WarriorData"); break;
				case CharacterClass.Ranger: classData = Resources.Load<CharacterClassData>("ClassData/RangerData"); break;
				case CharacterClass.Mage: classData = Resources.Load<CharacterClassData>("ClassData/MageData"); break;
			}
		}

		// attach behaviour component based on selectedClass (if not already assigned)
		AttachBehaviourForSelectedClass();

		// apply base class values then modifiers
		ApplyStatModifiers();
		CurrentHealth = MaximumHealth;

		if (classBehaviour != null)
			classBehaviour.Initialize(this);

		// ensure inspector mirrors are set at start
		UpdateInspectorFields();

		// notify any listeners about initial EXP state (useful for UI that binds after Start)
		try { OnExpChanged?.Invoke(CurrentExp, ExpForLevel(Level)); } catch { }
	}

	private void ApplyStatModifiers()
	{
		if (classData == null) return;
		if (statSystem == null) statSystem = FindObjectOfType<StatSystem>();

		// moveSpeed now uses new 'speed' field on classData
		moveSpeed = statSystem != null ? statSystem.GetMoveSpeed(classData.speed) : classData.speed;

		// MaximumHealth now derived from Vigor
		if (statSystem != null)
			MaximumHealth = Mathf.RoundToInt(statSystem.GetVigor(classData.Vigor));
		else
			MaximumHealth = classData.Vigor > 0 ? classData.Vigor : 100; // fallback default

		// Attack: compute primary stat base, apply primary stat modifiers and then Damage modifiers
		float primaryBase = 1f;
		switch (classData.primaryStat)
		{
			case StatType.Strength: primaryBase = classData.Strength; break;
			case StatType.Dexterity: primaryBase = classData.Dexterity; break;
			case StatType.Intelligence: primaryBase = classData.Intelligence; break;
			default: primaryBase = 1f; break; // fallback default
		}
		float primaryValue = statSystem != null ? statSystem.GetPrimaryStatForClass(classData, primaryBase) : primaryBase;
		float finalAttackFloat = statSystem != null ? statSystem.GetModifiedStatFloat(primaryValue, StatType.Damage) : primaryValue;
		Attack = Mathf.RoundToInt(finalAttackFloat);

		// CritChance derived from Luck, but keep compatibility with CritChance modifiers
		if (statSystem != null)
			CritChance = statSystem.GetModifiedStatFloat(classData.Luck, StatType.CritChance);
		else
			CritChance = classData.Luck > 0f ? classData.Luck : 10f;

		AttackSpeed = statSystem != null ? statSystem.GetAttackSpeed(classData.attackSpeed) : classData.attackSpeed;
		DamageMultiplier = statSystem != null ? statSystem.GetDamageMultiplier(classData.damageMultiplier) : classData.damageMultiplier;
		ExpGainRate = statSystem != null ? statSystem.GetExpGainRate(classData.expGainRate) : classData.expGainRate;
		HealthRegen = statSystem != null ? statSystem.GetHealthRegen(classData.healthRegenPerSecond) : classData.healthRegenPerSecond;

		// let behaviour apply any additional stat changes
		if (classBehaviour != null)
			classBehaviour.ApplyStats();

		// update inspector-visible mirrors
		UpdateInspectorFields();
	}

	void Update()
	{
		if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return; // disable input when game over

		movement.x = Input.GetAxisRaw("Horizontal");
		movement.y = Input.GetAxisRaw("Vertical");
		movement.Normalize();
		if (animator != null) animator.SetBool("bIsMoving", movement != Vector2.zero);

		// health regen
		if (HealthRegen > 0f && CurrentHealth < MaximumHealth)
		{
			healthRegenAccumulator += Time.deltaTime * HealthRegen;
			if (healthRegenAccumulator >= 1f)
			{
				int heal = Mathf.FloorToInt(healthRegenAccumulator);
				CurrentHealth = Mathf.Min(MaximumHealth, CurrentHealth + heal);
				healthRegenAccumulator -= heal;
				// update UI
				if (playerHealthContainerUISystem != null && playerHealthFillUISystem != null) DrawHealthUI();

				// update inspector mirrors when health changes
				UpdateInspectorFields();
			}
		}

		// delegate class-specific input (e.g. ranger charge & fire)
		if (classBehaviour != null)
			classBehaviour.HandleInput();

		ProjectileSpawnRotationHandle();
	}

	void FixedUpdate()
	{
		if (rb != null) rb.linearVelocity = movement * moveSpeed;
	}

	private void LateUpdate()
	{
		if (spriteRenderer == null) return;
		if (movement.x > 0) spriteRenderer.flipX = true;
		else if (movement.x < 0) spriteRenderer.flipX = false;
	}

	public void TakeDamage(int damageAmount)
	{
		if (isDying) return; // ignore incoming damage while dying

		CurrentHealth -= damageAmount;
		if (CurrentHealth < 0)
		{
			CurrentHealth = 0;
		}
		// Optionally trigger hurt animation, sound, UI update, etc.
		DrawHealthUI();

		// update inspector mirrors when health changes
		UpdateInspectorFields();

		// flash red on player sprite
		if (spriteRenderer != null)
		{
			if (damageFlashRoutine != null) StopCoroutine(damageFlashRoutine);
			damageFlashRoutine = StartCoroutine(PlayerDamageFlash());
		}

		// if player died, trigger game over
		if (CurrentHealth <= 0)
		{
			isDying = true;
			// trigger game over via GameManager
			var gm = GameManager.Instance;
			if (gm != null)
			{
				gm.TriggerGameOver(this.gameObject);
			}
		}
	}

	// Adapter implementation so projectiles can damage the player
	// ProjectileBaseClassScript will call this signature when hitting the player.
	public void TakeDamage(float amount, bool wasCritical, CharacterClass sourceClass, GameObject source)
	{
		// Ignore self-fired projectiles (owner may be the player or a child of the player)
		if (source != null)
		{
			if (source == this.gameObject) return;
			if (source.transform.IsChildOf(this.transform)) return;
		}

		if (isDying) return; // ignore damage during death

		int intAmount = Mathf.CeilToInt(amount);
		TakeDamage(intAmount);
	}

	private IEnumerator PlayerDamageFlash()
	{
		if (spriteRenderer == null) yield break;
		Color orig = originalSpriteColor;
		spriteRenderer.color = new Color(1f, 0f, 0f, orig.a);
		float t = 0f;
		while (t < playerDamageFlashDuration)
		{
			t += Time.deltaTime;
			yield return null;
		}
		// restore
		spriteRenderer.color = orig;
		damageFlashRoutine = null;
	}

	public void DrawHealthUI()
	{
		if (playerHealthContainerUISystem != null)
			playerHealthContainerUISystem.DrawHeartContainer(CurrentHealth, MaximumHealth);
		if (playerHealthFillUISystem != null)
			playerHealthFillUISystem.DrawHeartFill(CurrentHealth, MaximumHealth);
	}
	
	// EXP formula per design
	// Made internal so UI/controller can query total for current level
	internal float ExpForLevel(int L)
	{
		return 199f * Mathf.Pow(L,2.69f);
	}

	// Add experience and handle level up
	public void AddExp(float amount)
	{
		if (amount <=0f) return;
		Debug.Log($"CharacterInput.AddExp: adding {amount} EXP to {gameObject.name}. Before: Level={Level} CurrentExp={CurrentExp}");
		CurrentExp += amount;
		// loop to handle multiple level ups
		while (CurrentExp >= ExpForLevel(Level))
		{
			CurrentExp -= ExpForLevel(Level);
			Level++;
			Debug.Log($"CharacterInput.AddExp: Leveled up to {Level} for {gameObject.name}");
			try { OnLevelUp?.Invoke(Level); } catch { }
		}
		Debug.Log($"CharacterInput.AddExp: After: Level={Level} CurrentExp={CurrentExp} (Needed for next={ExpForLevel(Level)})");
		OnExpChanged?.Invoke(CurrentExp, ExpForLevel(Level));
		UpdateInspectorFields();

		// If using hero profiles with persistence, save updated progression
		if (heroProfile != null && HeroProgressionManager.Instance != null)
		{
			HeroProgressionManager.Instance.SaveForProfile(heroProfile, Level, CurrentExp);
		}
	}

	public float GetExpToNextLevel()
	{
		return ExpForLevel(Level) - CurrentExp;
	}

	public float GetExpProgress01()
	{
		float needed = ExpForLevel(Level);
		return needed >0f ? Mathf.Clamp01(CurrentExp / needed) :0f;
	}


	// Attach appropriate behaviour component based on selectedClass if none assigned or if assigned behaviour doesn't match
	private void AttachBehaviourForSelectedClass()
	{
		// If already assigned, check if it matches selectedClass
		if (classBehaviour != null)
		{
			if (selectedClass == CharacterClass.Ranger && classBehaviour is RangerBehaviour) return;
			if (selectedClass == CharacterClass.Warrior && classBehaviour.GetType().Name.Contains("Warrior")) return;
			if (selectedClass == CharacterClass.Mage && classBehaviour.GetType().Name.Contains("Mage")) return;
		}

		// Remove any existing PlayerClassBehaviour on this GameObject (we will add the correct one)
		var existing = GetComponents<PlayerClassBehaviour>();
		foreach (var ex in existing)
		{
			DestroyImmediate(ex);
		}

		// Add appropriate behaviour component
		switch (selectedClass)
		{
			case CharacterClass.Ranger:
				classBehaviour = gameObject.AddComponent<RangerBehaviour>();
				break;
			case CharacterClass.Warrior:
				classBehaviour = gameObject.AddComponent<GenericClassBehaviour>();
				break;
			case CharacterClass.Mage:
				// fallback to a simple generic behaviour that does nothing until specialized behaviours are implemented
				classBehaviour = gameObject.AddComponent<GenericClassBehaviour>();
				break;
			default:
				Debug.LogWarning($"CharacterInput: Unhandled selectedClass {selectedClass}");
				break;
		}

		// assign classData to behaviour if available
		if (classBehaviour != null && classData != null)
			classBehaviour.classData = classData;
	}

	// Copy runtime values into serialized fields so they're visible in the inspector during play
	private void UpdateInspectorFields()
	{
		inspector_moveSpeed = moveSpeed;
		inspector_MaximumHealth = MaximumHealth;
		inspector_CurrentHealth = CurrentHealth;
		inspector_Attack = Attack;
		inspector_CritChance = CritChance;
		inspector_AttackSpeed = AttackSpeed;
		inspector_DamageMultiplier = DamageMultiplier;
		inspector_ExpGainRate = ExpGainRate;
		inspector_HealthRegen = HealthRegen;
		inspector_Level = Level;
		inspector_CurrentExp = CurrentExp;
		inspector_ExpToNext = GetExpToNextLevel();
	}

	private void ProjectileSpawnRotationHandle()
	{
		// Ensure that the Spawnpoint follows mouse cursor direction, and that it constantly Orbits the Player.

		if (projectileSpawnpointGameObject == null || projectileSpawnpointTransform == null)
		{
			return;
		}

		Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
		Vector3 direction = mousePosition - transform.position;
		direction.z = 0f;
		direction.Normalize();
		float distance = Mathf.Clamp(Vector3.Distance(mousePosition, transform.position), minProjectileSpawnDistance, maxProjectileSpawnDistance);
		projectileSpawnpointTransform.position = transform.position + direction * distance;

		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
		projectileSpawnpointTransform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));

    }
}
