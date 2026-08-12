using System;
using System.Collections.Generic;
using System.Transactions;

public partial class TwoFactorAuthenticator : Control
{
    [Export]
    public Godot.Collections.Array<SpinBox> FactorFields;

    [Export]
    public Button ConfirmButton;

    [Export]
    public Button ResendCodeButton;

    private Authenticator RuntimeAuthentication = new();

    public override void _Ready()
    {
        if (!RuntimeAuthentication.GenerateCode(FactorFields))
        {
            return;
        }
    }

    private sealed class Authenticator
    {
        private MailData CodeMail = new();
        
        private int FactorFieldCount = 0;

        private List<int> FactorKeys = new();

        private Random RNGGen = new Random();

        public bool GenerateCode(Godot.Collections.Array<SpinBox> Factors)
        {
            if (Factors == null)
            {
                GD.PrintErr("factor fields not present");
                return false;
            }
            FactorFieldCount = 0;
            FactorKeys.Clear();

            foreach(SpinBox box in Factors)
            {
                if (box == null)
                {
                    GD.PrintErr("a spinbox was null");
                    return false;
                }
                FactorFieldCount += 1;
            }

            for (int i = 0; i < FactorFieldCount; i++)
            {
                FactorKeys.Add(RNGGen.Next(1, 9));
            }
            return true;
        }

        public bool TrySendCodeMail()
        {
            return true;
        }
    }
}