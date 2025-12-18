using UnityEngine;

// Central definition for CharacterClass plus helpers.
// This file was intended to hold the enum previously declared inside `CharacterInput`
// and provide convenience extension methods for loading default class data.
public enum CharacterClass
{
 Unknown,
 Warrior,
 Ranger,
 Mage
}

public static class CharacterClassExtensions
{
 // Return the default Resources path for a CharacterClass's CharacterClassData asset.
 public static string DefaultResourcePath(this CharacterClass cls)
 {
 switch (cls)
 {
 case CharacterClass.Warrior: return "ClassData/WarriorData";
 case CharacterClass.Ranger: return "ClassData/RangerData";
 case CharacterClass.Mage: return "ClassData/MageData";
 default: return "";
 }
 }

 // Convenience to load the default CharacterClassData from Resources.
 public static CharacterClassData LoadDefaultData(this CharacterClass cls)
 {
 var path = cls.DefaultResourcePath();
 if (string.IsNullOrEmpty(path)) return null;
 return Resources.Load<CharacterClassData>(path);
 }
}
