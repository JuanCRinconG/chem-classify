using Godot;

public partial class UIToggleButton : Button
{
    private UIBindings _ButtonBindings = new();

    [Export]
    public Control[] PrimaryControls;

    [Export]
    public Control[] SecondaryControls;

    private bool _HasAllConfigs = true;

    public override void _EnterTree()
    {
        _ButtonBindings.BindButton(this, OnToggleButtonPressed);  
    }
    public override void _Ready()
    {
        _HasAllConfigs = ValidateControlArray(PrimaryControls) && ValidateControlArray(SecondaryControls);
    }

    public override void _ExitTree()
    {
        _ButtonBindings.Clear();
    }

    private bool ValidateControlArray(Control[] controls)
    {
        if (controls == null)
        {
            return false;
        }

        if(controls.Length == 0)
        {
            return true; // Empty array is valid
        }

        foreach(Control node in controls)
        {
            if(!IsInstanceValid(node))
            {
                return false;
            }
        }
        return true;
    }

    private void ToggleControlVisibility(Control[] controls)
    {
        foreach(Control node in controls)
        {
            if(IsInstanceValid(node))
            {
                node.Visible = !node.Visible;
            }
        }
    }

    private void OnToggleButtonPressed()
    {
        if(!_HasAllConfigs)
        {
            return;
        }

        ToggleControlVisibility(PrimaryControls);
        ToggleControlVisibility(SecondaryControls);
    }
}