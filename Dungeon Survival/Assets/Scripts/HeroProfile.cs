using System;
using UnityEngine;

// Represents a "hero" or character instance profile used for hiring/roster systems in the future.
// This is a lightweight scaffold that stores which class the hero uses and a per-stat growth tier
// (e.g. G..SSS). The system is intentionally data-driven so designers can author randomized
// growth templates and later the game can generate individual heroes from those templates.

public enum GrowthRank
{
	G =0,
	F,
	E,
	D,
	C,
	B,
	A,
	S,
	SS,
	SSS
}

[Serializable]
public struct StatGrowthEntry
{
	public StatType stat;
	public GrowthRank rank;
}

[CreateAssetMenu(fileName = "HeroProfile", menuName = "Game/Hero Profile", order =200)]
public class HeroProfile : ScriptableObject
{
	[Tooltip("Class this hero profile is for (Warrior, Ranger, Mage, etc.)")]
	public CharacterClass classType = CharacterClass.Ranger;

	[Tooltip("Optional base stat overrides for heroes created from this profile.")]
	public CharacterClassData baseClassOverrides;

	[Header("Stat Growth Tiers")]
	[Tooltip("Per-stat growth rank used to guide how stats scale as the hero levels. Not implemented gameplay-wise yet.")]
	public StatGrowthEntry[] statGrowths;

	[Header("Designer notes")]
	[TextArea]
	public string notes;

	[Header("Starting progression")]
	[Tooltip("Starting level for heroes using this profile")]
	public int startingLevel =1;
	[Tooltip("Starting experience toward next level")]
	public float startingExp =0f;

	// Helper: get rank for a given stat (fallback to middle 'C' if not specified)
	public GrowthRank GetGrowthRankFor(StatType stat)
	{
		if (statGrowths != null)
		{
			for (int i =0; i < statGrowths.Length; i++)
			{
				if (statGrowths[i].stat == stat) return statGrowths[i].rank;
			}
		}
		return GrowthRank.C;
	}
}
