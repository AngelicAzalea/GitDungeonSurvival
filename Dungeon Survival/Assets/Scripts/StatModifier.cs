using System;
using UnityEngine;

[Serializable]
public enum StatType
{
 // New naming: `Speed` is the RPG stat for movement. `MoveSpeed` is kept for backward compatibility with older items.
 Speed,
 MoveSpeed,
 MaxHealth,
 Damage,
 ProjectileCount,
 ProjectileSpread, // experimental: spread angle in degrees applied when firing multiple projectiles
 CritChance,

 // additional stats
 AttackSpeed,
 DamageMultiplier,
 ExpGainRate,
 HealthRegenPerSecond,
 PassiveTrait, // generic flag/trait placeholder

 // New RPG-style stats (planned)
 Strength,
 Dexterity,
 Vigor,
 Intelligence,
 Arcane,
 Luck,
 Armor,
 CooldownReduction
}

[Serializable]
public enum ModifierMode
{
 Additive, // value is added
 Multiplicative // value is a multiplier (e.g.1.2 = +20%)
}

[Serializable]
public struct StatModifier
{
 public StatType stat;
 public ModifierMode mode;
 public float value; // additive value or multiplier depending on mode
}
