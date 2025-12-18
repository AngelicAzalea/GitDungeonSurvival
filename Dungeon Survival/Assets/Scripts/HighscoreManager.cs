using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HighscoreEntry
{
 public int score;
 public float timeSeconds;
 public List<string> itemsCollected;
 public string timestamp;
}

[Serializable]
public class HighscoreList
{
 public List<HighscoreEntry> entries = new List<HighscoreEntry>();
}

// Simple persistent highscore manager that stores a JSON list in PlayerPrefs.
public class HighscoreManager : MonoBehaviour
{
 public static HighscoreManager Instance { get; private set; }

 [Tooltip("PlayerPrefs key used to persist highscores")] public string prefsKey = "HighscoresData";
 [Tooltip("Maximum number of top entries to keep")] public int maxEntries =10;

 private HighscoreList list = new HighscoreList();

 private void Awake()
 {
 if (Instance != null && Instance != this)
 {
 Destroy(gameObject);
 return;
 }
 Instance = this;
 DontDestroyOnLoad(gameObject);
 Load();
 }

 public void Load()
 {
 list = new HighscoreList();
 if (PlayerPrefs.HasKey(prefsKey))
 {
 string json = PlayerPrefs.GetString(prefsKey);
 try
 {
 list = JsonUtility.FromJson<HighscoreList>(json) ?? new HighscoreList();
 }
 catch (Exception ex)
 {
 Debug.LogWarning($"HighscoreManager: failed to parse json: {ex.Message}");
 list = new HighscoreList();
 }
 }
 }

 public void Save()
 {
 string json = JsonUtility.ToJson(list);
 PlayerPrefs.SetString(prefsKey, json);
 PlayerPrefs.Save();
 }

 public IReadOnlyList<HighscoreEntry> GetEntries() => list.entries.AsReadOnly();

 // Add a new entry and trim to maxEntries (sorted by score desc)
 public void AddEntry(int score, float timeSeconds, List<string> itemsCollected)
 {
 var e = new HighscoreEntry
 {
 score = score,
 timeSeconds = timeSeconds,
 itemsCollected = itemsCollected != null ? new List<string>(itemsCollected) : new List<string>(),
 timestamp = DateTime.UtcNow.ToString("u")
 };

 list.entries.Add(e);
 // sort descending by score, then ascending by time
 list.entries.Sort((a, b) =>
 {
 int cmp = b.score.CompareTo(a.score);
 if (cmp !=0) return cmp;
 return a.timeSeconds.CompareTo(b.timeSeconds);
 });

 if (list.entries.Count > maxEntries) list.entries.RemoveRange(maxEntries, list.entries.Count - maxEntries);
 Save();
 }

 // Clear all saved highscores
 public void ClearAll()
 {
 list.entries.Clear();
 PlayerPrefs.DeleteKey(prefsKey);
 }
}
