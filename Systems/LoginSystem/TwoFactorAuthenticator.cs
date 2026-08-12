using System.Collections.Generic;

public partial class TwoFactorAuthenticator : Control
{
    [Export]
    Godot.Collections.Array<SpinBox> FactorFields;

    private int FactorFieldCount = 0;

    private List<int> FactorKeys = new();

    public override void _EnterTree()
    {
        if (FactorFields == null)
        {
            GD.PrintErr("factor fields not present");
            return;
        }
        FactorFieldCount = 0;

        foreach(SpinBox box in FactorFields)
        {
            if (box == null)
            {
                GD.PrintErr("a spinbox was null");
                return;
            }
            FactorFieldCount =+ 1;
        }
        
    }
}