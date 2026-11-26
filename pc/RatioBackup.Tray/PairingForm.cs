using RatioBackup.Core;

namespace RatioBackup.Tray;

/// <summary>Zeigt den 6-stelligen Kopplungscode, bis das Handy gekoppelt ist.</summary>
internal sealed class PairingForm : Form
{
    private readonly PairingService _pairing;
    private readonly Label _code = new() { AutoSize = true, Font = new Font("Segoe UI", 40, FontStyle.Bold) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    public PairingForm(PairingService pairing)
    {
        _pairing = pairing;
        Text = "Handy koppeln";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        TopMost = true;

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "Öffne RatioBackup auf dem Handy und gib diesen Code ein:", AutoSize = true });
        layout.Controls.Add(_code);
        layout.Controls.Add(new Label { Text = "Der Code gilt 5 Minuten. Handy und PC müssen im selben WLAN sein.", AutoSize = true });
        layout.Controls.Add(new Label
        {
            Text = "Fragt Windows nach der Firewall: bitte für private Netzwerke zulassen.",
            AutoSize = true,
            ForeColor = Color.DarkRed,
        });
        var renew = new Button { Text = "Neuer Code", AutoSize = true };
        renew.Click += (_, _) => _code.Text = _pairing.NewCode();
        layout.Controls.Add(renew);
        Controls.Add(layout);

        _code.Text = _pairing.NewCode();
        _timer.Tick += (_, _) => { if (_pairing.CurrentCode is null) _code.Text = _pairing.NewCode(); };
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
