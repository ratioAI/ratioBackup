using System;
using System.Windows;
using System.Windows.Threading;
using RatioBackup.Core;

namespace RatioBackup.Tray;

public partial class PairingWindow : Window
{
    private readonly PairingService _pairing;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    internal PairingWindow(PairingService pairing, string verifyCode)
    {
        InitializeComponent();
        Theme.ApplyTitleBar(this);
        _pairing = pairing;
        VerifyText.Text = $"Prüfcode {verifyCode}";
        CodeText.Text = Format(_pairing.NewCode());
        _timer.Tick += (_, _) => { if (_pairing.CurrentCode is null) CodeText.Text = Format(_pairing.NewCode()); };
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private static string Format(string code) => code.Length == 6 ? $"{code[..3]} {code[3..]}" : code;

    private void Renew_Click(object sender, RoutedEventArgs e) => CodeText.Text = Format(_pairing.NewCode());

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
