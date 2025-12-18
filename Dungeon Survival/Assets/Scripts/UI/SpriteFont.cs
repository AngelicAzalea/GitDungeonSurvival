using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteFont", menuName = "UI/Sprite Font", order =200)]
public class SpriteFont : ScriptableObject
{
 [Serializable]
 public struct CharSprite
 {
 public string key; // use string to allow ":" or "!" etc.
 public Sprite sprite;
 }

 public CharSprite[] chars = new CharSprite[0];

 // runtime lookup (built on demand)
 private System.Collections.Generic.Dictionary<string, Sprite> map;

 private void EnsureMap()
 {
 if (map != null) return;
 map = new System.Collections.Generic.Dictionary<string, Sprite>(StringComparer.Ordinal);
 if (chars != null)
 {
 foreach (var e in chars)
 {
 if (string.IsNullOrEmpty(e.key) || e.sprite == null) continue;
 if (!map.ContainsKey(e.key)) map[e.key] = e.sprite;
 }
 }
 }

 // key should be a single-character string for letters or punctuation like ":" or "!"
 public Sprite GetSpriteFor(char c)
 {
 EnsureMap();
 string k = c.ToString();
 if (map.TryGetValue(k, out var s)) return s;
 // try uppercase fallback
 k = k.ToUpperInvariant();
 if (map.TryGetValue(k, out s)) return s;
 return null;
 }

 // editor helper
#if UNITY_EDITOR
 public void RebuildMapFromArray()
 {
 map = null;
 EnsureMap();
 }
#endif
}
