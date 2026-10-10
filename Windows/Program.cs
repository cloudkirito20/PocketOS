using System.Diagnostics;
using System.Net;
using System.Text;
using System.Drawing.Drawing2D;
using System.IO.Ports;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PocketOSManager.UI;

namespace PocketOSManager;

internal static class Program
{
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainWindow()); }
}

public sealed class Settings
{
    public string EspAddress { get; set; } = "http://192.168.4.1";
    public string BridgeExe { get; set; } = "";
    public string ComPort { get; set; } = "";
    public bool PreferUsb { get; set; } = true;
    public string PythonExe { get; set; } = "python";
    public bool ShinyHunt { get; set; } = false;
    public bool TrayMode { get; set; } = false;
    public string PkhexExe { get; set; } = "";
    public string PokeradarExe { get; set; } = "";
    public string SaveFolder { get; set; } = "";
    public bool AutoCheckUpdates { get; set; } = true;
    public DateTime LastGitHubCheckUtc { get; set; } = DateTime.MinValue;
}

public sealed class MainWindow : Form
{
    private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketOS", "manager.json");
    private readonly Settings cfg;
    private readonly Label status = new() { AutoSize = true, Text = "Not connected" };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly NotifyIcon tray;
    private Process? bridge;
    private HttpListener? bridgeListener;
    private readonly CancellationTokenSource bridgeStop = new();
    private const string LocalBridgeUrl = "http://127.0.0.1:18765/";
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private string selectedBin = "";
    private bool updating;
    private readonly System.Windows.Forms.Timer githubTimer = new() { Interval = 3600000 };
    private PocketOSManager.Updates.UpdateManifest? pendingManifest;
    private readonly Panel screenPreview = new() { Dock = DockStyle.Top, Height = 280, BackColor = Color.FromArgb(5, 9, 7) };
    private string previewMode = "MENU";
    private int previewSelection = 0;
    private byte[]? liveFrame;
    private bool busySync;
    private int pendingCommands;
    private DateTime lastNavigationUtc = DateTime.MinValue;
    private readonly SemaphoreSlim serialGate = new(1, 1);
    private SerialPort? activeSerial;
    private string activeSerialName = "";
    private DateTime serialRetryAfter = DateTime.MinValue;
    private string lastSerialIssue = "";
    private readonly System.Windows.Forms.Timer mirrorTimer = new() { Interval = 2500 };
    private readonly string[] pocketApps = { "POKEMON PARTNER", "POKEMON INFO", "WILD ENCOUNTER", "SHINY HUNT", "CLOCK", "ABOUT", "WI-FI TOOLS", "PC MONITOR", "SETTINGS" };
    private static readonly Color Ink = Color.FromArgb(218, 248, 225);
    private static readonly Color Surface = Color.FromArgb(14, 23, 19);
    private static readonly Color Frame = Color.FromArgb(77, 105, 84);

    public MainWindow()
    {
        cfg = LoadSettings();
        StartLocalBridgeListener();
        Shown += (_, _) => BeginWindowIntro();
        githubTimer.Tick += async (_, _) => await CheckGithubUpdatesAsync(false);
        githubTimer.Start();
        Shown += async (_, _) => { await Task.Delay(6500); if (!IsDisposed) await CheckGithubUpdatesAsync(false); };
        mirrorTimer.Tick += async (_, _) => await SyncDeviceState();
        mirrorTimer.Start();
        Text = "PocketOS Manager / Monochrome Beta"; Width = 1150; Height = 740;
        MinimumSize = new Size(950, 620); BackColor = Color.FromArgb(7, 13, 10);
        ForeColor = Ink; Font = new Font("Consolas", 9, FontStyle.Regular);
        StartPosition = FormStartPosition.CenterScreen;
        tray = new NotifyIcon { Text = "PocketOS Manager", Icon = SystemIcons.Application, Visible = true };
        tray.DoubleClick += (_, _) => Restore();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Manager", null, (_, _) => Restore());
        menu.Items.Add("Exit", null, (_, _) => { cfg.TrayMode = false; Close(); });
        tray.ContextMenuStrip = menu;

        KeyPreview = true;
        Text = "LazyTech / PocketOS";
        Width = 1050; Height = 780; MinimumSize = new Size(840, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(18, 21, 23);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22, 16, 22, 16),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 4, 0, 12), Margin = new Padding(0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        header.Controls.Add(new Label
        {
            Text = "LT  LAZYTECH / POCKETOS",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Padding = new Padding(10, 0, 0, 0),
            Font = new Font("Consolas", 16, FontStyle.Bold),
            Margin = new Padding(0)
        }, 0, 0);
        var settingsButton = Button("⚙  SETTINGS", () => { ShowSettings(); return Task.CompletedTask; });
        settingsButton.Dock = DockStyle.Fill; settingsButton.Margin = new Padding(12, 10, 4, 10);
        header.Controls.Add(settingsButton, 1, 0);
        root.Controls.Add(header, 0, 0);

        var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), Margin = new Padding(0), BackColor = Color.FromArgb(39, 45, 45) };
        var screenTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        screenTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        screenTop.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        screenTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        screenTop.Controls.Add(new Label
        {
            Text = "ESP32 OLED  /  128 x 64",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 11, FontStyle.Bold)
        }, 0, 0);
        screenPreview.Dock = DockStyle.Fill;
        screenPreview.BackColor = Color.Black;
        screenPreview.Paint += PaintPreview;
        screenPreview.Resize += (_, _) => screenPreview.Invalidate();
        screenTop.Controls.Add(screenPreview, 0, 1);
        status.Text = "DISCONNECTED  •  Connect in Settings";
        status.Dock = DockStyle.Fill; status.AutoSize = false;
        status.TextAlign = ContentAlignment.MiddleCenter;
        screenTop.Controls.Add(status, 0, 2);
        middle.Controls.Add(screenTop);
        root.Controls.Add(middle, 0, 1);

        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = BackColor };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        var pad = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Margin = new Padding(25, 10, 0, 6) };
        for (int i = 0; i < 3; i++) { pad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3)); pad.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3)); }
        void PadButton(string title, string action, int x, int y)
        {
            var button = Button(title, () => SendCommand(action));
            button.Dock = DockStyle.Fill; button.Margin = new Padding(1); button.Font = new Font("Consolas", 17, FontStyle.Bold);
            pad.Controls.Add(button, x, y);
        }
        PadButton("▲", "up", 1, 0); PadButton("◀", "left", 0, 1);
        PadButton("▶", "right", 2, 1); PadButton("▼", "down", 1, 2);
        pad.Controls.Add(new Label
        {
            Text = "+",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Consolas", 17, FontStyle.Bold)
        }, 1, 1);
        controls.Controls.Add(pad, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 45, 0, 0)
        };
        void ActionButton(string letter, string action)
        {
            var b = Button(letter, () => SendCommand(action));
            b.Width = 78; b.Height = 78; b.AutoSize = false;
            b.Font = new Font("Consolas", 24, FontStyle.Bold);
            b.Margin = new Padding(10, 0, 10, 0);
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.BorderColor = Color.WhiteSmoke; b.BackColor = Color.FromArgb(44, 47, 50);
            actions.Controls.Add(b);
        }
        ActionButton("B", "back"); ActionButton("A", "select");
        controls.Controls.Add(actions, 2, 0);
        root.Controls.Add(controls, 0, 2);
        Controls.Add(root);
        ApplyMonochrome(root);
        // Always render only device pixels; a disconnected preview must not imitate the OLED.
        mirrorTimer.Interval = 850;
        Append("Beta manager ready. Firmware updater accepts .bin only.");
        Shown += async (_, _) => await SyncDeviceState();
    }

    private Form? settingsWindow;
    private void ShowSettings()
    {
        if (settingsWindow is { IsDisposed: false }) { settingsWindow.Activate(); return; }
        settingsWindow = new Form
        {
            Text = "PocketOS / Settings & Tools",
            Width = 910,
            Height = 690,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Surface,
            ForeColor = Ink,
            Font = new Font("Consolas", 10),
            KeyPreview = false
        };
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        TabControlStyler.Apply(tabs);

        tabs.TabPages.Add(BuildPartner());
        tabs.TabPages.Add(BuildWifi());
        tabs.TabPages.Add(BuildHunt());
        tabs.TabPages.Add(BuildSettings());
        tabs.TabPages.Add(BuildUpdates());
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        layout.Controls.Add(tabs, 0, 0);
        var logPanel = new Panel { Dock = DockStyle.Fill };
        log.Dock = DockStyle.Fill; log.BackColor = Color.Black;
        log.ForeColor = Ink; logPanel.Controls.Add(log);
        layout.Controls.Add(logPanel, 0, 1);
        settingsWindow.Controls.Add(layout);
        ApplyMonochrome(layout);
        settingsWindow.FormClosed += (_, _) => { logPanel.Controls.Remove(log); settingsWindow = null; };
        settingsWindow.Show(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (intro != null && keyData == Keys.Escape) { EndWindowIntro(); return true; }
        if (intro != null) return true;
        // Only capture keys while the gamepad window is active; text fields in Settings are unaffected.
        string? action = keyData switch
        {
            Keys.Up => "up",
            Keys.Down => "down",
            Keys.Left => "left",
            Keys.Right => "right",
            Keys.Enter => "select",
            Keys.Back => "back",
            _ => null
        };
        if (action != null) { _ = SendCommand(action); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private TabPage BuildPartner()
    {
        var page = Page("Partner"); ; var panel = Stack(page);
        panel.Controls.Add(Label("Your original bridge continues to perform Pokemon detection, encounter analysis, IV checks and ratings."));
        var exe = new TextBox { Text = cfg.BridgeExe, Width = 510 };
        panel.Controls.Add(Line(Label("Bridge executable"), exe, Button("Browse...", async () => { using var dlg = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" }; if (dlg.ShowDialog() == DialogResult.OK) { exe.Text = cfg.BridgeExe = dlg.FileName; Save(); } await Task.CompletedTask; })));
        panel.Controls.Add(Line(Button("Start Bridge", async () => {
            if (bridge is { HasExited: false }) { Append("Bridge already running"); return; }
            if (!File.Exists(exe.Text)) { Warn("Choose your compiled Pokemon Partner bridge executable first."); return; }
            cfg.BridgeExe = exe.Text; Save();
            try
            {
                if (bridgeListener == null || !bridgeListener.IsListening) { Warn("USB bridge listener unavailable. Check logs."); return; }
                if (string.IsNullOrWhiteSpace(cfg.SaveFolder) || !Directory.Exists(cfg.SaveFolder))
                {
                    using var folder = new FolderBrowserDialog { Description = "Select your emulator save folder for automatic game detection" };
                    if (folder.ShowDialog() != DialogResult.OK) return;
                    cfg.SaveFolder = folder.SelectedPath;
                    Save();
                }
                var start = new ProcessStartInfo(exe.Text) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe.Text)! };
                // Bridge retains its original RetroArch/melonDS memory scanner.
                // Only its transport changes: loopback HTTP -> this Manager -> USB.
                start.ArgumentList.Add("--esp32"); start.ArgumentList.Add(LocalBridgeUrl.TrimEnd('/'));
                start.ArgumentList.Add("--save-folder"); start.ArgumentList.Add(cfg.SaveFolder);
                bridge = Process.Start(start);
                Append("Bridge started: emulator -> localhost -> PocketOS USB / " + cfg.ComPort);
            }
            catch (Exception ex) { Warn(ex.Message); }
            await Task.CompletedTask;
        }), Button("Stop Bridge", async () => { if (bridge is { HasExited: false }) { bridge.Kill(entireProcessTree: true); Append("Bridge stopped"); } await Task.CompletedTask; })));
        panel.Controls.Add(Label("Select the ESP32 USB COM port to control the OLED without Wi-Fi."));
        var controlPorts = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
        void RefreshControlPorts() { controlPorts.Items.Clear(); controlPorts.Items.AddRange(SerialPort.GetPortNames()); if (controlPorts.Items.Contains(cfg.ComPort)) controlPorts.SelectedItem = cfg.ComPort; else if (controlPorts.Items.Count > 0) controlPorts.SelectedIndex = 0; }
        panel.Controls.Add(Line(Label("OLED USB port"), controlPorts, Button("Refresh", async () => { RefreshControlPorts(); await Task.CompletedTask; })));
        controlPorts.SelectedIndexChanged += async (_, _) => { cfg.ComPort = controlPorts.SelectedItem as string ?? ""; serialRetryAfter = DateTime.MinValue; lastSerialIssue = ""; await CloseSerialAsync(); Save(); await SyncDeviceState(); };
        RefreshControlPorts();
        panel.Controls.Add(Label("Direct controls: Up / Down / Select / Back, or launch an app from the menu."));
        panel.Controls.Add(Line(Button("Pokemon Mode", () => SendCommand("pokemon")), Button("Open Menu", () => SendCommand("menu")), Button("IV Bars", () => SendCommand("ivs")), Button("Moves", () => SendCommand("moves"))));
        panel.Controls.Add(Line(Button("Up", () => SendCommand("up")), Button("Down", () => SendCommand("down")), Button("Select", () => SendCommand("select")), Button("Back", () => SendCommand("back"))));
        var apps = new ComboBox { Width = 265, DropDownStyle = ComboBoxStyle.DropDownList };
        apps.Items.AddRange(new object[] { "Pokemon Partner", "Pokemon Info", "Wild Encounter", "Shiny Hunt", "Clock", "About", "PC Monitor", "Settings" }); apps.SelectedIndex = 0;
        panel.Controls.Add(Line(Label("Open OLED app"), apps, Button("Open", () => SendCommand("app:" + apps.SelectedIndex))));
        return page;
    }



    // Native Pokémon Studio. No external EXE is launched. This is a first-stage
    // save inspector and Gen 4 RNG explorer, NOT the full PKHeX / PokéFinder engine.
    // Multi-generation navigation. Real save decoding requires PKHeX.Core integration.
    private TabPage BuildPokemonTools()
    {
        var page = Page("Pokemon Studio");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        var generations = new ComboBox { Width = 410, DropDownStyle = ComboBoxStyle.DropDownList };
        generations.Items.AddRange(new object[] {
            "Gen 1 - Red / Blue / Yellow", "Gen 2 - Gold / Silver / Crystal",
            "Gen 3 - Ruby / Sapphire / Emerald / FireRed / LeafGreen",
            "Gen 4 - Diamond / Pearl / Platinum / HeartGold / SoulSilver",
            "Gen 5 - Black / White / Black 2 / White 2",
            "Gen 6 - X / Y / Omega Ruby / Alpha Sapphire",
            "Gen 7 - Sun / Moon / Ultra Sun / Ultra Moon",
            "Gen 8 - Sword / Shield / BDSP / Legends Arceus",
            "Gen 9 - Scarlet / Violet"
        });
        generations.SelectedIndex = 3;
        header.Controls.Add(Label("Game generation"));
        header.Controls.Add(generations);
        var help = Label("Choose a generation to view relevant tools. Save editing is not yet available.");
        help.AutoSize = true;
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        top.Controls.Add(header, 0, 0);
        top.Controls.Add(help, 0, 1);
        layout.Controls.Add(top, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var savePage = BuildSaveInspector();
        var rngPage = BuildGen4Rng();
        tabs.TabPages.Add(savePage);
        tabs.TabPages.Add(rngPage);
        tabs.TabPages.Add(BuildStudioAbout());
        generations.SelectedIndexChanged += (_, _) => {
            bool g4 = generations.SelectedIndex == 3;
            rngPage.Text = g4 ? "Gen 4 RNG (Advanced)" : "Gen 4 RNG (Gen 4 only)";
            help.Text = g4
                ? "Gen 4 selected. Advanced experimental Gen 4 RNG frame search is available."
                : "Generation selected. Save inspection works for files of any generation; editing and RNG are not yet supported here.";
            if (!g4 && tabs.SelectedTab == rngPage) tabs.SelectedTab = savePage;
        };
        layout.Controls.Add(tabs, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildSaveInspector()
    {
        var page = Page("Open Save");
        var panel = Stack(page);
        panel.Controls.Add(Label("MY POKEMON / SAVE FILE"));
        panel.Controls.Add(Label("Open a save to see file details. Pokemon boxes and editing need the PKHeX save engine."));
        var path = new TextBox { Width = 560, ReadOnly = true };
        var info = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Width = 750,
            Height = 150,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = Ink,
            Text = "No save opened.\r\nThis is a read-only preview; your files are never modified."
        };
        panel.Controls.Add(Line(path, Button("Open Pokemon Save", () => {
            using var dlg = new OpenFileDialog { Filter = "Save files (*.sav;*.dsv;*.srm;*.dat;*.bin)|*.sav;*.dsv;*.srm;*.dat;*.bin|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
            try
            {
                var file = new FileInfo(dlg.FileName);
                path.Text = file.FullName;
                info.Text = "Name: " + file.Name + Environment.NewLine
                    + "Size: " + file.Length.ToString("N0") + " bytes" + Environment.NewLine
                    + "Last modified: " + file.LastWriteTime.ToString("g") + Environment.NewLine
                    + "Mode: READ ONLY - no changes made" + Environment.NewLine
                    + "Game identification: not available without save decoding";
                Append("Opened save details read-only: " + file.Name);
            }
            catch (Exception ex) { info.Text = "Unable to open save: " + ex.Message; }
            return Task.CompletedTask;
        })));
        panel.Controls.Add(info);
        panel.Controls.Add(Label("Your selected generation does not verify the save format. Back up saves before editing them in any application."));
        panel.Controls.Add(Label("Coming later: Pokemon boxes, sprites, moves, IVs/EVs, legality checks and safe save editing via PKHeX.Core."));
        return page;
    }

    private TabPage BuildGen4Rng()
    {
        var page = Page("Gen 4 RNG (Advanced)");
        var panel = Stack(page);
        panel.Controls.Add(Label("GEN 4 HEARTGOLD / SOULSILVER RNG WORKSPACE"));
        panel.Controls.Add(Label("In-app LCRNG PID/IV candidate search. Results are NOT verified encounter-method or legality results."));
        var seed = new TextBox { Width = 115, Text = "00000000", CharacterCasing = CharacterCasing.Upper };
        var startFrame = new NumericUpDown { Width = 85, Minimum = 0, Maximum = 1000000 };
        var scan = new NumericUpDown { Width = 95, Minimum = 1, Maximum = 1000000, Value = 10000 };
        var tid = new NumericUpDown { Width = 90, Minimum = 0, Maximum = 65535 };
        var sid = new NumericUpDown { Width = 90, Minimum = 0, Maximum = 65535 };
        panel.Controls.Add(Line(Label("Seed"), seed, Label("Start"), startFrame, Label("Scan"), scan));
        panel.Controls.Add(Line(Label("Trainer ID"), tid, Label("Secret ID"), sid));
        var nature = new ComboBox { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList };
        string[] natures = { "Hardy", "Lonely", "Brave", "Adamant", "Naughty", "Bold", "Docile", "Relaxed", "Impish", "Lax", "Timid", "Hasty", "Serious", "Jolly", "Naive", "Modest", "Mild", "Quiet", "Bashful", "Rash", "Calm", "Gentle", "Sassy", "Careful", "Quirky" };
        nature.Items.Add("Any nature"); nature.Items.AddRange(natures); nature.SelectedIndex = 0;
        var shiny = new CheckBox { Text = "Shiny only", AutoSize = true };
        var minIv = new NumericUpDown { Width = 75, Minimum = 0, Maximum = 31, Value = 0 };
        panel.Controls.Add(Line(Label("Nature"), nature, Label("Minimum all IVs"), minIv, shiny));
        var summary = Label("Ready. The search limits displayed results to 500 matches.");
        var results = new DataGridView
        {
            Width = 800,
            Height = 310,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.Black,
            ForeColor = Color.Black,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        foreach (string title in new[] { "Frame", "Seed", "PID", "Nature", "Shiny", "HP", "Atk", "Def", "SpA", "SpD", "Spe" })
            results.Columns.Add(title, title);
        panel.Controls.Add(Line(Button("Search frames", () => {
            if (seed.Text.Trim().Length != 8 || !uint.TryParse(seed.Text.Trim(),
                System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint state))
            {
                MessageBox.Show(this, "Enter exactly eight hexadecimal seed digits, such as 1234ABCD.", "Invalid seed");
                return Task.CompletedTask;
            }
            results.Rows.Clear();
            int first = (int)startFrame.Value;
            int total = (int)scan.Value;
            for (int i = 0; i < first; i++) state = G4Next(state);
            int matches = 0;
            for (int offset = 0; offset < total; offset++)
            {
                uint frameSeed = state;
                uint temp = state;
                ushort lo = (ushort)(G4Next(ref temp) >> 16);
                ushort hi = (ushort)(G4Next(ref temp) >> 16);
                uint pid = ((uint)hi << 16) | lo;
                ushort iv1 = (ushort)(G4Next(ref temp) >> 16);
                ushort iv2 = (ushort)(G4Next(ref temp) >> 16);
                int hp = iv1 & 31, atk = (iv1 >> 5) & 31, def = (iv1 >> 10) & 31;
                int spa = (iv2 >> 5) & 31, spd = (iv2 >> 10) & 31, spe = iv2 & 31;
                int natureIndex = (int)(pid % 25);
                bool isShiny = (((ushort)(pid >> 16)) ^ ((ushort)pid) ^ (ushort)tid.Value ^ (ushort)sid.Value) < 8;
                state = G4Next(frameSeed);
                if (nature.SelectedIndex > 0 && natureIndex != nature.SelectedIndex - 1) continue;
                if (shiny.Checked && !isShiny) continue;
                int threshold = (int)minIv.Value;
                if (hp < threshold || atk < threshold || def < threshold || spa < threshold || spd < threshold || spe < threshold) continue;
                matches++;
                if (results.Rows.Count < 500)
                    results.Rows.Add(first + offset, frameSeed.ToString("X8"), pid.ToString("X8"), natures[natureIndex],
                        isShiny ? "YES" : "-", hp, atk, def, spa, spd, spe);
            }
            summary.Text = $"Scanned {total:N0} candidate frames; {matches:N0} matches; {results.Rows.Count:N0} displayed.";
            return Task.CompletedTask;
        }), Button("Copy PID", () => {
            if (results.CurrentRow?.Cells[2].Value is string pid) Clipboard.SetText(pid);
            return Task.CompletedTask;
        }), Button("Export CSV", () => {
            using var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "PocketOS_Gen4_RNG.csv" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
            static string Quote(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
            var header = string.Join(",", results.Columns.Cast<DataGridViewColumn>().Select(c => Quote(c.HeaderText)));
            var rows = results.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                .Select(r => string.Join(",", r.Cells.Cast<DataGridViewCell>().Select(c => Quote(c.Value))));
            File.WriteAllLines(dlg.FileName, new[] { header }.Concat(rows));
            return Task.CompletedTask;
        })));
        panel.Controls.Add(summary);
        panel.Controls.Add(results);
        panel.Controls.Add(Label("Raw LCRNG frame model only; PokéFinder's encounter-specific advances, methods, delays and verification are not ported."));
        return page;
    }

    private static uint G4Next(uint seed) => unchecked(seed * 0x41C64E6Du + 0x6073u);
    private static uint G4Next(ref uint seed) { seed = G4Next(seed); return seed; }

    private TabPage BuildStudioAbout()
    {
        var page = Page("About integration");
        var panel = Stack(page);
        panel.Controls.Add(Label("POCKETOS / POKEMON STUDIO - IN-WINDOW FOUNDATION"));
        panel.Controls.Add(Label("No PKHeX or PokeFinder executable is launched. All visible tools run inside this Windows Manager."));
        panel.Controls.Add(Label("Implemented: multi-generation game selector, read-only save metadata, Gen 4 raw LCRNG explorer."));
        panel.Controls.Add(Label("Not yet implemented: PKHeX save model/box editing/legality engine; PokeFinder's full encounter methods, filters and profiles."));
        panel.Controls.Add(Label("These require correctly integrated upstream source/dependencies, license review and tests against actual HeartGold saves."));
        return page;
    }

    // Optional Wi-Fi provisioning over the existing USB connection. Never auto-start radio.
    private TabPage BuildWifi()
    {
        var page = Page("Wi-Fi");
        var panel = Stack(page);
        panel.Controls.Add(Label("WARNING: This board previously reset with BROWNOUT at WIFI_INIT. Wi-Fi stays OFF at boot. Use USB-only if resets continue."));
        panel.Controls.Add(Label("Scanning or connecting activates the ESP32 Wi-Fi radio and may reset the device."));
        var networks = new ListBox { Width = 600, Height = 140 };
        var pass = new TextBox { Width = 300, UseSystemPasswordChar = true };
        var result = Label("Wi-Fi not started");
        panel.Controls.Add(Button("Scan Wi-Fi (may reset ESP32)", async () => {
            networks.Items.Clear();
            if (string.IsNullOrWhiteSpace(cfg.ComPort)) { result.Text = "Select a USB COM port in Settings first"; return; }
            await SerialRequest(cfg.ComPort, "WIFI_SCAN", reply => {
                if (reply.StartsWith("WIFI_NETWORK ")) networks.Items.Add(reply[13..]);
                if (reply.StartsWith("WIFI_ERROR") || reply.StartsWith("WIFI_SCAN_DONE")) result.Text = reply;
            });
        }));
        panel.Controls.Add(networks);
        panel.Controls.Add(Line(Label("Password"), pass));
        panel.Controls.Add(Button("Connect selected network (may reset ESP32)", async () => {
            if (networks.SelectedItem is not string selected) { result.Text = "Select a network first"; return; }
            if (string.IsNullOrWhiteSpace(cfg.ComPort)) { result.Text = "Choose a COM port first"; return; }
            var ssid = selected.Split('|')[0].Trim();
            var command = "WIFI_JOIN " + JsonSerializer.Serialize(new { ssid, password = pass.Text });
            pass.Clear();
            await SerialRequest(cfg.ComPort, command, reply => {
                if (reply.StartsWith("WIFI_")) result.Text = reply;
            });
        }));
        panel.Controls.Add(Button("Check Wi-Fi status", async () => {
            if (string.IsNullOrWhiteSpace(cfg.ComPort)) { result.Text = "Choose a COM port first"; return; }
            await SerialRequest(cfg.ComPort, "WIFI_STATUS", reply => {
                if (reply.StartsWith("WIFI_")) result.Text = reply;
            });
        }));
        panel.Controls.Add(result);
        return page;
    }

    private TabPage BuildHunt()
    {
        var page = Page("Shiny Hunt"); var panel = Stack(page);
        var shiny = new CheckBox { Text = "Optional Shiny Hunt Mode (off by default)", Checked = cfg.ShinyHunt, AutoSize = true };
        shiny.CheckedChanged += (_, _) => { cfg.ShinyHunt = shiny.Checked; Save(); };
        panel.Controls.Add(shiny);
        panel.Controls.Add(Button("Apply to ESP32", async () => {
            if (string.IsNullOrEmpty(cfg.ComPort)) { Warn("Select a USB port on the Pokemon Partner tab first."); return; }
            await SerialRequest(cfg.ComPort, "POCKET_HUNT " + (shiny.Checked ? "1" : "0"), reply => Append(reply));
        }));
        return page;
    }

    private TabPage BuildFirmware()
    {
        var page = Page("Firmware Update"); var panel = Stack(page);
        panel.Controls.Add(Label("USB firmware upload: .bin ONLY. Requires Python and esptool (python -m pip install esptool)."));
        panel.Controls.Add(Label("Use an APPLICATION firmware .bin built for your exact ESP32 and flash partition layout."));
        var ports = new ComboBox { Width = 155, DropDownStyle = ComboBoxStyle.DropDownList };
        void Refresh() { ports.Items.Clear(); ports.Items.AddRange(SerialPort.GetPortNames()); if (ports.Items.Count > 0) ports.SelectedIndex = 0; }
        panel.Controls.Add(Line(Label("Serial Port"), ports, Button("Refresh", async () => { Refresh(); await Task.CompletedTask; })));
        var python = new TextBox { Text = cfg.PythonExe, Width = 230 };
        panel.Controls.Add(Line(Label("Python executable"), python));
        var fileName = new TextBox { ReadOnly = true, Width = 470 };
        panel.Controls.Add(Line(Label("Firmware (.bin)"), fileName, Button("Choose BIN", async () => {
            using var dlg = new OpenFileDialog { Filter = "ESP32 firmware (*.bin)|*.bin", CheckFileExists = true };
            if (dlg.ShowDialog() == DialogResult.OK) { selectedBin = dlg.FileName; fileName.Text = selectedBin; }
            await Task.CompletedTask;
        })));
        var address = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        address.Items.Add("0x10000 - Application only"); address.SelectedIndex = 0;
        panel.Controls.Add(Line(Label("Flash offset"), address));
        panel.Controls.Add(Label("This updater intentionally does not flash bootloader/partitions and cannot accept merged factory images."));
        var agree = new CheckBox { AutoSize = true, Text = "I verified the .bin matches this ESP32 and its existing partition scheme." };
        panel.Controls.Add(agree);
        var progress = new ProgressBar { Width = 610, Style = ProgressBarStyle.Marquee, Visible = false };
        panel.Controls.Add(progress);
        var start = Button("Flash ESP32 (.bin)", async () => {
            if (updating) return;
            if (ports.SelectedItem is not string port || !File.Exists(selectedBin)) { Warn("Select a COM port and .bin firmware file."); return; }
            if (!agree.Checked) { Warn("Confirm firmware compatibility before flashing."); return; }
            await FlashAsync(python.Text, port, selectedBin, progress);
        });
        panel.Controls.Add(start); Refresh(); return page;
    }

    private TabPage BuildSettings()
    {
        var page = Page("Settings"); var panel = Stack(page);
        var address = new TextBox { Width = 370, Text = cfg.EspAddress };
        panel.Controls.Add(Line(Label("ESP32 HTTP URL"), address));
        panel.Controls.Add(Button("Save Address", async () => { cfg.EspAddress = address.Text.TrimEnd('/'); Save(); Append("ESP32 URL updated"); await Task.CompletedTask; }));
        var minimize = new CheckBox { Text = "Minimize to system tray", Checked = cfg.TrayMode, AutoSize = true };
        minimize.CheckedChanged += (_, _) => { cfg.TrayMode = minimize.Checked; Save(); };
        panel.Controls.Add(minimize);
        panel.Controls.Add(Label("Windows Manager beta. Direct OLED navigation requires matching ESP32 USB serial command handlers."));
        return page;
    }

    private TabPage BuildUpdates()
    {
        var page = Page("Updates");
        var panel = Stack(page);

        panel.Controls.Add(Label("Official releases: github.com/cloudkirito20/PocketOS"));
        panel.Controls.Add(Label("Checks once every 24 hours while running. Manual checks are available anytime."));

        var auto = new CheckBox
        {
            Text = "Automatically check GitHub Releases daily",
            Checked = cfg.AutoCheckUpdates,
            AutoSize = true
        };

        auto.CheckedChanged += (_, _) =>
        {
            cfg.AutoCheckUpdates = auto.Checked;
            Save();
        };

        panel.Controls.Add(auto);

        var message = Label("No update check completed yet.");
        panel.Controls.Add(message);

        panel.Controls.Add(Button("Check GitHub for Updates", async () =>
        {
            await CheckGithubUpdatesAsync(true, message);
        }));


        panel.Controls.Add(Button("Download and Install Windows + Bridge", async () =>
        {
            if (pendingManifest == null)
            {
                Warn("Check for updates first.");
                return;
            }

            if (MessageBox.Show(
                "Download the verified update, close PocketOS, install Manager and Bridge, and restart?",
                "Install PocketOS Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            Warn("Manifest update detected. Installer asset integration will be added in the next update phase.");

            await Task.CompletedTask;
        }));


        panel.Controls.Add(Button("Download ESP32 Firmware (.bin)", async () =>
        {
            if (pendingManifest == null)
            {
                Warn("Check for updates first.");
                return;
            }

            Warn("ESP32 manifest firmware download integration will be added in the next update phase.");

            await Task.CompletedTask;
        }));


        panel.Controls.Add(Label(
            "ESP32 flashing is never automatic. Confirm the correct board and partition layout before flashing."
        ));

        return page;
    }

    private async Task CheckGithubUpdatesAsync(bool manual, Label? target = null)
    {
        if (updating || (!manual && (!cfg.AutoCheckUpdates || DateTime.UtcNow - cfg.LastGitHubCheckUtc < TimeSpan.FromHours(24)))) return;
        updating = true;
        try
        {
            if (target != null) target.Text = "Checking GitHub Releases...";
            var manifestClient = new PocketOSManager.Updates.ManifestUpdateClient();

            var manifest = await manifestClient.CheckAsync(
                PocketOSManager.Updates.UpdateSource.ManifestUrl);

            if (manifest == null)
            {
                throw new Exception("Update manifest unavailable.");
            }

            cfg.LastGitHubCheckUtc = DateTime.UtcNow;
            Save();

            string msg =
                $"Latest: {manifest.Version} | " +
                $"Manager {manifest.ManagerVersion} | " +
                $"Bridge {manifest.BridgeVersion} | " +
                $"ESP32 {manifest.Esp32Version}";
            Append("GitHub: " + msg);
            if (target != null) target.Text = msg;
            else if (pendingManifest != null)
            {
                BeginInvoke((Action)(() => {
                    if (!IsDisposed && MessageBox.Show("New PocketOS component updates are available. Open Updates?", "PocketOS Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) ShowSettings();
                }));
            }
        }
        catch (Exception ex) { Append("GitHub update check: " + ex.Message); if (target != null) target.Text = "Update check failed: " + ex.Message; }
        finally { updating = false; }
    }

    private async Task InstallGithubUpdateAsync(GithubReleaseInfo release, Label message)
    {
        try
        {
            message.Text = "Downloading and verifying Windows update...";
            var zip = await GithubReleases.DownloadVerifiedAsync(client, release, "windows", CancellationToken.None);
            var helper = Path.Combine(AppContext.BaseDirectory, "Install_Update.ps1");
            if (!File.Exists(helper)) throw new FileNotFoundException("Updater helper missing", helper);
            if (bridge is { HasExited: false }) bridge.Kill(entireProcessTree: true);
            await CloseSerialAsync();
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", helper, "-Archive", zip, "-InstallDirectory", AppContext.BaseDirectory, "-WaitPid", Environment.ProcessId.ToString() }) psi.ArgumentList.Add(arg);
            Process.Start(psi);
            cfg.TrayMode = false; Close();
        }
        catch (Exception ex) { message.Text = "Update failed: " + ex.Message; }
    }

    private async Task FlashAsync(string pythonExe, string port, string file, ProgressBar indicator)
    {
        try
        {
            using var stream = File.OpenRead(file);
            if (stream.Length < 256 || stream.ReadByte() != 0xE9) { Warn("Invalid ESP32 image header. Only normal ESP32 application images are accepted."); return; }
            if (MessageBox.Show($"Flash {Path.GetFileName(file)} to {port} at 0x10000? Existing application will be replaced.", "Confirm ESP32 Firmware Update", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            updating = true; indicator.Visible = true; Append("Starting USB firmware flash...");
            await CloseSerialAsync();
            var psi = new ProcessStartInfo(pythonExe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "-m", "esptool", "--chip", "esp32", "--port", port, "--baud", "460800", "write_flash", "0x10000", file }) psi.ArgumentList.Add(arg);
            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) SafeLog(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) SafeLog(e.Data); };
            proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            Append(proc.ExitCode == 0 ? "Flashing process completed. Verify firmware boot and ESP32 reconnection." : $"Flashing failed (exit code {proc.ExitCode}). Use USB recovery as needed.");
        }
        catch (Exception ex) { Warn("Flash error: " + ex.Message); }
        finally { indicator.Visible = false; updating = false; }
    }

    private async Task CloseSerialAsync()
    {
        await serialGate.WaitAsync();
        try { activeSerial?.Dispose(); activeSerial = null; activeSerialName = ""; }
        finally { serialGate.Release(); }
    }

    private void StartLocalBridgeListener()
    {
        try
        {
            bridgeListener = new HttpListener();
            bridgeListener.Prefixes.Add(LocalBridgeUrl);
            bridgeListener.Start();
            _ = ProcessBridgeRequestsAsync(bridgeStop.Token);
        }
        catch (Exception ex)
        {
            bridgeListener?.Close(); bridgeListener = null;
            BeginInvokeSafe(() => Append("USB bridge listener unavailable: " + ex.Message));
        }
    }

    private void BeginInvokeSafe(Action action)
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(action);
    }

    private async Task ProcessBridgeRequestsAsync(CancellationToken token)
    {
        var listener = bridgeListener;
        if (listener == null) return;
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext request;
            try { request = await listener.GetContextAsync(); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = HandleBridgeRequestAsync(request);
        }
    }

    private async Task HandleBridgeRequestAsync(HttpListenerContext context)
    {
        int code = 200;
        string body = "{\"ok\":true}";
        try
        {
            string route = context.Request.Url?.AbsolutePath ?? "";
            if (route == "/state" && context.Request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                string json = await reader.ReadToEndAsync();
                using var parsed = JsonDocument.Parse(json);
                if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                    !parsed.RootElement.TryGetProperty("species", out var species) ||
                    !species.TryGetInt32(out int id) || id <= 0 || id > 1025 || json.Length > 3500)
                    throw new InvalidDataException("Invalid encounter payload");
                bool acknowledged = false;
                await SerialRequest(cfg.ComPort, "POCKET_ENCOUNTER_JSON " + json,
                    response => { if (response == "POCKET_ENCOUNTER_OK") acknowledged = true; });
                if (!acknowledged) { code = 503; body = "{\"error\":\"USB encounter not acknowledged\"}"; }
                else BeginInvokeSafe(() => Append("USB encounter: species #" + id));
            }
            else if (route == "/heartbeat")
            {
                await SerialRequest(cfg.ComPort, "POCKET_ENCOUNTER_HEARTBEAT", _ => { });
            }
            else if (route == "/bridge" || route == "/health")
            {
                // Reports local listener readiness only; /state requires USB acknowledgement.
            }
            else { code = 404; body = "{\"error\":\"Unknown route\"}"; }
        }
        catch (Exception ex)
        {
            code = ex is InvalidDataException or JsonException ? 400 : 503;
            body = JsonSerializer.Serialize(new { error = ex.Message });
        }
        try
        {
            context.Response.StatusCode = code;
            context.Response.ContentType = "application/json";
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception) { }
        finally { context.Response.Close(); }
    }

    private async Task SerialRequest(string port, string command, Action<string> onLine)
    {
        if (updating || string.IsNullOrWhiteSpace(port)) return;
        await serialGate.WaitAsync();
        try
        {
            var replies = await Task.Run(() =>
            {
                if (activeSerial == null || !activeSerial.IsOpen || activeSerialName != port)
                {
                    activeSerial?.Dispose();
                    activeSerial = new SerialPort(port, 115200)
                    {
                        ReadTimeout = 170,
                        WriteTimeout = 600,
                        NewLine = "\n",
                        DtrEnable = false,
                        RtsEnable = false
                    };
                    activeSerial.Open();
                    activeSerialName = port;
                    activeSerial.DiscardInBuffer();
                }
                var serial = activeSerial;
                serial.WriteLine(command);
                var replies = new List<string>();
                // Frame is ~2 KB over 115200 baud; allow enough time without
                // blocking button commands for seconds on every request.
                var deadline = DateTime.UtcNow.AddMilliseconds(command == "WIFI_SCAN" ? 7000 : command == "POCKET_GET_FRAME" ? 650 : command.StartsWith("WIFI_") ? 1200 : command.StartsWith("POCKET_ENCOUNTER_") ? 450 : 130);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        var line = serial.ReadLine().Trim();
                        if (line.Length == 0) continue;
                        replies.Add(line);
                        if (command == "POCKET_GET_FRAME" && line.StartsWith("POCKET_FRAME ")) break;
                        if (command == "POCKET_GET_STATE" && line.StartsWith("POCKET_STATE ")) break;
                        if (command.StartsWith("POCKET_CMD ") && line.StartsWith("POCKET_OK")) break;
                        if (command.StartsWith("POCKET_ENCOUNTER_") && line.StartsWith("POCKET_ENCOUNTER_")) break;
                        if (line.StartsWith("WIFI_DISABLED") || line.StartsWith("WIFI_SCAN_DONE") || line.StartsWith("WIFI_CONNECTING") || line.StartsWith("WIFI_ERROR") || line.StartsWith("WIFI_CONNECTED") || line.StartsWith("WIFI_DISCONNECTED")) break;
                    }
                    catch (TimeoutException) { }
                }
                return replies;
            });
            foreach (var reply in replies) onLine(reply);
            if (replies.Count > 0) { serialRetryAfter = DateTime.MinValue; lastSerialIssue = ""; }
        }
        catch (UnauthorizedAccessException)
        {
            activeSerial?.Dispose(); activeSerial = null; activeSerialName = "";
            ReportSerialIssue(port, "PORT BUSY / CLOSE SERIAL MONITOR OR OTHER APP", 4);
        }
        catch (IOException ex)
        {
            activeSerial?.Dispose(); activeSerial = null; activeSerialName = "";
            ReportSerialIssue(port, "USB UNAVAILABLE / CHECK CONNECTION", 4, ex.Message);
        }
        catch (Exception ex)
        {
            activeSerial?.Dispose(); activeSerial = null; activeSerialName = "";
            ReportSerialIssue(port, "CONNECTION FAILED / CHECK SETTINGS", 4, ex.Message);
        }
        finally { serialGate.Release(); }
    }
    private void ReportSerialIssue(string port, string userMessage, int cooldownSeconds, string? detail = null)
    {
        serialRetryAfter = DateTime.UtcNow.AddSeconds(cooldownSeconds);
        // Keep the last successfully received OLED image on transient serial errors.
        status.Text = $"{port}: {userMessage}";
        string issue = $"{port}: {userMessage}";
        if (issue != lastSerialIssue) { Append(issue + (detail == null ? "" : " / " + detail)); lastSerialIssue = issue; }
    }

    private void ApplyDeviceState(string reply)
    {
        if (!reply.StartsWith("POCKET_STATE ")) return;
        var parts = reply[13..].Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[1], out int selected) || !int.TryParse(parts[2], out int active)) return;
        if (selected < 0 || selected >= pocketApps.Length || active < 0 || active >= pocketApps.Length) return;
        void Update() { previewSelection = selected; previewMode = parts[0] == "MENU" ? "MENU" : pocketApps[active]; screenPreview.Invalidate(); status.Text = "DEVICE STATE: " + previewMode + " / AWAITING FRAME"; }
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }
    private void ApplyDeviceFrame(string reply)
    {
        if (!reply.StartsWith("POCKET_FRAME ")) return;
        var hex = reply[13..].Trim();
        if (hex.Length != 2048) { Append("Invalid OLED framebuffer length."); return; }
        byte[] bytes;
        try { bytes = Convert.FromHexString(hex); }
        catch (FormatException) { Append("Invalid OLED framebuffer data."); return; }
        void Update() { serialRetryAfter = DateTime.MinValue; lastSerialIssue = ""; liveFrame = bytes; screenPreview.Invalidate(); status.Text = "● LIVE OLED MIRROR / " + cfg.ComPort; }
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }
    private async Task SyncDeviceState()
    {
        if (busySync || updating || System.Threading.Volatile.Read(ref pendingCommands) > 0 ||
            (DateTime.UtcNow - lastNavigationUtc).TotalMilliseconds < 320 ||
            serialGate.CurrentCount == 0 || string.IsNullOrWhiteSpace(cfg.ComPort) || DateTime.UtcNow < serialRetryAfter) return;
        busySync = true;
        try
        {
            // Ask for the device's actual app and the raw framebuffer; never infer pixels from a menu command.
            await SerialRequest(cfg.ComPort, "POCKET_GET_FRAME", ApplyDeviceFrame);
        }
        finally { busySync = false; }
    }

    private async Task SendCommand(string command)
    {
        if (!string.IsNullOrEmpty(cfg.ComPort))
        {
            if (DateTime.UtcNow < serialRetryAfter) { status.Text = "PORT BUSY / RETRYING AUTOMATICALLY"; return; }
            System.Threading.Interlocked.Increment(ref pendingCommands);
            lastNavigationUtc = DateTime.UtcNow;
            try
            {
                await SerialRequest(cfg.ComPort, "POCKET_CMD " + command, reply => {
                    // Keep command processing lightweight; do not repaint the whole screen.
                    if (reply.StartsWith("POCKET_OK")) status.Text = "USB CONTROL / " + cfg.ComPort;
                });
            }
            finally
            {
                lastNavigationUtc = DateTime.UtcNow;
                System.Threading.Interlocked.Decrement(ref pendingCommands);
            }
            return;
        }
        try
        {
            using var response = await client.PostAsJsonAsync(cfg.EspAddress.TrimEnd('/') + "/pocketos/control", new { action = command });
            Append($"OLED command {command}: HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) { Append("ESP32 unavailable: " + ex.Message); }
    }

    private static void ApplyMonochrome(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is TextBox tb && tb != null) { tb.BackColor = Color.FromArgb(3, 9, 6); tb.ForeColor = Ink; tb.BorderStyle = BorderStyle.FixedSingle; }
            else if (c is ListBox lb) { lb.BackColor = Color.FromArgb(3, 9, 6); lb.ForeColor = Ink; lb.BorderStyle = BorderStyle.FixedSingle; }
            else if (c is ComboBox cb) { cb.BackColor = Color.FromArgb(10, 18, 13); cb.ForeColor = Ink; cb.FlatStyle = FlatStyle.Flat; }
            else if (c is Button btn) { btn.FlatStyle = FlatStyle.Flat; btn.BackColor = Color.FromArgb(23, 41, 29); btn.ForeColor = Ink; btn.FlatAppearance.BorderColor = Frame; btn.FlatAppearance.BorderSize = 1; btn.Height = Math.Max(34, btn.Height); }
            else if (c is TabPage page) { page.BackColor = Surface; page.ForeColor = Ink; }
            else if (c is Label lab) { lab.ForeColor = Ink; }
            else if (c is CheckBox check) { check.ForeColor = Ink; }
            ApplyMonochrome(c);
        }
    }

    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(5, 9, 7));
        g.SmoothingMode = SmoothingMode.None;
        int scale = Math.Max(1, Math.Min((screenPreview.Width - 24) / 128, (screenPreview.Height - 35) / 64));
        int width = 128 * scale, height = 64 * scale;
        int ox = (screenPreview.Width - width) / 2, oy = (screenPreview.Height - height) / 2;
        g.FillRectangle(Brushes.Black, ox, oy, width, height);
        g.DrawRectangle(new Pen(Frame, 2), ox - 3, oy - 3, width + 5, height + 5);
        if (liveFrame is { Length: 1024 } frame)
        {
            using var pixelBrush = new SolidBrush(Ink);
            for (int yy = 0; yy < 64; ++yy)
                for (int xx = 0; xx < 128; ++xx)
                    if ((frame[xx + (yy / 8) * 128] & (1 << (yy & 7))) != 0)
                        g.FillRectangle(pixelBrush, ox + xx * scale, oy + yy * scale, scale, scale);
            return;
        }
        using var emptyFont = new Font("Consolas", 8, FontStyle.Bold);
        var message = string.IsNullOrWhiteSpace(cfg.ComPort) ? "CONNECT ESP32 IN SETTINGS" : "WAITING FOR OLED FRAME";
        using var brush = new SolidBrush(Color.WhiteSmoke);
        g.DrawString(message, emptyFont, brush, ox + 8, oy + height / 2 - 8);

    }

    private LazyTechIntro? intro;
    private void BeginWindowIntro()
    {
        intro = new LazyTechIntro { Dock = DockStyle.Fill };
        intro.Finished += (_, _) => EndWindowIntro();
        Controls.Add(intro);
        intro.BringToFront();
        intro.Begin();
    }
    private void EndWindowIntro()
    {
        if (intro == null) return;
        var old = intro;
        intro = null;
        Controls.Remove(old);
        old.Dispose();
        ActiveControl = null;
    }
    private void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (cfg.TrayMode && WindowState == FormWindowState.Minimized) Hide(); }
    protected override void OnFormClosed(FormClosedEventArgs e) { bridgeStop.Cancel(); bridgeListener?.Close(); bridgeStop.Dispose(); mirrorTimer.Stop(); mirrorTimer.Dispose(); githubTimer.Stop(); githubTimer.Dispose(); activeSerial?.Dispose(); serialGate.Dispose(); tray.Visible = false; tray.Dispose(); client.Dispose(); base.OnFormClosed(e); }
    private static Settings LoadSettings() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(ConfigPath)) ?? new(); } catch { return new(); } }
    private void Save() { try { Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!); File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true })); } catch (Exception ex) { Append("Settings save failed: " + ex.Message); } }
    private void SafeLog(string message) { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => Append(message))); }
    private void Append(string message) { if (!IsDisposed) log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); }
    private void Warn(string message) { Append(message); MessageBox.Show(this, message, "PocketOS Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private static TabPage Page(string title) => new(title) { Padding = new Padding(14), AutoScroll = true };
    private static FlowLayoutPanel Stack(TabPage page) { var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; page.Controls.Add(stack); return stack; }
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(6, 10, 6, 8) };
    private static Button Button(string text, Func<Task> fn) { var b = new Button { Text = text, AutoSize = true, Margin = new Padding(6), Height = 32 }; b.Click += async (_, _) => { b.Enabled = false; try { await fn(); } finally { b.Enabled = true; } }; return b; }
    private static Control Line(params Control[] controls) { var line = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false }; line.Controls.AddRange(controls); return line; }
}

public sealed class LazyTechIntro : Control
{
    private int frame;
    private readonly System.Windows.Forms.Timer tick = new() { Interval = 80 };
    private readonly Image? companyLogo;
    public event EventHandler? Finished;
    public LazyTechIntro()
    {
        BackColor = Color.Black;
        DoubleBuffered = true;
        // This overlay occupies only the regular resizable main window client area.
        using var resource = typeof(LazyTechIntro).Assembly.GetManifestResourceStream("PocketOSManager.Assets.LazyTechMonochrome.png");
        if (resource != null) companyLogo = new Bitmap(resource);
        tick.Tick += (_, _) => { ++frame; Invalidate(); if (frame >= 64) Finished?.Invoke(this, EventArgs.Empty); };

    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        float scale = Math.Min(ClientSize.Width / 1100f, ClientSize.Height / 710f);
        var state = g.Save();
        g.TranslateTransform((ClientSize.Width - 1100 * scale) / 2, (ClientSize.Height - 710 * scale) / 2);
        g.ScaleTransform(scale, scale);
        using var outline = new Pen(Color.White, 8);
        using var thin = new Pen(Color.White, 3);
        using var white = new SolidBrush(Color.White);
        using var bold = new Font("Consolas", 28, FontStyle.Bold);
        using var small = new Font("Consolas", 15, FontStyle.Bold);
        if (frame < 31)
        {
            // Capture beat rendered inside the normal application window.
            int bob = (int)(Math.Sin(frame * .29) * 18);
            var ball = new Rectangle(460, 230 + bob, 180, 180);
            g.DrawEllipse(outline, ball);
            g.DrawLine(outline, 460, 320 + bob, 640, 320 + bob);
            g.DrawEllipse(outline, 528, 288 + bob, 44, 44);
            if (frame < 12)
            {
                g.DrawEllipse(thin, 465 - frame * 2, 230 + bob - frame * 2, 170 + frame * 4, 170 + frame * 4);
                g.DrawString("CAPTURING...", small, white, 464, 465);
            }
            else
            {
                g.DrawString("GOTCHA!", bold, white, 463, 475);
            }
        }
        else
        {
            int opening = Math.Min(78, (frame - 30) * 7);
            g.DrawArc(outline, 460, 230 - opening, 180, 180, 180, 180);
            g.DrawArc(outline, 460, 230 + opening, 180, 180, 0, 180);
            if (frame >= 37)
            {
                // Render the user's real LazyTech logo, converted to black and white.
                if (companyLogo != null)
                    g.DrawImage(companyLogo, new Rectangle(268, 168, 565, 380));
                else
                    g.DrawString("LazyTech", bold, white, 446, 347);
                g.DrawString("PRESENTS", small, white, 492, 570);
            }
        }
        g.Restore(state);
    }
    public void Begin() => tick.Start();
    public void Skip() => Finished?.Invoke(this, EventArgs.Empty);
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tick.Stop(); tick.Dispose(); companyLogo?.Dispose(); }
        base.Dispose(disposing);
    }
}
