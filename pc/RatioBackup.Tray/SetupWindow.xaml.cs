using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace RatioBackup.Tray;

public partial class SetupWindow : Window
{
    public string Folder => FolderBox.Text.Trim();
    public int QuotaGb { get; private set; } = 100;

    public SetupWindow()
    {
        InitializeComponent();
        Theme.ApplyTitleBar(this);
        FolderBox.Text = SuggestFolder();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner für die Handy-Sicherung" };
        if (dlg.ShowDialog(this) == true) FolderBox.Text = Path.Combine(dlg.FolderName, "RatioBackup");
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Folder.Length == 0 || !Path.IsPathRooted(Folder)) { ShowError("Bitte einen vollständigen Ordnerpfad wählen."); return; }
        if (!int.TryParse(QuotaBox.Text.Trim(), out var gb) || gb < 5 || gb > 4000) { ShowError("Speicherlimit: bitte eine Zahl zwischen 5 und 4000."); return; }
        try { Directory.CreateDirectory(Folder); }
        catch (Exception ex) { ShowError($"Ordner kann nicht angelegt werden: {ex.Message}"); return; }
        QuotaGb = gb;
        DialogResult = true;
    }

    private void ShowError(string text)
    {
        Error.Text = text;
        Error.Visibility = Visibility.Visible;
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
