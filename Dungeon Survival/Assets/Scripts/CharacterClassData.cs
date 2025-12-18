using UnityEngine;

[CreateAssetMenu(fileName = "CharacterClassData", menuName = "Game/Character Class Data", order = 100)]
public class CharacterClassData : ScriptableObject
{
	public CharacterClass classType;

	[Header("Core Stats (example)")]
	// Movement base (replaces old moveSpeed)
	public float speed = 5f;

	// Legacy numeric core stats removed in favor of RPG-style stats below.

	// added rich stats to align with StatSystem
	public float attackSpeed = 1.0f;
	public float damageMultiplier = 1.0f;
	public float expGainRate = 1.0f;
	public float healthRegenPerSecond = 0f;

	[Header("Visuals / VFX")]
	public Sprite classPortrait;
	public GameObject abilityPrefab;
	public Sprite classProjectile;
	public GameObject ProjectileObject;

	[Header("Class metadata")]
	// Primary stat for the class: Strength or Dexterity (used for future scaling)
	public StatType primaryStat = StatType.Dexterity; // default to DEX (Ranger)

	[Header("RPG-style stats (primary)")]
	[Tooltip("Strength � physical power; typical for melee classes")] public int Strength = 0;
	[Tooltip("Dexterity � ranged/precision power; typical for ranger")] public int Dexterity = 0;
	[Tooltip("Vigor � governs max health")] public int Vigor = 0;
	[Tooltip("Intelligence � governs max mana / spell potency")] public int Intelligence = 0;
	[Tooltip("Arcane � governs mana recovery / regen")] public int Arcane = 0;
	[Tooltip("Luck � affects crit rate and other chance effects")] public float Luck = 0f;
	[Tooltip("Armor � flat damage reduction or source for DR calculations")] public int Armor = 0;
	[Tooltip("CooldownReduction � percent reduction to ability cooldowns (0..1) ")] public float CooldownReduction = 0f;

	[Header("Combat")]
	[Tooltip("Critical damage multiplier applied on crits")]
	public float critMultiplier = 1.5f;

	// OnValidate kept minimal to avoid referencing removed legacy fields
	private void OnValidate()
	{
		// ensure sensible defaults
		if (speed <= 0f) speed = 5f;
		if (Vigor <= 0 && maxHealthFallback > 0) Vigor = maxHealthFallback;
	}

	// Editor-only temporary fallback used during migration (not serialized to disk changes)
	[SerializeField, HideInInspector] private int maxHealthFallback = 100;
}
