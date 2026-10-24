using Godot;

[Tool]
[GlobalClass]
public partial class DisplayName : Resource
{
    [Export(PropertyHint.None, DisplayList.GetDisplay)]
    public StringName Name;
}