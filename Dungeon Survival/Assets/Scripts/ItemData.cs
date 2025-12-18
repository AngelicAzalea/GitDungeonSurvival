using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Game/Item Data", order =200)]
public class ItemData : ScriptableObject
{
 public string id; // unique id for persistent unlocks
 public string itemName;
 public Sprite icon;
 [TextArea] public string description;
 public StatModifier[] modifiers;
}
