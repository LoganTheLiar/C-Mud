using System.Drawing;
using System.Windows.Forms;

namespace MudClient;

public class ConnectDialog : Form
{
    private readonly TextBox _host = new() { Text = "127.0.0.1" };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Value = 4000 };
    private readonly TextBox _name = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly RadioButton _rbLogin = new() { Text = "Login", Checked = true };
    private readonly RadioButton _rbRegister = new() { Text = "Register new" };

    public new string Name => _name.Text.Trim();
    public string Host => _host.Text.Trim();
    public int Port => (int)_port.Value;
    public string Password => _password.Text;
    public bool IsRegister => _rbRegister.Checked;

    public ConnectDialog()
    {
        Text = "Connect to MUD";
        ClientSize = new Size(360, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(24, 26, 34);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9.5f);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(14),
            BackColor = Color.FromArgb(24, 26, 34)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label Lbl(string t) => new() { Text = t, Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.Gainsboro, Padding = new Padding(0, 6, 0, 0) };
        _host.Dock = DockStyle.Fill;
        _port.Dock = DockStyle.Fill;
        _name.Dock = DockStyle.Fill;
        _password.Dock = DockStyle.Fill;

        layout.Controls.Add(Lbl("Host"), 0, 0); layout.Controls.Add(_host, 1, 0);
        layout.Controls.Add(Lbl("Port"), 0, 1); layout.Controls.Add(_port, 1, 1);
        layout.Controls.Add(Lbl("Name"), 0, 2); layout.Controls.Add(_name, 1, 2);
        layout.Controls.Add(Lbl("Password"), 0, 3); layout.Controls.Add(_password, 1, 3);

        var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _rbLogin.AutoSize = true; _rbLogin.ForeColor = Color.Gainsboro;
        _rbRegister.AutoSize = true; _rbRegister.ForeColor = Color.Gainsboro;
        modePanel.Controls.Add(_rbLogin);
        modePanel.Controls.Add(_rbRegister);
        layout.Controls.Add(Lbl("Mode"), 0, 4); layout.Controls.Add(modePanel, 1, 4);

        var ok = new Button { Text = "Connect", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(60, 120, 90), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 110 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(70, 70, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 110 };
        var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        btns.Controls.Add(ok);
        btns.Controls.Add(cancel);
        layout.Controls.Add(new Label(), 0, 5);
        layout.Controls.Add(btns, 1, 5);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);
    }
}
