using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MudShared.Models;
using MudShared.Protocol;

namespace MudClient;

public class NetworkClient
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _client?.Connected == true;

    public event Action<NetMessage>? MessageReceived;
    public event Action<string>? ConnectionError;
    public event Action? Disconnected;

    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            _cts = new CancellationTokenSource();
            _ = Task.Run(ReadLoop);
            return true;
        }
        catch (Exception ex)
        {
            ConnectionError?.Invoke(ex.Message);
            return false;
        }
    }

    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }
        try { _client?.Close(); } catch { }
        _client = null;
    }

    public void Send(string type, object? payload)
    {
        if (_writer == null) return;
        try { _writer.WriteLine(NetMessage.Serialize(type, payload)); }
        catch (Exception ex) { ConnectionError?.Invoke(ex.Message); }
    }

    private async Task ReadLoop()
    {
        try
        {
            while (_cts != null && !_cts.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync(_cts.Token);
                if (line == null) break;
                var msg = NetMessage.Parse(line);
                if (msg != null) MessageReceived?.Invoke(msg);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ConnectionError?.Invoke(ex.Message); }
        finally { Disconnected?.Invoke(); }
    }
}
