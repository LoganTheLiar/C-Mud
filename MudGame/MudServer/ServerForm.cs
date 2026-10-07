using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MudServer;

public class ServerForm : Form
{
    private readonly GameServer _server;
    private readonly ListBox _logBox = new();
    private readonly ListBox _sessionsBox = new();
    private readonly Label _statusLabel = new();

    public ServerForm(GameServer server)
    {
        _server = server;
        BuildUi();
        _server.LogMessage += OnLog;
        _server.SessionsChanged += OnSessionsChanged;
        OnLog($"[{DateTime.Now:HH:mm:ss}] MUD Server GUI ready.");
        UpdateStatus();
    }

    private void BuildUi()
    {
        Text = "MUD Server";
        ClientSize = new Size(820, 500);
        BackColor = Color.FromArgb(20, 22, 30);
        ForeColor = Color.Gainsboro;
        Font = new Font("Consolas", 10f);

        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Height = 36;
        _statusLabel.BackColor = Color.FromArgb(32, 36, 48);
        _statusLabel.ForeColor = Color.LightGreen;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Padding = new Padding(10, 0, 0, 0);
        _statusLabel.Font = new Font("Consolas", 11f, FontStyle.Bold);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 500,
            BackColor = Color.FromArgb(20, 22, 30),
            Panel1MinSize = 300,
            Panel2MinSize = 180
        };

        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = Color.FromArgb(14, 16, 22);
        _logBox.ForeColor = Color.Gainsboro;
        _logBox.BorderStyle = BorderStyle.None;
        _logBox.IntegralHeight = false;

        _sessionsBox.Dock = DockStyle.Fill;
        _sessionsBox.BackColor = Color.FromArgb(14, 16, 22);
        _sessionsBox.ForeColor = Color.LightSkyBlue;
        _sessionsBox.BorderStyle = BorderStyle.None;
        _sessionsBox.IntegralHeight = false;

        var logLabel = new Label { Dock = DockStyle.Top, Height = 22, Text = "  Server Log", ForeColor = Color.Silver, BackColor = Color.FromArgb(32, 36, 48) };
        var sessLabel = new Label { Dock = DockStyle.Top, Height = 22, Text = "  Connected Sessions", ForeColor = Color.Silver, BackColor = Color.FromArgb(32, 36, 48) };

        split.Panel1.Controls.Add(_logBox);
        split.Panel1.Controls.Add(logLabel);
        split.Panel2.Controls.Add(_sessionsBox);
        split.Panel2.Controls.Add(sessLabel);

        Controls.Add(split);
        Controls.Add(_statusLabel);
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = $"  Listening on port {_server.Port}  |  Sessions: {_server.Sessions.Count}  |  Rooms: {_server.World.Rooms.Count}  |  Players: {_server.World.Players.Count}";
    }

    private void OnLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(() => OnLog(line)); } catch { } return; }
        _logBox.Items.Add(line);
        if (_logBox.Items.Count > 500) _logBox.Items.RemoveAt(0);
        _logBox.TopIndex = _logBox.Items.Count - 1;
        UpdateStatus();
    }

    private void OnSessionsChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(OnSessionsChanged); } catch { } return; }
        _sessionsBox.Items.Clear();
        foreach (var s in _server.Sessions.Values)
            _sessionsBox.Items.Add($"{s.Id}  {s.PlayerName ?? "(unauth)"}" + (s.IsAdmin ? "  [admin]" : ""));
        UpdateStatus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _server.LogMessage -= OnLog;
        _server.SessionsChanged -= OnSessionsChanged;
        base.OnFormClosing(e);
    }
}
