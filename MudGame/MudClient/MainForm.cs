using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using MudClient.EditorPanels;
using MudShared.Models;
using MudShared.Protocol;

namespace MudClient;

public class MainForm : Form
{
    private readonly NetworkClient _net = new();
    private readonly RichTextBox _output = new();
    private readonly TextBox _input = new();
    private readonly ToolStripStatusLabel _statusLabel = new("Disconnected");
    private readonly TabControl _tabs = new();
    private readonly TabPage _gameTab = new("Game");
    private readonly TabPage _editorTab = new("World Editor");
    private readonly WorldEditorPanel _editorPanel;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public bool IsAdmin { get; private set; }
    public string? MyName { get; private set; }
    public World? WorldSnapshot { get; private set; }

    public MainForm()
    {
        _editorPanel = new WorldEditorPanel(_net);
        BuildUi();
        _net.MessageReceived += OnMessage;
        _net.ConnectionError += err => UiAppend($"[net error] {err}", Color.OrangeRed);
        _net.Disconnected += () =>
        {
            UiAppend("[disconnected]", Color.OrangeRed);
            UiSetStatus("Disconnected");
            SafeInvoke(() => _editorTab.Enabled = false);
        };
        Shown += (_, _) => ShowConnectDialog();
    }

    private void BuildUi()
    {
        Text = "MUD Client";
        ClientSize = new Size(1000, 650);
        BackColor = Color.FromArgb(16, 18, 24);
        ForeColor = Color.Gainsboro;
        Font = new Font("Consolas", 10f);
        MinimumSize = new Size(720, 400);

        var menu = new MenuStrip { BackColor = Color.FromArgb(32, 36, 48), ForeColor = Color.Gainsboro };
        var fileMenu = new ToolStripMenuItem("&Connection");
        var connectItem = new ToolStripMenuItem("Connect to server...", null, (_, _) => ShowConnectDialog());
        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => Close());
        fileMenu.DropDownItems.AddRange(new ToolStripItem[] { connectItem, new ToolStripSeparator(), quitItem });
        menu.Items.Add(fileMenu);
        MainMenuStrip = menu;

        var status = new StatusStrip { BackColor = Color.FromArgb(32, 36, 48), ForeColor = Color.Gainsboro };
        status.Items.Add(_statusLabel);

        _tabs.Dock = DockStyle.Fill;
        _tabs.BackColor = Color.FromArgb(20, 22, 30);

        // Game tab layout
        var gameLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(16, 18, 24)
        };
        gameLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        gameLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));

        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.BackColor = Color.FromArgb(10, 12, 16);
        _output.ForeColor = Color.Gainsboro;
        _output.BorderStyle = BorderStyle.None;
        _output.Font = new Font("Consolas", 11f);
        _output.DetectUrls = false;
        _output.HideSelection = false;

        _input.Dock = DockStyle.Fill;
        _input.BackColor = Color.FromArgb(10, 12, 16);
        _input.ForeColor = Color.LightGreen;
        _input.BorderStyle = BorderStyle.FixedSingle;
        _input.Font = new Font("Consolas", 12f);
        _input.KeyDown += InputKeyDown;

        var inputPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 4), BackColor = Color.FromArgb(16, 18, 24) };
        var prompt = new Label { Text = ">", Dock = DockStyle.Left, Width = 22, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGreen, BackColor = Color.FromArgb(16, 18, 24), Font = new Font("Consolas", 12f, FontStyle.Bold) };
        inputPanel.Controls.Add(_input);
        inputPanel.Controls.Add(prompt);

        gameLayout.Controls.Add(_output, 0, 0);
        gameLayout.Controls.Add(inputPanel, 0, 1);
        _gameTab.Controls.Add(gameLayout);
        _gameTab.BackColor = Color.FromArgb(16, 18, 24);

        _editorTab.Controls.Add(_editorPanel);
        _editorTab.BackColor = Color.FromArgb(20, 22, 30);
        _editorPanel.Dock = DockStyle.Fill;
        _editorTab.Enabled = false;

        _tabs.TabPages.Add(_gameTab);
        _tabs.TabPages.Add(_editorTab);
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedTab == _editorTab && _net.IsConnected && IsAdmin)
                _net.Send(MessageType.RequestWorld, null);
        };

        Controls.Add(_tabs);
        Controls.Add(menu);
        Controls.Add(status);
    }

    private void InputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            var text = _input.Text.Trim();
            _input.Clear();
            if (string.IsNullOrEmpty(text)) return;
            if (!_net.IsConnected) { UiAppend("Not connected.", Color.OrangeRed); return; }
            _history.Add(text);
            _historyIndex = _history.Count;
            if (!string.IsNullOrEmpty(MyName))
            {
                UiAppend($"> {text}", Color.DarkGray);
                _net.Send(MessageType.Command, new { text });
            }
            else
            {
                // Not logged in — allow slash commands here as fallback
                UiAppend("Please use the login dialog to connect.", Color.Khaki);
            }
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (_history.Count == 0) return;
            e.SuppressKeyPress = true;
            _historyIndex = Math.Max(0, _historyIndex - 1);
            if (_historyIndex < _history.Count) _input.Text = _history[_historyIndex];
            _input.SelectionStart = _input.Text.Length;
        }
        else if (e.KeyCode == Keys.Down)
        {
            if (_history.Count == 0) return;
            e.SuppressKeyPress = true;
            _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
            _input.Text = _historyIndex < _history.Count ? _history[_historyIndex] : "";
            _input.SelectionStart = _input.Text.Length;
        }
    }

    private void ShowConnectDialog()
    {
        using var dlg = new ConnectDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (_net.IsConnected) _net.Disconnect();
        _ = ConnectAsync(dlg.Host, dlg.Port, dlg.Name, dlg.Password, dlg.IsRegister);
    }

    private async Task ConnectAsync(string host, int port, string name, string password, bool register)
    {
        UiAppend($"[connecting to {host}:{port}...]", Color.Khaki);
        var ok = await _net.ConnectAsync(host, port);
        if (!ok) { UiAppend("Connection failed.", Color.OrangeRed); return; }
        UiSetStatus($"Connected to {host}:{port}");
        _net.Send(register ? MessageType.Register : MessageType.Login, new { name, password });
    }

    // ---------- message routing ----------
    private void OnMessage(NetMessage msg)
    {
        switch (msg.Type)
        {
            case MessageType.Text:
                var line = msg.GetPayload<TextLine>();
                if (line != null) UiAppend(line.Line, Color.Gainsboro);
                break;
            case MessageType.LoginOk:
                var ok = msg.GetPayload<LoginOkPayload>();
                MyName = ok?.Name;
                IsAdmin = ok?.IsAdmin ?? false;
                UiAppend($"*** Logged in as {MyName}{(IsAdmin ? " [admin]" : "")} ***", Color.LightGreen);
                UiSetStatus($"Logged in as {MyName}{(IsAdmin ? " [admin]" : "")}");
                SafeInvoke(() => { _editorTab.Enabled = IsAdmin; _input.Focus(); });
                if (IsAdmin) _net.Send(MessageType.RequestWorld, null);
                break;
            case MessageType.LoginFail:
                var fail = msg.GetPayload<LoginFailPayload>();
                UiAppend("Login failed: " + (fail?.Reason ?? ""), Color.OrangeRed);
                break;
            case MessageType.RoomView:
                var rv = msg.GetPayload<RoomViewPayload>();
                if (rv != null) RenderRoom(rv);
                break;
            case MessageType.WorldSnapshot:
                var world = msg.GetPayload<World>();
                if (world != null)
                {
                    WorldSnapshot = world;
                    SafeInvoke(() => _editorPanel.LoadWorld(world));
                }
                break;
            case MessageType.EditorAck:
                var ack = msg.GetPayload<EditorAckPayload>();
                if (ack != null)
                {
                    UiAppend($"[editor] {ack.Message}", ack.Ok ? Color.LightSkyBlue : Color.OrangeRed);
                    if (ack.Ok) _net.Send(MessageType.RequestWorld, null);
                }
                break;
        }
    }

    private void RenderRoom(RoomViewPayload r)
    {
        SafeInvoke(() =>
        {
            UiAppendColored($"\n{r.Name}\n", Color.Gold, bold: true);
            UiAppend(r.Description, Color.Gainsboro);
            if (r.Exits?.Count > 0) UiAppendColored("Exits: " + string.Join(", ", r.Exits), Color.LightSkyBlue);
            else UiAppendColored("Exits: none", Color.LightSkyBlue);
            if (r.Items?.Count > 0) UiAppendColored("You see: " + string.Join(", ", r.Items), Color.PaleGreen);
            if (r.Npcs?.Count > 0) UiAppendColored("Here: " + string.Join(", ", r.Npcs), Color.Plum);
            if (r.Players?.Count > 0) UiAppendColored("Also here: " + string.Join(", ", r.Players), Color.LightPink);
        });
    }

    // ---------- UI helpers ----------
    private void SafeInvoke(Action a)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(a); } catch { } }
        else a();
    }

    private void UiAppend(string line, Color color)
    {
        SafeInvoke(() => UiAppendColored(line, color));
    }

    private void UiAppendColored(string line, Color color, bool bold = false)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = color;
        if (bold) _output.SelectionFont = new Font(_output.Font, FontStyle.Bold);
        _output.AppendText(line + Environment.NewLine);
        if (bold) _output.SelectionFont = _output.Font;
        _output.SelectionColor = _output.ForeColor;
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }

    private void UiSetStatus(string s) => SafeInvoke(() => _statusLabel.Text = s);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _net.Disconnect();
        base.OnFormClosed(e);
    }

    // ---------- payload records ----------
    private record TextLine(string Line);
    private record LoginOkPayload(string Name, bool IsAdmin);
    private record LoginFailPayload(string Reason);
    private record EditorAckPayload(bool Ok, string Message);
    private record RoomViewPayload(string Id, string Name, string Description,
        List<string> Exits, List<string> Items, List<string> Npcs, List<string> Players);
}
