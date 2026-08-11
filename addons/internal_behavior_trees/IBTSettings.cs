#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTPlugin;

/// <summary>Project settings for the Internal Behavior Trees plugin.</summary>
public static class IBTSettings
{
	public const string IncludeRuntimeEditor = "runtime/include_runtime_editor";
	public const string RuntimeEditorToggleKey = "runtime/editor_toggle_key";

	private const string Prefix = "internal_behavior_trees/";

	private static readonly Key[] ToggleKeys = BuildToggleKeys();
	private static readonly string ToggleKeyHintString = string.Join(",", Array.ConvertAll(ToggleKeys, key => key.ToString()));
	private static readonly int DefaultToggleKeyIndex = Math.Max(0, Array.IndexOf(ToggleKeys, Key.B));

	public static void Prepare()
	{
		RegisterBool(IncludeRuntimeEditor, true);
		RegisterEnum(RuntimeEditorToggleKey, DefaultToggleKeyIndex, ToggleKeyHintString);
	}

	public static bool GetIncludeRuntimeEditor() => GetBool(IncludeRuntimeEditor, true);

	public static Key GetRuntimeEditorToggleKey()
	{
		int index = GetInt(RuntimeEditorToggleKey, DefaultToggleKeyIndex);
		return index >= 0 && index < ToggleKeys.Length ? ToggleKeys[index] : Key.B;
	}

	private static Key[] BuildToggleKeys()
	{
		var keys = new List<Key>();
		foreach (Key key in Enum.GetValues<Key>())
		{
			if (key is Key.None or Key.Special)
			{
				continue;
			}

			keys.Add(key);
		}

		keys.Sort((a, b) => ((int)a).CompareTo((int)b));
		return keys.ToArray();
	}

	private static void RegisterBool(string suffix, bool defaultValue) =>
		RegisterSetting(suffix, defaultValue, Variant.Type.Bool);

	private static void RegisterEnum(string suffix, int defaultIndex, string hintString) =>
		RegisterSetting(
			suffix,
			defaultIndex,
			Variant.Type.Int,
			PropertyHint.Enum,
			hintString);

	private static void RegisterSetting(
		string suffix,
		Variant defaultValue,
		Variant.Type type,
		PropertyHint hint = PropertyHint.None,
		string hintString = "")
	{
		string settingName = Prefix + suffix;
		if (!ProjectSettings.HasSetting(settingName))
		{
			ProjectSettings.SetSetting(settingName, defaultValue);
		}

		ProjectSettings.SetInitialValue(settingName, defaultValue);
		var propertyInfo = new Godot.Collections.Dictionary
		{
			{ "name", settingName },
			{ "type", (int)type },
		};
		if (hint != PropertyHint.None)
		{
			propertyInfo["hint"] = (int)hint;
			propertyInfo["hint_string"] = hintString;
		}

		ProjectSettings.AddPropertyInfo(propertyInfo);
		ProjectSettings.SetAsBasic(settingName, true);
	}

	private static bool GetBool(string suffix, bool defaultValue) =>
		ProjectSettings.HasSetting(Prefix + suffix)
			? ProjectSettings.GetSetting(Prefix + suffix).AsBool()
			: defaultValue;

	private static int GetInt(string suffix, int defaultValue) =>
		ProjectSettings.HasSetting(Prefix + suffix)
			? ProjectSettings.GetSetting(Prefix + suffix).AsInt32()
			: defaultValue;
}
