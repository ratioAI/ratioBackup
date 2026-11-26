using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RatioBackup.Core;

/// <summary>Beantwortet den UDP-Broadcast "RATIOBACKUP?" des Handys mit Name, Port und Fingerabdruck.</summary>
public sealed class DiscoveryResponder : IDisposable
{
    public const int DefaultPort = 47320;
    public const string Probe = "RATIOBACKUP?";

    private readonly byte[] _reply;
    private readonly CancellationTokenSource _cts = new();
    private UdpClient? _udp;

    public int Port { get; private set; }

    public DiscoveryResponder(string name, int apiPort, string fingerprint)
    {
        _reply = JsonSerializer.SerializeToUtf8Bytes(new { name, port = apiPort, fingerprint });
    }

    public void Start(int udpPort = DefaultPort)
    {
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, udpPort));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        _ = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var msg = await _udp!.ReceiveAsync(_cts.Token);
                if (Encoding.ASCII.GetString(msg.Buffer) == Probe)
                    await _udp.SendAsync(_reply, msg.RemoteEndPoint, _cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { /* einzelnes Paket fehlgeschlagen – weiter lauschen */ }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udp?.Dispose();
    }
}
