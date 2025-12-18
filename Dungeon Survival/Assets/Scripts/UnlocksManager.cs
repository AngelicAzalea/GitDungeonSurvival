using System.Collections.Generic;
using UnityEngine;

public class UnlocksManager : MonoBehaviour
{
 private const string PlayerPrefsKey = "UnlockedItems_v1";
 private HashSet<string> unlocked = new HashSet<string>();

 private void Awake()
 {
 Load();
 }

 public bool IsUnlocked(string id)
 {
 return unlocked.Contains(id);
 }

 public void Unlock(string id)
 {
 if (string.IsNullOrEmpty(id)) return;
 if (unlocked.Add(id)) Save();
 }

 public void Lock(string id)
 {
 if (string.IsNullOrEmpty(id)) return;
 if (unlocked.Remove(id)) Save();
 }

 public IEnumerable<string> GetAllUnlocked() => unlocked;

 public void Save()
 {
 var arr = new List<string>(unlocked);
 var json = JsonUtility.ToJson(new SerializationWrapper { ids = arr });
 PlayerPrefs.SetString(PlayerPrefsKey, json);
 PlayerPrefs.Save();
 }

 public void Load()
 {
 unlocked.Clear();
 var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
 if (string.IsNullOrEmpty(json)) return;
 try
 {
 var wrap = JsonUtility.FromJson<SerializationWrapper>(json);
 if (wrap?.ids != null)
 {
 foreach (var id in wrap.ids) unlocked.Add(id);
 }
 }
 catch { }
 }

 [System.Serializable]
 private class SerializationWrapper { public List<string> ids; }
}
