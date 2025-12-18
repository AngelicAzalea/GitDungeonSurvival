using System;
using System.Collections.Generic;
using UnityEngine;

// Central runtime service that applies item modifiers to a target (CharacterInput)
public class StatSystem : MonoBehaviour
{
	[SerializeField] private ItemInventory runInventory; // ScriptableObject instance stored in project; cleared between runs

	[Header("Debug")] [Tooltip("Enable to log stat modifier evaluation for troubleshooting")]
	public bool debugStatSystem = false;

	public event Action OnInventoryChanged;
	// New: notify when a specific item is added to the run
	public event Action<ItemData> OnItemAdded;

	public void Initialize(ItemInventory inventory)
	{
		runInventory = inventory;
	}

	private void Awake()
	{
		// if not provided via bootstrap, try to find a RunInventory in Resources (fallback)
		if (runInventory == null)
		{
			runInventory = Resources.Load<ItemInventory>("RunInventory");
		}
	}

	// Add item via the stat system so it can trigger notifications
	public void AddItem(ItemData item)
	{
		if (runInventory == null || item == null) return;
		runInventory.AddItem(item);
		if (debugStatSystem)
		{
			Debug.Log($"StatSystem: AddItem '{item.itemName}' with { (item.modifiers?.Length ??0) } modifiers");
			if (item.modifiers != null)
			{
				foreach (var m in item.modifiers) Debug.Log($" - modifier: {m.stat} {m.mode} {m.value}");
			}
		}
		OnInventoryChanged?.Invoke();
		OnItemAdded?.Invoke(item);
	}

	// Allow external callers to signal inventory change
	public void NotifyInventoryChanged()
	{
		OnInventoryChanged?.Invoke();
	}

	// Compute final stat value given base and modifiers
	public float GetModifiedStatFloat(float baseValue, StatType type)
	{
		float result = baseValue;
		if (runInventory == null) return result;

		// apply additive first
		foreach (var item in runInventory.items)
		{
			if (item == null || item.modifiers == null) continue;
			foreach (var mod in item.modifiers)
			{
				if (mod.stat != type) continue;
				if (mod.mode == ModifierMode.Additive) result += mod.value;
			}
		}

		// then multiplicative
		foreach (var item in runInventory.items)
		{
			if (item == null || item.modifiers == null) continue;
			foreach (var mod in item.modifiers)
			{
				if (mod.stat != type) continue;
				if (mod.mode == ModifierMode.Multiplicative) result *= mod.value;
			}
		}

		if (debugStatSystem && (type == StatType.ProjectileCount || type == StatType.Speed || type == StatType.DamageMultiplier))
		{
			Debug.Log($"StatSystem: GetModifiedStatFloat type={type} base={baseValue} => result={result} (inventory items={runInventory.items.Count})");
		}

		return result;
	}

	// Example helper for integer stats
	public int GetModifiedStatInt(int baseValue, StatType type)
	{
		return Mathf.RoundToInt(GetModifiedStatFloat(baseValue, type));
	}

	// Derived helpers for common gameplay values
	public int GetProjectileCount(int baseCount)
	{
		return GetModifiedStatInt(baseCount, StatType.ProjectileCount);
	}

	public float GetCritChance(float baseCrit)
	{
		// Prefer Luck stat as the RPG driver for crit chance; fall back to legacy CritChance if no modifiers.
		float r = GetModifiedStatFloat(baseCrit, StatType.Luck);
		if (Mathf.Approximately(r, baseCrit))
		{
			r = GetModifiedStatFloat(baseCrit, StatType.CritChance);
		}
		return r;
	}

	public float GetMoveSpeed(float baseSpeed)
	{
		// Prefer new RPG stat `Speed` if modifiers exist, otherwise fall back to legacy `MoveSpeed` stat.
		float r = GetModifiedStatFloat(baseSpeed, StatType.Speed);
		// If result equals baseSpeed (no modifiers applied) check legacy MoveSpeed modifiers
		if (Mathf.Approximately(r, baseSpeed))
		{
			r = GetModifiedStatFloat(baseSpeed, StatType.MoveSpeed);
		}
		return r;
	}

	public int GetMaxHealth(int baseMax)
	{
		// Prefer Vigor stat as the RPG driver for max health; fall back to legacy MaxHealth if no modifiers.
		int r = GetModifiedStatInt(baseMax, StatType.Vigor);
		if (r == baseMax)
		{
			r = GetModifiedStatInt(baseMax, StatType.MaxHealth);
		}
		return r;
	}

	public float GetAttackSpeed(float baseAttackSpeed)
	{
		return GetModifiedStatFloat(baseAttackSpeed, StatType.AttackSpeed);
	}

	public float GetDamageMultiplier(float baseMultiplier)
	{
		return GetModifiedStatFloat(baseMultiplier, StatType.DamageMultiplier);
	}

	public float GetExpGainRate(float baseRate)
	{
		return GetModifiedStatFloat(baseRate, StatType.ExpGainRate);
	}

	public float GetHealthRegen(float baseRegen)
	{
		return GetModifiedStatFloat(baseRegen, StatType.HealthRegenPerSecond);
	}

	// Convenience getters for new RPG-style stats
	public float GetStrength(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Strength);
	public float GetDexterity(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Dexterity);
	public float GetVigor(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Vigor);
	public float GetIntelligence(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Intelligence);
	public float GetArcane(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Arcane);
	public float GetLuck(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Luck);
	public float GetArmor(float baseValue) => GetModifiedStatFloat(baseValue, StatType.Armor);
	public float GetCooldownReduction(float baseValue) => GetModifiedStatFloat(baseValue, StatType.CooldownReduction);

	// Additional convenience: get primary stat value depending on CharacterClassData
	public float GetPrimaryStatForClass(CharacterClassData data, float baseValue)
	{
		if (data == null) return baseValue;
		switch (data.primaryStat)
		{
			case StatType.Strength: return GetStrength(baseValue);
			case StatType.Dexterity: return GetDexterity(baseValue);
			default: return GetModifiedStatFloat(baseValue, data.primaryStat);
		}
	}
}
