#nullable enable
using System;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Admin SDS registration UI. Collects form fields and delegates the full write path
/// (OneDrive PDF upload + Firestore metadata) to <see cref="FirebaseSds.RegisterAsync"/>.
/// </summary>
public partial class PDFRegisterCore : Control
{
    private UIBindings _bindings = new();
    private string? _selectedPdfPath;

    [Export]
    public LineEdit DisplayNameInput = null!;

    [Export]
    public LineEdit CasInput = null!;

    [Export]
    public LineEdit HazardInput = null!;

    [Export]
    public SpinBox ExpiresYear = null!;

    [Export]
    public SpinBox ExpiresMonth = null!;

    [Export]
    public SpinBox ExpiresDay = null!;

    [Export]
    public Label SelectedFileLabel = null!;

    [Export]
    public Button PickFileButton = null!;

    [Export]
    public Label SdsIdPreviewLabel = null!;

    [Export]
    public Button ClearButton = null!;

    [Export]
    public Button RegisterButton = null!;

    [Export]
    public ErrorData? RegisterError;

    public override void _Ready()
    {
        _bindings.BindButton(PickFileButton, OnPickFile);
        _bindings.BindButton(ClearButton, OnClear);
        _bindings.BindButton(RegisterButton, OnRegister);
    }

    public override void _ExitTree()
    {
        _bindings.Clear();
    }

    private void OnPickFile()
    {
        var dialog = new FileDialog
        {
            Title = "Select SDS PDF",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = new[] { "*.pdf ; PDF Files" }
        };

        dialog.FileSelected += path =>
        {
            _selectedPdfPath = path;
            SelectedFileLabel.Text = System.IO.Path.GetFileName(path);
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private async void OnRegister()
    {
        RegisterButton.Disabled = true;
        await RegisterAsync();
        RegisterButton.Disabled = false;
    }

    private async Task RegisterAsync()
    {
        if (ValidateForm() is string formError)
        {
            ShowError(formError);
            return;
        }

        if (!TryBuildExpirationDate(out DateTime expiresAt))
        {
            ShowError("Expiration date is invalid.");
            return;
        }

        var request = new SdsRegisterRequest(
            DisplayName: DisplayNameInput.Text.Trim(),
            LocalPdfPath: _selectedPdfPath!,
            ExpiresAt: expiresAt,
            CasNumber: NullIfWhiteSpace(CasInput.Text),
            HazardSummary: NullIfWhiteSpace(HazardInput.Text),
            CreatedByUid: UserSession.Current?.Uid);

        SdsRegisterResult result = await FirebaseSds.RegisterAsync(request);
        if (!result.Ok)
        {
            ShowError(result.ErrorMessage ?? "Registration failed.");
            return;
        }

        SdsIdPreviewLabel.Text = $"SDS id (assigned on save): {result.SdsId}";
        GD.Print($"SDS registered: {result.SdsId}");
    }

    private void OnClear()
    {
        DisplayNameInput.Text = string.Empty;
        CasInput.Text = string.Empty;
        HazardInput.Text = string.Empty;
        ExpiresYear.Value = DateTime.Today.Year;
        ExpiresMonth.Value = DateTime.Today.Month;
        ExpiresDay.Value = DateTime.Today.Day;
        _selectedPdfPath = null;
        SelectedFileLabel.Text = "No file selected";
        SdsIdPreviewLabel.Text = "SDS id (assigned on save): —";
    }

    private string? ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(DisplayNameInput.Text))
            return "Chemical / product name is required.";

        if (string.IsNullOrWhiteSpace(_selectedPdfPath))
            return "Select an SDS PDF file.";

        if (!TryBuildExpirationDate(out DateTime expiresAt))
            return "Expiration date is invalid.";

        if (expiresAt.Date < DateTime.Today)
            return "Expiration date cannot be in the past.";

        return null;
    }

    private bool TryBuildExpirationDate(out DateTime expiresAt)
    {
        int year = (int)ExpiresYear.Value;
        int month = (int)ExpiresMonth.Value;
        int day = (int)ExpiresDay.Value;

        try
        {
            expiresAt = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            expiresAt = default;
            return false;
        }
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ShowError(string message)
    {
        if (RegisterError != null)
            RegisterError.Show(message, this);
        else
            GD.PrintErr(message);
    }
}
