namespace RatioBackup.Tray;

/// <summary>Erster Start: Ablageordner und Speicherlimit wählen.</summary>
internal sealed class SetupForm : Form
{
    private readonly TextBox _folder = new() { Width = 340 };
    private readonly NumericUpDown _quota = new() { Minimum = 5, Maximum = 4000, Value = 100, Width = 80 };
    private readonly Button _ok = new() { Text = "Weiter", DialogResult = DialogResult.OK, Width = 100 };

    public string Folder => _folder.Text.Trim();
    public int QuotaGb => (int)_quota.Value;

    public SetupForm()
    {
        Text = "RatioBackup einrichten";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var browse = new Button { Text = "Durchsuchen…", Width = 110 };
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Ordner für die Handy-Sicherung", UseDescriptionForTitle = true, SelectedPath = Folder };
            if (dlg.ShowDialog(this) == DialogResult.OK) _folder.Text = Path.Combine(dlg.SelectedPath, "RatioBackup");
        };
        _folder.Text = SuggestFolder();
        _folder.TextChanged += (_, _) => _ok.Enabled = Folder.Length > 0;

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "Wohin soll das Handy gesichert werden?", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 2);
        layout.Controls.Add(new Label { Text = "Am besten ein Laufwerk mit viel Platz, nicht C:.", AutoSize = true }, 0, 1);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 1)!, 2);
        layout.Controls.Add(_folder, 0, 2);
        layout.Controls.Add(browse, 1, 2);
        var quotaRow = new FlowLayoutPanel { AutoSize = true };
        quotaRow.Controls.Add(new Label { Text = "Speicherlimit (GB):", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        quotaRow.Controls.Add(_quota);
        layout.Controls.Add(quotaRow, 0, 3);
        layout.SetColumnSpan(quotaRow, 2);
        layout.Controls.Add(_ok, 1, 4);
        Controls.Add(layout);
        AcceptButton = _ok;
    }

    private static string SuggestFolder()
    {
        var other = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed && !d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.AvailableFreeSpace)
            .FirstOrDefault();
        var basePath = other?.RootDirectory.FullName ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(basePath, "RatioBackup");
    }
}
