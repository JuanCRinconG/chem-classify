using Godot;
using DPM.System;

[Tool]
[GlobalClass]
public partial class DisplayList : Resource
{
    [Export]
    public Godot.Collections.Dictionary<StringName, PackedScene> List;

	public const string GetDisplay = Operations.HintPrefix + nameof(DisplayNames);

    public bool Contains(StringName displayName)
    {
        return TryGet(displayName, out _);
    }

    public bool TryGet(StringName displayName, out PackedScene scene)
    {
        scene = null;
        if (List == null || string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        return List.TryGetValue(displayName, out scene) && scene != null;
    }

    public Result DisplayNames(Walk path)
    {
        if (!path.Done)
        {
            return Result.Fail(Result.DefaultPathTooLongFailure);
        }

        return Result.Terminal(Operations.SortStringNameKeys(List?.Keys));
    }
}