using System;
using System.Collections.Generic;

public partial class TwoFactorAuthenticator : Control
{
    private UIBindings TFABindings = new();

    [Export]
    public Godot.Collections.Array<SpinBox> FactorFields;

    [Export]
    public Button ConfirmButton;

    [Export]
    public Button ResendCodeButton;

    [Export]
    public Button ReturnButton;

    [Export]
    public ErrorData TFAError;

    private Authenticator RuntimeAuthentication = new();

    public string ReceiverMail;

    public override void _Ready()
    {
        TFABindings.BindButton(ResendCodeButton, AuthenticationCommand);
        TFABindings.BindButton(ConfirmButton, OnConfirmFields);
        AuthenticationCommand();
    }

    public override void _ExitTree()
    {
        TFABindings.Clear();
    }

    private void AuthenticationCommand()
    {
        if (!RuntimeAuthentication.GenerateCode(FactorFields))
        {
            GD.PrintErr("Could not generate the code");
            return;
        }
        if (!RuntimeAuthentication.TrySendCodeMail(ReceiverMail))
        {
            GD.PrintErr("Could not send mail");
            return;
        }
    }

    private void OnConfirmFields()
    {
        if (!RuntimeAuthentication.VerifyCode(FactorFields))
        {
            TFAError.Show("The code does not match", this);
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

        public bool VerifyCode(Godot.Collections.Array<SpinBox> Factors)
        {
            if (Factors == null)
            {
                GD.PrintErr("factor fields not present");
                return false;
            }

            for(int i = 0; i < FactorFieldCount; i++)
            {
                SpinBox CurrentBox = Factors[i];
                if (CurrentBox == null)
                {
                    GD.PrintErr("a spinbox was null");
                    return false;
                }
                if (CurrentBox.Value != FactorKeys[i])
                {
                    return false;
                }
            }
            return true;   
        }

        public bool TrySendCodeMail(string Receiver)
        {
            if (FactorKeys.Count == 0)
            {
                return false;
            }
            CodeMail.Receiver = Receiver;
            CodeMail.Subject = "Your code for logging into ChemClassify";
            CodeMail.Body = "Your code is " + string.Join("-", FactorKeys);
            CodeMail.SendMail();
            return true;
        }
    }
}