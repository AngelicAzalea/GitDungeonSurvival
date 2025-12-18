using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Simple persistent storage for per-HeroProfile progression (level + currentExp).
// Saves a JSON file to Application.persistentDataPath/hero_progress.json
public class HeroProgressionManager : MonoBehaviour
{
	public static HeroProgressionManager Instance { get; private set; }

	[Tooltip("Filename used for saving hero progression data")]
	public string saveFilename = "hero_progress.json";

	private string SavePath => Path.Combine(Application.persistentDataPath, saveFilename);

	[System.Serializable]
	public class HeroSaveEntry
	{
		public string profileId;
		public int level;
		public float currentExp;
	}

	[System.Serializable]
	public class HeroSaveCollection
	{
		public List<HeroSaveEntry> heroes = new List<HeroSaveEntry>();
	}

	private HeroSaveCollection collection = new HeroSaveCollection();
	private Dictionary<string, HeroSaveEntry> lookup = new Dictionary<string, HeroSaveEntry>();

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(this);
			return;
		}
		Instance = this;
		LoadFromDisk();
	}

	private void LoadFromDisk()
	{
		lookup.Clear();
		collection = new HeroSaveCollection();
		if (!File.Exists(SavePath)) return;
		try
		{
			string json = File.ReadAllText(SavePath);
			collection = JsonUtility.FromJson<HeroSaveCollection>(json) ?? new HeroSaveCollection();
			foreach (var e in collection.heroes)
			{
				if (string.IsNullOrEmpty(e.profileId)) continue;
				lookup[e.profileId] = e;
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"HeroProgressionManager: Failed to load progression file: {ex.Message}");
			collection = new HeroSaveCollection();
		}
	}

	private void WriteToDisk()
	{
		try
		{
			string json = JsonUtility.ToJson(collection, prettyPrint: true);
			File.WriteAllText(SavePath, json);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"HeroProgressionManager: Failed to write progression file: {ex.Message}");
		}
	}

	// Use a stable profile id. If profile.profileId is empty, fallback to profile.name.
	private string GetProfileKey(HeroProfile profile)
	{
		if (profile == null) return null;
		if (!string.IsNullOrEmpty(profile.name)) return profile.name;
		return null;
	}

	// Save progression for a given HeroProfile
	public void SaveForProfile(HeroProfile profile, int level, float currentExp)
	{
		if (profile == null) return;
		string key = GetProfileKey(profile);
		if (string.IsNullOrEmpty(key)) return;

		HeroSaveEntry entry;
		if (!lookup.TryGetValue(key, out entry))
		{
			entry = new HeroSaveEntry { profileId = key, level = level, currentExp = currentExp };
			collection.heroes.Add(entry);
			lookup[key] = entry;
		}
		else
		{
			entry.level = level;
			entry.currentExp = currentExp;
		}
		WriteToDisk();
	}

	// Load progression for a given profile. Returns true if saved data exists.
	public bool LoadForProfile(HeroProfile profile, out int level, out float currentExp)
	{
		level =0; currentExp =0f;
		if (profile == null) return false;
		string key = GetProfileKey(profile);
		if (string.IsNullOrEmpty(key)) return false;
		if (lookup.TryGetValue(key, out var entry))
		{
			level = entry.level;
			currentExp = entry.currentExp;
			return true;
		}
		return false;
	}

	// Optional: clear saved data (for debugging)
	public void ClearAll()
	{
		collection = new HeroSaveCollection();
		lookup.Clear();
		WriteToDisk();
	}
}
