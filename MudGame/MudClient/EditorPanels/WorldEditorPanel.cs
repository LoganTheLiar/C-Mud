using System.Drawing;
using System.Windows.Forms;
using MudShared.Models;
using MudShared.Protocol;

namespace MudClient.EditorPanels;

/// <summary>
/// GUI panel that lets admins create and edit the world (rooms, items, NPCs, exits).
/// All edits are sent over the network; the server is the source of truth.
/// </summary>
public class WorldEditorPanel : UserControl
{
    private readonly NetworkClient _net;
    private World _world = new();

    // Tabs
    private readonly TabControl _tabs = new();
    private readonly TabPage _tabRooms = new("Rooms");
    private readonly TabPage _tabItems = new("Items");
    private readonly TabPage _tabNpcs = new("NPCs");

    // Rooms tab controls
    private readonly ListBox _roomList = new();
    private readonly TextBox _roomName = new();
    private readonly TextBox _roomDescription = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _roomIdLabel = new() { Text = "(new)" };
    private readonly Label _startingBadge = new();
    private readonly DataGridView _exitsGrid = new();
    private readonly CheckedListBox _roomItemsList = new();
    private readonly CheckedListBox _roomNpcsList = new();

    // Items tab controls
    private readonly ListBox _itemList = new();
    private readonly TextBox _itemName = new();
    private readonly TextBox _itemDescription = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox _itemTakeable = new() { Text = "Takeable", Checked = true };
    private readonly Label _itemIdLabel = new() { Text = "(new)" };

    // NPC tab controls
    private readonly ListBox _npcList = new();
    private readonly TextBox _npcName = new();
    private readonly TextBox _npcDescription = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _npcGreeting = new();
    private readonly Label _npcIdLabel = new() { Text = "(new)" };

    public WorldEditorPanel(NetworkClient net)
    {
        _net = net;
        BackColor = Color.FromArgb(20, 22, 30);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9.5f);
        Build();
    }

    public void LoadWorld(World world)
    {
        _world = world;
        RefreshRoomList();
        RefreshItemList();
        RefreshNpcList();
        RefreshRoomChecklists();
        RefreshExitsGridTargets();
    }

    // ------------------- Build UI -------------------
    private void Build()
    {
        _tabs.Dock = DockStyle.Fill;
        _tabs.BackColor = Color.FromArgb(20, 22, 30);
        _tabs.TabPages.Add(_tabRooms);
        _tabs.TabPages.Add(_tabItems);
        _tabs.TabPages.Add(_tabNpcs);
        foreach (TabPage t in _tabs.TabPages) t.BackColor = Color.FromArgb(20, 22, 30);

        BuildRoomsTab();
        BuildItemsTab();
        BuildNpcsTab();

        Controls.Add(_tabs);
    }

    private static TableLayoutPanel TwoCol(int leftPx = 220)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, leftPx));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        t.BackColor = Color.FromArgb(20, 22, 30);
        return t;
    }

    private void BuildRoomsTab()
    {
        var layout = TwoCol(220);

        // Left: list + new/delete
        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.FromArgb(20, 22, 30) };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        _roomList.Dock = DockStyle.Fill;
        StyleList(_roomList);
        _roomList.SelectedIndexChanged += (_, _) => LoadSelectedRoom();

        var leftBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.FromArgb(20, 22, 30), Padding = new Padding(4) };
        leftBtns.Controls.Add(MkButton("New Room", (_, _) => NewRoom()));
        leftBtns.Controls.Add(MkButton("Delete", (_, _) => DeleteRoom(), danger: true));
        leftBtns.Controls.Add(MkButton("Set as Start", (_, _) => SetStartingRoom()));
        leftPanel.Controls.Add(_roomList, 0, 0);
        leftPanel.Controls.Add(leftBtns, 0, 1);

        // Right: form
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(8), BackColor = Color.FromArgb(20, 22, 30) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 7; i++) right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles[5] = new RowStyle(SizeType.Percent, 100f);

        _roomName.Dock = DockStyle.Fill;
        StyleTextbox(_roomName);
        _roomDescription.Dock = DockStyle.Fill;
        _roomDescription.Height = 90;
        StyleTextbox(_roomDescription);

        _startingBadge.AutoSize = true;
        _startingBadge.ForeColor = Color.Gold;

        var idRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _roomIdLabel.AutoSize = true; _roomIdLabel.ForeColor = Color.Silver;
        idRow.Controls.Add(_roomIdLabel);
        idRow.Controls.Add(_startingBadge);

        right.Controls.Add(Lbl("ID"), 0, 0); right.Controls.Add(idRow, 1, 0);
        right.Controls.Add(Lbl("Name"), 0, 1); right.Controls.Add(_roomName, 1, 1);
        right.Controls.Add(Lbl("Description"), 0, 2); right.Controls.Add(_roomDescription, 1, 2);

        // Exits grid
        _exitsGrid.Dock = DockStyle.Fill;
        _exitsGrid.Height = 140;
        _exitsGrid.AllowUserToAddRows = true;
        _exitsGrid.RowHeadersVisible = false;
        _exitsGrid.BackgroundColor = Color.FromArgb(14, 16, 22);
        _exitsGrid.DefaultCellStyle.BackColor = Color.FromArgb(14, 16, 22);
        _exitsGrid.DefaultCellStyle.ForeColor = Color.Gainsboro;
        _exitsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 70, 100);
        _exitsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 36, 48);
        _exitsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
        _exitsGrid.EnableHeadersVisualStyles = false;
        _exitsGrid.GridColor = Color.FromArgb(50, 54, 66);
        _exitsGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "dir",
            HeaderText = "Direction",
            Items = { "north", "south", "east", "west", "up", "down" },
            Width = 110,
            FlatStyle = FlatStyle.Flat
        });
        _exitsGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "target",
            HeaderText = "Target Room",
            FlatStyle = FlatStyle.Flat,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        right.Controls.Add(Lbl("Exits"), 0, 3); right.Controls.Add(_exitsGrid, 1, 3);

        // Items/NPCs placement
        var placePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(20, 22, 30) };
        placePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        placePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        StyleCheckedList(_roomItemsList);
        StyleCheckedList(_roomNpcsList);
        _roomItemsList.Dock = DockStyle.Fill;
        _roomNpcsList.Dock = DockStyle.Fill;
        var itemsBox = BoxedList("Items in this room", _roomItemsList);
        var npcsBox = BoxedList("NPCs in this room", _roomNpcsList);
        placePanel.Controls.Add(itemsBox, 0, 0);
        placePanel.Controls.Add(npcsBox, 1, 0);
        right.Controls.Add(Lbl("Place"), 0, 4); right.Controls.Add(placePanel, 1, 4);

        var saveRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        saveRow.Controls.Add(MkButton("Save Room", (_, _) => SaveRoom(), primary: true));
        right.Controls.Add(new Label(), 0, 6);
        right.Controls.Add(saveRow, 1, 6);

        layout.Controls.Add(leftPanel, 0, 0);
        layout.Controls.Add(right, 1, 0);
        _tabRooms.Controls.Add(layout);
    }

    private void BuildItemsTab()
    {
        var layout = TwoCol(220);
        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.FromArgb(20, 22, 30) };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _itemList.Dock = DockStyle.Fill;
        StyleList(_itemList);
        _itemList.SelectedIndexChanged += (_, _) => LoadSelectedItem();
        var leftBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4), BackColor = Color.FromArgb(20, 22, 30) };
        leftBtns.Controls.Add(MkButton("New Item", (_, _) => NewItem()));
        leftBtns.Controls.Add(MkButton("Delete", (_, _) => DeleteItem(), danger: true));
        leftPanel.Controls.Add(_itemList, 0, 0);
        leftPanel.Controls.Add(leftBtns, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8), BackColor = Color.FromArgb(20, 22, 30) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++) right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles[2] = new RowStyle(SizeType.Percent, 100f);

        _itemName.Dock = DockStyle.Fill; StyleTextbox(_itemName);
        _itemDescription.Dock = DockStyle.Fill; _itemDescription.Height = 160; StyleTextbox(_itemDescription);
        _itemIdLabel.ForeColor = Color.Silver;
        _itemTakeable.ForeColor = Color.Gainsboro;

        right.Controls.Add(Lbl("ID"), 0, 0); right.Controls.Add(_itemIdLabel, 1, 0);
        right.Controls.Add(Lbl("Name"), 0, 1); right.Controls.Add(_itemName, 1, 1);
        right.Controls.Add(Lbl("Description"), 0, 2); right.Controls.Add(_itemDescription, 1, 2);
        right.Controls.Add(new Label(), 0, 3); right.Controls.Add(_itemTakeable, 1, 3);
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        row.Controls.Add(MkButton("Save Item", (_, _) => SaveItem(), primary: true));
        right.Controls.Add(new Label(), 0, 4); right.Controls.Add(row, 1, 4);

        layout.Controls.Add(leftPanel, 0, 0);
        layout.Controls.Add(right, 1, 0);
        _tabItems.Controls.Add(layout);
    }

    private void BuildNpcsTab()
    {
        var layout = TwoCol(220);
        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.FromArgb(20, 22, 30) };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _npcList.Dock = DockStyle.Fill;
        StyleList(_npcList);
        _npcList.SelectedIndexChanged += (_, _) => LoadSelectedNpc();
        var leftBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4), BackColor = Color.FromArgb(20, 22, 30) };
        leftBtns.Controls.Add(MkButton("New NPC", (_, _) => NewNpc()));
        leftBtns.Controls.Add(MkButton("Delete", (_, _) => DeleteNpc(), danger: true));
        leftPanel.Controls.Add(_npcList, 0, 0);
        leftPanel.Controls.Add(leftBtns, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8), BackColor = Color.FromArgb(20, 22, 30) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++) right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles[2] = new RowStyle(SizeType.Percent, 100f);

        _npcName.Dock = DockStyle.Fill; StyleTextbox(_npcName);
        _npcDescription.Dock = DockStyle.Fill; _npcDescription.Height = 140; StyleTextbox(_npcDescription);
        _npcGreeting.Dock = DockStyle.Fill; StyleTextbox(_npcGreeting);
        _npcIdLabel.ForeColor = Color.Silver;

        right.Controls.Add(Lbl("ID"), 0, 0); right.Controls.Add(_npcIdLabel, 1, 0);
        right.Controls.Add(Lbl("Name"), 0, 1); right.Controls.Add(_npcName, 1, 1);
        right.Controls.Add(Lbl("Description"), 0, 2); right.Controls.Add(_npcDescription, 1, 2);
        right.Controls.Add(Lbl("Greeting"), 0, 3); right.Controls.Add(_npcGreeting, 1, 3);
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        row.Controls.Add(MkButton("Save NPC", (_, _) => SaveNpc(), primary: true));
        right.Controls.Add(new Label(), 0, 4); right.Controls.Add(row, 1, 4);

        layout.Controls.Add(leftPanel, 0, 0);
        layout.Controls.Add(right, 1, 0);
        _tabNpcs.Controls.Add(layout);
    }

    // ------------------- styling helpers -------------------
    private static Label Lbl(string t) => new()
    {
        Text = t,
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.Silver,
        Padding = new Padding(2, 6, 0, 0),
        Height = 24
    };

    private static Button MkButton(string text, EventHandler click, bool primary = false, bool danger = false)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(4),
            BackColor = danger ? Color.FromArgb(140, 55, 65)
                        : primary ? Color.FromArgb(60, 120, 90)
                        : Color.FromArgb(60, 65, 80)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private static void StyleList(ListBox l)
    {
        l.BackColor = Color.FromArgb(14, 16, 22);
        l.ForeColor = Color.Gainsboro;
        l.BorderStyle = BorderStyle.None;
        l.IntegralHeight = false;
    }
    private static void StyleCheckedList(CheckedListBox l)
    {
        l.BackColor = Color.FromArgb(14, 16, 22);
        l.ForeColor = Color.Gainsboro;
        l.BorderStyle = BorderStyle.None;
        l.CheckOnClick = true;
        l.IntegralHeight = false;
    }
    private static void StyleTextbox(TextBoxBase t)
    {
        t.BackColor = Color.FromArgb(14, 16, 22);
        t.ForeColor = Color.Gainsboro;
        t.BorderStyle = BorderStyle.FixedSingle;
    }

    private GroupBox BoxedList(string title, Control inner)
    {
        var g = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = Color.Silver,
            Padding = new Padding(6)
        };
        inner.Dock = DockStyle.Fill;
        g.Controls.Add(inner);
        return g;
    }

    // ------------------- Rooms logic -------------------
    private Room? _editingRoom;

    private void RefreshRoomList()
    {
        var selectedId = _editingRoom?.Id;
        _roomList.BeginUpdate();
        _roomList.Items.Clear();
        foreach (var r in _world.Rooms.Values.OrderBy(r => r.Name))
            _roomList.Items.Add(new RoomRow(r));
        _roomList.EndUpdate();
        if (selectedId != null)
        {
            for (int i = 0; i < _roomList.Items.Count; i++)
                if (((RoomRow)_roomList.Items[i]!).Room.Id == selectedId) { _roomList.SelectedIndex = i; break; }
        }
    }

    private void RefreshExitsGridTargets()
    {
        if (_exitsGrid.Columns["target"] is DataGridViewComboBoxColumn col)
        {
            col.Items.Clear();
            foreach (var r in _world.Rooms.Values.OrderBy(r => r.Name))
                col.Items.Add(new RoomRow(r));
            col.DisplayMember = "Display";
            col.ValueMember = "Id";
        }
    }

    private void RefreshRoomChecklists()
    {
        _roomItemsList.Items.Clear();
        foreach (var it in _world.Items.Values.OrderBy(i => i.Name))
            _roomItemsList.Items.Add(new ItemRow(it), _editingRoom?.ItemIds.Contains(it.Id) == true);
        _roomNpcsList.Items.Clear();
        foreach (var n in _world.Npcs.Values.OrderBy(x => x.Name))
            _roomNpcsList.Items.Add(new NpcRow(n), _editingRoom?.NpcIds.Contains(n.Id) == true);

        _roomItemsList.ItemCheck -= OnRoomItemToggle;
        _roomNpcsList.ItemCheck -= OnRoomNpcToggle;
        _roomItemsList.ItemCheck += OnRoomItemToggle;
        _roomNpcsList.ItemCheck += OnRoomNpcToggle;
    }

    private void OnRoomItemToggle(object? s, ItemCheckEventArgs e)
    {
        if (_editingRoom == null || string.IsNullOrEmpty(_editingRoom.Id)) return;
        var row = (ItemRow)_roomItemsList.Items[e.Index]!;
        var type = e.NewValue == CheckState.Checked ? MessageType.PlaceItemInRoom : MessageType.RemoveItemFromRoom;
        _net.Send(type, new { roomId = _editingRoom.Id, entityId = row.Item.Id });
    }
    private void OnRoomNpcToggle(object? s, ItemCheckEventArgs e)
    {
        if (_editingRoom == null || string.IsNullOrEmpty(_editingRoom.Id)) return;
        var row = (NpcRow)_roomNpcsList.Items[e.Index]!;
        var type = e.NewValue == CheckState.Checked ? MessageType.PlaceNpcInRoom : MessageType.RemoveNpcFromRoom;
        _net.Send(type, new { roomId = _editingRoom.Id, entityId = row.Npc.Id });
    }

    private void LoadSelectedRoom()
    {
        if (_roomList.SelectedItem is not RoomRow row) return;
        _editingRoom = row.Room;
        _roomIdLabel.Text = row.Room.Id;
        _startingBadge.Text = row.Room.Id == _world.StartingRoomId ? "   [STARTING ROOM]" : "";
        _roomName.Text = row.Room.Name;
        _roomDescription.Text = row.Room.Description;
        _exitsGrid.Rows.Clear();
        foreach (var kv in row.Room.Exits)
            _exitsGrid.Rows.Add(kv.Key, kv.Value);
        RefreshRoomChecklists();
    }

    private void NewRoom()
    {
        _editingRoom = new Room { Name = "New Room", Description = "" };
        _roomIdLabel.Text = "(new)";
        _startingBadge.Text = "";
        _roomName.Text = _editingRoom.Name;
        _roomDescription.Text = "";
        _exitsGrid.Rows.Clear();
        RefreshRoomChecklists();
        _roomList.ClearSelected();
    }

    private void SaveRoom()
    {
        if (_editingRoom == null) _editingRoom = new Room();
        _editingRoom.Name = string.IsNullOrWhiteSpace(_roomName.Text) ? "Unnamed" : _roomName.Text.Trim();
        _editingRoom.Description = _roomDescription.Text;
        _editingRoom.Exits = new();
        foreach (DataGridViewRow row in _exitsGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var dir = row.Cells["dir"].Value?.ToString();
            var target = row.Cells["target"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(target)) continue;
            _editingRoom.Exits[dir!] = target!;
        }
        _net.Send(MessageType.UpsertRoom, _editingRoom);
    }

    private void DeleteRoom()
    {
        if (_editingRoom == null || string.IsNullOrEmpty(_editingRoom.Id)) return;
        if (MessageBox.Show(this, $"Delete room '{_editingRoom.Name}'?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        _net.Send(MessageType.DeleteRoom, new { id = _editingRoom.Id });
        _editingRoom = null;
    }

    private void SetStartingRoom()
    {
        if (_editingRoom == null || string.IsNullOrEmpty(_editingRoom.Id)) return;
        _net.Send(MessageType.SetStartingRoom, new { id = _editingRoom.Id });
    }

    // ------------------- Items logic -------------------
    private Item? _editingItem;

    private void RefreshItemList()
    {
        _itemList.BeginUpdate();
        _itemList.Items.Clear();
        foreach (var it in _world.Items.Values.OrderBy(i => i.Name))
            _itemList.Items.Add(new ItemRow(it));
        _itemList.EndUpdate();
    }

    private void LoadSelectedItem()
    {
        if (_itemList.SelectedItem is not ItemRow row) return;
        _editingItem = row.Item;
        _itemIdLabel.Text = row.Item.Id;
        _itemName.Text = row.Item.Name;
        _itemDescription.Text = row.Item.Description;
        _itemTakeable.Checked = row.Item.Takeable;
    }

    private void NewItem()
    {
        _editingItem = new Item { Name = "New Item" };
        _itemIdLabel.Text = "(new)";
        _itemName.Text = _editingItem.Name;
        _itemDescription.Text = "";
        _itemTakeable.Checked = true;
        _itemList.ClearSelected();
    }

    private void SaveItem()
    {
        _editingItem ??= new Item();
        _editingItem.Name = string.IsNullOrWhiteSpace(_itemName.Text) ? "unnamed item" : _itemName.Text.Trim();
        _editingItem.Description = _itemDescription.Text;
        _editingItem.Takeable = _itemTakeable.Checked;
        _net.Send(MessageType.UpsertItem, _editingItem);
    }

    private void DeleteItem()
    {
        if (_editingItem == null || string.IsNullOrEmpty(_editingItem.Id)) return;
        if (MessageBox.Show(this, $"Delete item '{_editingItem.Name}'?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        _net.Send(MessageType.DeleteItem, new { id = _editingItem.Id });
        _editingItem = null;
    }

    // ------------------- NPCs logic -------------------
    private Npc? _editingNpc;

    private void RefreshNpcList()
    {
        _npcList.BeginUpdate();
        _npcList.Items.Clear();
        foreach (var n in _world.Npcs.Values.OrderBy(x => x.Name))
            _npcList.Items.Add(new NpcRow(n));
        _npcList.EndUpdate();
    }

    private void LoadSelectedNpc()
    {
        if (_npcList.SelectedItem is not NpcRow row) return;
        _editingNpc = row.Npc;
        _npcIdLabel.Text = row.Npc.Id;
        _npcName.Text = row.Npc.Name;
        _npcDescription.Text = row.Npc.Description;
        _npcGreeting.Text = row.Npc.Greeting;
    }

    private void NewNpc()
    {
        _editingNpc = new Npc { Name = "New NPC" };
        _npcIdLabel.Text = "(new)";
        _npcName.Text = _editingNpc.Name;
        _npcDescription.Text = "";
        _npcGreeting.Text = "";
        _npcList.ClearSelected();
    }

    private void SaveNpc()
    {
        _editingNpc ??= new Npc();
        _editingNpc.Name = string.IsNullOrWhiteSpace(_npcName.Text) ? "unnamed npc" : _npcName.Text.Trim();
        _editingNpc.Description = _npcDescription.Text;
        _editingNpc.Greeting = _npcGreeting.Text;
        _net.Send(MessageType.UpsertNpc, _editingNpc);
    }

    private void DeleteNpc()
    {
        if (_editingNpc == null || string.IsNullOrEmpty(_editingNpc.Id)) return;
        if (MessageBox.Show(this, $"Delete NPC '{_editingNpc.Name}'?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        _net.Send(MessageType.DeleteNpc, new { id = _editingNpc.Id });
        _editingNpc = null;
    }

    // ------------------- row wrappers (ToString for ListBox display) -------------------
    private class RoomRow
    {
        public Room Room { get; }
        public RoomRow(Room r) { Room = r; }
        public string Id => Room.Id;
        public string Display => $"{Room.Name}  [{Room.Id}]";
        public override string ToString() => Display;
    }
    private class ItemRow
    {
        public Item Item { get; }
        public ItemRow(Item i) { Item = i; }
        public override string ToString() => $"{Item.Name}  [{Item.Id}]";
    }
    private class NpcRow
    {
        public Npc Npc { get; }
        public NpcRow(Npc n) { Npc = n; }
        public override string ToString() => $"{Npc.Name}  [{Npc.Id}]";
    }
}
