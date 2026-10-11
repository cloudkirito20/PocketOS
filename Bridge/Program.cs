using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PKHeX.Core;

namespace PokemonPartnerBridge;

internal record EncounterPokemon(
    string Source,
    string Game,
    string Nickname,
    int Species,
    int Level,
    bool Shiny,
    string Nature,
    string NatureEffect,
    string Ability,
    string Gender,
    int Stars,
    int PerfectIVs,
    int IVHP, int IVATK, int IVDEF, int IVSPA, int IVSPD, int IVSPE,
    int EVHP, int EVATK, int EVDEF, int EVSPA, int EVSPD, int EVSPE,
    int StatHP, int StatATK, int StatDEF, int StatSPA, int StatSPD, int StatSPE,
    string Move1, string Move2, string Move3, string Move4,
    DateTimeOffset UpdatedAt);

internal record ScanRegion(uint Start, int Length, string Label);

internal sealed record ScanCandidate(
    uint Address,
    PK6 Pokemon,
    int Score,
    string RegionLabel,
    uint WindowAddress,
    int WindowSize,
    int WindowOffset,
    byte[] Raw,
    bool DecryptedFromEncrypted);

internal sealed record Gen6AccessorCandidate(
    uint AccessorAddress,
    uint Vtable,
    uint PartyFlag,
    uint DataPointer,
    bool DataEncrypted,
    bool EncryptOnExit,
    PK6 Pokemon,
    int DecodeScore,
    int RankScore,
    string RegionLabel,
    byte[] RawPokemon,
    bool DecryptedFromEncrypted);

internal record LiveProfile(
    string Key,
    string DisplayName,
    string Transport,
    uint EncounterAddress,
    int PokemonSize,
    int Generation,
    ScanRegion[] ScanRegions);

internal sealed class BridgeConfig
{
    public string SaveFolder { get; set; } = "";
    public string Game { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Esp32 { get; set; } = "http://192.168.68.52";
    public int PollMs { get; set; } = 400;
    public int HeartbeatMs { get; set; } = 2000;
    public string CitraHost { get; set; } = "127.0.0.1";
    public int CitraPort { get; set; } = 45987;
    public string RetroHost { get; set; } = "127.0.0.1";
    public int RetroPort { get; set; } = 55355;
}

internal sealed record Gen6EnemySnapshot(
    PK6 Pokemon,
    uint Address,
    int Slot,
    int Score,
    byte[] Raw,
    string Token);

internal sealed record Gen4BattleMon(
    int Species,
    int Level,
    int Ability,
    int Gender,
    uint PID,
    ushort TID,
    ushort SID,
    int CurrentHP,
    int MaxHP,
    int Attack,
    int Defense,
    int Speed,
    int SpAttack,
    int SpDefense,
    int IVHP, int IVATK, int IVDEF, int IVSPA, int IVSPD, int IVSPE,
    int Move1, int Move2, int Move3, int Move4,
    byte[] Raw);

internal sealed record SaveCandidate(string Path, string GameKey, DateTime LastWriteUtc);

internal sealed record FolderDetection(
    string Transport,
    string GameKey,
    string SelectedSavePath,
    string Reason,
    SaveCandidate[] Candidates);

internal static class Program
{
    private static readonly Dictionary<string, LiveProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // Gen 6: use several NTR/Citra virtual-memory windows instead of the old
        // low 0x008xxxxx guesses.  Gen 6 runtime structures normally live in
        // the 0x08xxxxxx range.  The bridge scans only when it has no locked
        // encounter address and then keeps the discovered address for fast live reads.
        ["x"] = new(
            "x", "Pokemon X", "citra", 0, 0x104, 6,
            new[]
            {
                new ScanRegion(0x08C50000, 0x18000, "XY battle/work RAM A"),
                new ScanRegion(0x08C80000, 0x18000, "XY battle/work RAM B"),
                new ScanRegion(0x08D00000, 0x18000, "XY battle/work RAM C"),
            }),
        ["y"] = new(
            "y", "Pokemon Y", "citra", 0, 0x104, 6,
            new[]
            {
                new ScanRegion(0x08C50000, 0x18000, "XY battle/work RAM A"),
                new ScanRegion(0x08C80000, 0x18000, "XY battle/work RAM B"),
                new ScanRegion(0x08D00000, 0x18000, "XY battle/work RAM C"),
            }),
        ["or"] = new(
            "or", "Pokemon Omega Ruby", "citra", 0, 0x104, 6,
            new[]
            {
                // Citra Nightly 2104 exposes the useful ORAS process/FCRAM copies
                // in the 0x08Cxxxxx-0x08Dxxxxx area. Scan the full 2 MiB span.
                // The previous narrow windows only found the player's party copy
                // at 0x08C7D4CC/0x08C7D5D0 and missed the live opponent.
                new ScanRegion(0x08C00000, 0x100000, "ORAS FCRAM 08C"),
                new ScanRegion(0x08D00000, 0x100000, "ORAS FCRAM 08D"),
            }),
        ["as"] = new(
            "as", "Pokemon Alpha Sapphire", "citra", 0, 0x104, 6,
            new[]
            {
                new ScanRegion(0x08C00000, 0x100000, "ORAS FCRAM 08C"),
                new ScanRegion(0x08D00000, 0x100000, "ORAS FCRAM 08D"),
            }),

        // RetroArch / GBA Gen 3 opponent party addresses.  These are the live
        // opponent/wild party locations, not the player's party.
        ["emerald"] = new("emerald", "Pokemon Emerald", "retroarch", 0x02024744, 100, 3, Array.Empty<ScanRegion>()),
        ["firered"] = new("firered", "Pokemon FireRed", "retroarch", 0x0202402C, 100, 3, Array.Empty<ScanRegion>()),
        ["leafgreen"] = new("leafgreen", "Pokemon LeafGreen", "retroarch", 0x0202402C, 100, 3, Array.Empty<ScanRegion>()),
        ["ruby"] = new("ruby", "Pokemon Ruby", "retroarch", 0x030045C0, 100, 3, Array.Empty<ScanRegion>()),
        ["sapphire"] = new("sapphire", "Pokemon Sapphire", "retroarch", 0x030045C0, 100, 3, Array.Empty<ScanRegion>()),

        // Gen 4 Nintendo DS games. The live reader supports RetroArch melonDS in
        // two ways: RetroArch READ_CORE_MEMORY when the command interface is
        // available, or direct Windows process-memory discovery when it is not.
        ["diamond"] = new("diamond", "Pokemon Diamond", "melonds", 0, 0x80, 4, Array.Empty<ScanRegion>()),
        ["pearl"] = new("pearl", "Pokemon Pearl", "melonds", 0, 0x80, 4, Array.Empty<ScanRegion>()),
        ["platinum"] = new("platinum", "Pokemon Platinum", "melonds", 0, 0x80, 4, Array.Empty<ScanRegion>()),
        ["heartgold"] = new("heartgold", "Pokemon HeartGold", "melonds", 0, 0x80, 4, Array.Empty<ScanRegion>()),
        ["soulsilver"] = new("soulsilver", "Pokemon SoulSilver", "melonds", 0, 0x80, 4, Array.Empty<ScanRegion>()),
    };

    public static async Task Main(string[] args)
    {
        if (HasArg(args, "--help") || HasArg(args, "-h"))
        {
            PrintHelp();
            return;
        }

        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        if (HasArg(args, "--reset-config") && File.Exists(configPath))
        {
            File.Delete(configPath);
            Console.WriteLine("Saved settings reset.\n");
        }

        var config = LoadConfig(configPath);
        var debug = HasArg(args, "--debug");

        // Command-line values win; otherwise reuse config.json; otherwise use defaults.
        var esp32Arg = Arg(args, "--esp32");
        var esp32 = esp32Arg ?? "http://127.0.0.1:18765"; // Local PocketOS Manager USB-forwarding endpoint
        var intervalMs = ParseInt(Arg(args, "--interval-ms"), config.PollMs > 0 ? config.PollMs : 400, 150);
        var heartbeatMs = ParseInt(Arg(args, "--heartbeat-ms"), config.HeartbeatMs > 0 ? config.HeartbeatMs : 2000, 1000);
        var citraHost = Arg(args, "--citra-host") ?? (!string.IsNullOrWhiteSpace(config.CitraHost) ? config.CitraHost : "127.0.0.1");
        var citraPort = ParseInt(Arg(args, "--citra-port"), config.CitraPort > 0 ? config.CitraPort : 45987, 1);
        var retroHost = Arg(args, "--retro-host") ?? (!string.IsNullOrWhiteSpace(config.RetroHost) ? config.RetroHost : "127.0.0.1");
        var retroPort = ParseInt(Arg(args, "--retro-port"), config.RetroPort > 0 ? config.RetroPort : 55355, 1);
        var modeArg = Arg(args, "--mode");
        var gameArg = Arg(args, "--game");
        var mode = (modeArg ?? "").Trim().ToLowerInvariant();
        var gameKey = (gameArg ?? "").Trim().ToLowerInvariant();
        var saveFolderArg = Arg(args, "--save-folder");
        var saveFolder = saveFolderArg ?? "";

        // Unattended by default. Never ask for save folders or ESP32 IP on launch.
        if (string.IsNullOrWhiteSpace(saveFolder))
            saveFolder = config.SaveFolder ?? "";
        Console.WriteLine("PocketOS Bridge: automatic emulator discovery enabled.");
        Console.WriteLine("Encounter destination: PocketOS Manager on this PC (USB forwarding).");
        Console.WriteLine("Start RetroArch, melonDS, or Citra/Azahar. Ctrl+C to quit.\n");

        FolderDetection? folderDetection = null;
        if (!string.IsNullOrWhiteSpace(saveFolder))
        {
            folderDetection = DetectFromFolder(saveFolder);

            // Never carry a Pokemon X/ORAS/etc. profile into a newly selected
            // save folder. A recognized folder overrides config.json; an
            // unrecognized folder clears the stale game so we ask instead.
            if (string.IsNullOrWhiteSpace(gameArg))
                gameKey = folderDetection.GameKey ?? "";

            if (string.IsNullOrWhiteSpace(modeArg))
            {
                mode = folderDetection.Transport switch
                {
                    "retroarch" => "live-retroarch",
                    "melonds" => "live-melonds",
                    "citra" => "live-citra",
                    _ => ""
                };
            }

            if ((folderDetection.Transport == "retroarch" || folderDetection.Transport == "melonds") && folderDetection.Candidates.Length > 0)
            {
                Console.WriteLine("Pokemon saves found:");
                foreach (var candidate in folderDetection.Candidates.Take(8))
                {
                    var label = Profiles.TryGetValue(candidate.GameKey, out var cp) ? cp.DisplayName : candidate.GameKey;
                    Console.WriteLine($"  {label,-22} {Path.GetFileName(candidate.Path)}  [{candidate.LastWriteUtc.ToLocalTime():g}]");
                }
                Console.WriteLine();
            }
        }

        // If RetroArch's UDP command interface is disabled, Windows still
        // exposes the active core in the main window title (for example
        // "RetroArch melonDS DS 1.4.0" or "RetroArch gpSP ..."). Use that
        // to avoid selecting a GBA save while melonDS is actually running.
        if (string.IsNullOrWhiteSpace(gameArg) && folderDetection is not null && folderDetection.Candidates.Length > 0)
        {
            var coreHint = TryDetectRetroArchCoreFromWindow();
            if (coreHint == "melonds")
            {
                var ds = folderDetection.Candidates
                    .Where(c => Profiles.TryGetValue(c.GameKey, out var p) && p.Transport == "melonds")
                    .OrderByDescending(c => c.LastWriteUtc)
                    .FirstOrDefault();
                if (ds is not null)
                {
                    gameKey = ds.GameKey;
                    mode = "live-melonds";
                    folderDetection = new FolderDetection("melonds", ds.GameKey, ds.Path, "active melonDS core + newest DS save", folderDetection.Candidates);
                    Console.WriteLine($"RetroArch core detected from window: melonDS -> {Profiles[gameKey].DisplayName}");
                    Console.WriteLine();
                }
            }
            else if (coreHint == "gba")
            {
                var gba = folderDetection.Candidates
                    .Where(c => Profiles.TryGetValue(c.GameKey, out var p) && p.Transport == "retroarch")
                    .OrderByDescending(c => c.LastWriteUtc)
                    .FirstOrDefault();
                if (gba is not null)
                {
                    gameKey = gba.GameKey;
                    mode = "live-retroarch";
                    folderDetection = new FolderDetection("retroarch", gba.GameKey, gba.Path, "active GBA core + newest GBA save", folderDetection.Candidates);
                    Console.WriteLine($"RetroArch core detected from window: GBA -> {Profiles[gameKey].DisplayName}");
                    Console.WriteLine();
                }
            }
        }

        // RetroArch can tell us the exact content that is currently running.
        // This is more reliable than looking at every .srm in the save directory.
        if (string.IsNullOrWhiteSpace(gameArg)
            && (mode is "live-retroarch" or "retroarch" or "live-melonds" or "melonds"
                || folderDetection?.Transport is "retroarch" or "melonds"))
        {
            var running = await TryDetectRetroArchCurrentGameAsync(retroHost, retroPort);
            if (!string.IsNullOrWhiteSpace(running.GameKey))
            {
                gameKey = running.GameKey;
                mode = Profiles.TryGetValue(gameKey, out var detectedProfile) && detectedProfile.Transport == "melonds"
                    ? "live-melonds"
                    : "live-retroarch";
                var display = Profiles.TryGetValue(gameKey, out var rp) ? rp.DisplayName : gameKey;
                Console.WriteLine($"RetroArch current game detected: {display}");
                if (!string.IsNullOrWhiteSpace(running.Status))
                    Console.WriteLine($"  {running.Status}");
                Console.WriteLine();
            }
            else if (folderDetection is not null && !string.IsNullOrWhiteSpace(folderDetection.GameKey))
            {
                gameKey = folderDetection.GameKey;
                var display = Profiles.TryGetValue(gameKey, out var fp) ? fp.DisplayName : gameKey;
                Console.WriteLine($"RetroArch status unavailable; using most recently modified save: {display}");
                if (!string.IsNullOrWhiteSpace(folderDetection.SelectedSavePath))
                    Console.WriteLine($"  {Path.GetFileName(folderDetection.SelectedSavePath)}");
                Console.WriteLine();
            }
        }
        else if (string.IsNullOrWhiteSpace(gameArg) && folderDetection is not null && !string.IsNullOrWhiteSpace(folderDetection.GameKey))
        {
            gameKey = folderDetection.GameKey;
        }

        // Do not prompt for a game. Check the running process and RetroArch's
        // current content until a known game is available.
        LiveProfile? profile = null;
        if (!Profiles.TryGetValue(gameKey, out profile))
        {
            Console.WriteLine("Waiting for a supported Pokemon game...");
            while (profile is null)
            {
                var found = await DetectActiveGameAsync(retroHost, retroPort, saveFolder);
                if (found.GameKey is not null && Profiles.TryGetValue(found.GameKey, out profile))
                {
                    gameKey = found.GameKey;
                    mode = profile.Transport switch
                    {
                        "citra" => "live-citra",
                        "melonds" => "live-melonds",
                        _ => "live-retroarch"
                    };
                    Console.WriteLine($"Detected {found.Emulator}: {profile.DisplayName}");
                    break;
                }
                await Task.Delay(2000);
            }
        }
        // Explicit --game overrides auto discovery; profile was validated above.

        if (string.IsNullOrWhiteSpace(mode))
            mode = profile.Transport switch
            {
                "citra" => "live-citra",
                "melonds" => "live-melonds",
                _ => "live-retroarch"
            };

        config.SaveFolder = saveFolder ?? "";
        config.Game = gameKey;
        config.Mode = mode;
        config.Esp32 = esp32;
        config.PollMs = intervalMs;
        config.HeartbeatMs = heartbeatMs;
        config.CitraHost = citraHost;
        config.CitraPort = citraPort;
        config.RetroHost = retroHost;
        config.RetroPort = retroPort;
        SaveConfig(configPath, config);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        // Let the ESP32 know the local bridge is alive as soon as the app starts.
        // The ESP32 keeps its IP on-screen until this dedicated /bridge request
        // arrives, then it plays the opening animation.
        var bridgeConnected = false;
        try
        {
            using var health = await http.GetAsync($"{esp32.TrimEnd('/')}/bridge");
            bridgeConnected = health.IsSuccessStatusCode;
            if (debug)
                Console.WriteLine(bridgeConnected
                    ? "ESP32 bridge connection: OK"
                    : $"ESP32 bridge connection: HTTP {(int)health.StatusCode}");
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"ESP32 bridge connection failed: {ex.Message}");
        }

        // The opening/capture animation is intentionally several seconds long.
        // Give the ESP32 time to finish it before the first encounter POST, so
        // the animation never blocks /state long enough to trigger an HTTP timeout.
        if (bridgeConnected)
            await Task.Delay(6000);
        string? lastFullFingerprint = null;
        var lastHeartbeatAt = DateTimeOffset.MinValue;
        var heartbeatEvery = TimeSpan.FromMilliseconds(heartbeatMs);
        long encounterUpdateId = 0;

        async Task SendIfChanged(EncounterPokemon state)
        {
            var fingerprint = string.Join('|',
                state.Species, state.Level, state.Shiny, state.Nature, state.NatureEffect,
                state.Ability, state.Gender, state.Stars, state.PerfectIVs,
                state.IVHP, state.IVATK, state.IVDEF, state.IVSPA, state.IVSPD, state.IVSPE,
                state.EVHP, state.EVATK, state.EVDEF, state.EVSPA, state.EVSPD, state.EVSPE,
                state.StatHP, state.StatATK, state.StatDEF, state.StatSPA, state.StatSPD, state.StatSPE,
                state.Move1, state.Move2, state.Move3, state.Move4);
            var changed = fingerprint != lastFullFingerprint;
            var now = DateTimeOffset.Now;

            if (!changed)
            {
                if (now - lastHeartbeatAt < heartbeatEvery)
                    return;

                // Tiny keep-alive only. The ESP32 owns its page timer, so a heartbeat
                // cannot reset or redraw Overview/Stats/EVs/Moves.
                try
                {
                    using var response = await http.GetAsync($"{esp32.TrimEnd('/')}/heartbeat");
                    if (!response.IsSuccessStatusCode && debug)
                        Console.WriteLine($"[{DateTime.Now:T}] ESP32 heartbeat HTTP {(int)response.StatusCode}");
                    else if (response.IsSuccessStatusCode)
                        lastHeartbeatAt = now;
                }
                catch (Exception ex)
                {
                    if (debug) Console.WriteLine($"[{DateTime.Now:T}] ESP32 heartbeat failed: {ex.Message}");
                }
                return;
            }

            encounterUpdateId++;
            var payload = new Dictionary<string, object?>
            {
                ["encounterid"] = encounterUpdateId,
                ["source"] = state.Source,
                ["game"] = state.Game,
                ["nickname"] = state.Nickname,
                ["species"] = state.Species,
                ["level"] = state.Level,
                ["shiny"] = state.Shiny,
                ["nature"] = state.Nature,
                ["natureeffect"] = state.NatureEffect,
                ["ability"] = state.Ability,
                ["gender"] = state.Gender,
                ["stars"] = state.Stars,
                ["perfectivs"] = state.PerfectIVs,
                ["ivhp"] = state.IVHP,
                ["ivatk"] = state.IVATK,
                ["ivdef"] = state.IVDEF,
                ["ivspa"] = state.IVSPA,
                ["ivspd"] = state.IVSPD,
                ["ivspe"] = state.IVSPE,
                ["evhp"] = state.EVHP,
                ["evatk"] = state.EVATK,
                ["evdef"] = state.EVDEF,
                ["evspa"] = state.EVSPA,
                ["evspd"] = state.EVSPD,
                ["evspe"] = state.EVSPE,
                ["stathp"] = state.StatHP,
                ["statatk"] = state.StatATK,
                ["statdef"] = state.StatDEF,
                ["statspa"] = state.StatSPA,
                ["statspd"] = state.StatSPD,
                ["statspe"] = state.StatSPE,
                ["move1"] = state.Move1,
                ["move2"] = state.Move2,
                ["move3"] = state.Move3,
                ["move4"] = state.Move4,
            };

            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await http.PostAsync($"{esp32.TrimEnd('/')}/state", content);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[{DateTime.Now:T}] ESP32 update failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    if (debug && !string.IsNullOrWhiteSpace(body))
                        Console.WriteLine($"  ESP32 says: {body.Trim()}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] ESP32 update failed: {ex.Message}");
                return;
            }

            lastFullFingerprint = fingerprint;
            lastHeartbeatAt = now;

            // Normal mode is intentionally quiet: one compact block per new/changed encounter.
            Console.WriteLine($"\nEncounter: {state.Nickname} Lv.{state.Level} | {state.Nature} {state.NatureEffect} | {new string('*', state.Stars)}");
            Console.WriteLine($"Ability {state.Ability} | Gender {state.Gender} | Perfect IVs {state.PerfectIVs}");
            Console.WriteLine($"IV  {state.IVHP}/{state.IVATK}/{state.IVDEF}/{state.IVSPA}/{state.IVSPD}/{state.IVSPE}");
            Console.WriteLine($"EV  {state.EVHP}/{state.EVATK}/{state.EVDEF}/{state.EVSPA}/{state.EVSPD}/{state.EVSPE}");
            Console.WriteLine($"Stats HP {state.StatHP} Atk {state.StatATK} Def {state.StatDEF} SpA {state.StatSPA} SpD {state.StatSPD} Spe {state.StatSPE}");
            Console.WriteLine($"Moves {state.Move1} | {state.Move2} | {state.Move3} | {state.Move4}");
            Console.WriteLine("Sent to ESP32.\n");
        }

        void ResetSendState()
        {
            // Required so an identical species/IV spread encountered later still gets
            // a full /state packet after the OLED has timed out to Waiting.
            lastFullFingerprint = null;
            lastHeartbeatAt = DateTimeOffset.MinValue;
        }

        Console.WriteLine("\nPokemon Encounter Display - LIVE");
        Console.WriteLine($"Game:  {profile.DisplayName}");
        Console.WriteLine($"ESP32: {esp32}");
        Console.WriteLine($"Poll:  {intervalMs} ms | Heartbeat: {heartbeatMs} ms");
        Console.WriteLine($"Config: {configPath}");
        Console.WriteLine(debug ? "Debug logging: ON\n" : "Waiting for encounter...\n");

        if (mode is "live-melonds" or "melonds")
        {
            await RunMelonDSAsync(profile, retroHost, retroPort, intervalMs, SendIfChanged, ResetSendState, debug);
            return;
        }

        if (mode is "live-retroarch" or "retroarch")
        {
            await RunRetroArchAsync(profile, args, retroHost, retroPort, intervalMs, SendIfChanged, ResetSendState, debug);
            return;
        }

        await RunCitraAsync(profile, args, citraHost, citraPort, intervalMs, SendIfChanged, ResetSendState, debug);
    }

    private static async Task RunRetroArchAsync(
        LiveProfile profile,
        string[] args,
        string host,
        int port,
        int intervalMs,
        Func<EncounterPokemon, Task> send,
        Action resetSend,
        bool debug)
    {
        var address = ParseHexOrDefault(Arg(args, "--address"), profile.EncounterAddress);
        Console.WriteLine("Mode: LIVE RetroArch encounter reader");
        Console.WriteLine($"Enemy/wild address: 0x{address:X8}");
        Console.WriteLine("RetroArch: Settings > Network > Network Commands = ON");
        Console.WriteLine("Works with Gen 3 Ruby/Sapphire/Emerald/FireRed/LeafGreen profiles. Ctrl+C to stop.\n");

        using var reader = new RetroArchReader(host, port);
        string? lastRawToken = null;
        EncounterPokemon? cachedState = null;
        var waitingShown = false;

        while (true)
        {
            try
            {
                var raw = await reader.ReadMemoryAsync(address, profile.PokemonSize);
                var rawToken = Convert.ToHexString(raw);

                // Always poll RAM, but only decode/recalculate when the enemy bytes change.
                if (!string.Equals(rawToken, lastRawToken, StringComparison.Ordinal))
                {
                    var pk = new PK3(raw);
                    cachedState = IsPlausible(pk, 386)
                        ? ToEncounterPokemon(pk, "RetroArch LIVE", profile.DisplayName)
                        : null;
                    lastRawToken = rawToken;
                }

                if (cachedState is not null)
                {
                    waitingShown = false;
                    await send(cachedState);
                }
                else if (!waitingShown)
                {
                    resetSend();
                    Console.WriteLine("Waiting for encounter...");
                    waitingShown = true;
                }
            }
            catch (Exception ex)
            {
                if (debug) Console.WriteLine($"[{DateTime.Now:T}] RetroArch read failed: {ex.Message}");
            }
            await Task.Delay(intervalMs);
        }
    }

    private static async Task RunMelonDSAsync(
        LiveProfile profile,
        string host,
        int port,
        int intervalMs,
        Func<EncounterPokemon, Task> send,
        Action resetSend,
        bool debug)
    {
        Console.WriteLine("Mode: LIVE RetroArch melonDS Gen 4 encounter reader");
        Console.WriteLine($"Game: {profile.DisplayName}");
        Console.WriteLine("Gen 4 battle structs: 0x80 bytes; player first, opponent second in a standard 1v1 battle.");
        Console.WriteLine("Trying RetroArch core memory first; Windows process-memory fallback is automatic if Network Commands are unavailable.\n");

        RetroArchReader? coreReader = null;
        WindowsProcessMemoryReader? processReader = null;
        bool coreAvailable = false;

        try
        {
            coreReader = new RetroArchReader(host, port);
            var status = await coreReader.GetStatusAsync();
            coreAvailable = !string.IsNullOrWhiteSpace(status);
            if (debug && coreAvailable) Console.WriteLine($"RetroArch command interface: {status}");
        }
        catch (Exception ex)
        {
            coreReader?.Dispose();
            coreReader = null;
            if (debug) Console.WriteLine($"RetroArch command interface unavailable: {ex.Message}");
        }

        if (!coreAvailable && OperatingSystem.IsWindows())
        {
            processReader = WindowsProcessMemoryReader.TryOpenRetroArch();
            if (processReader is not null)
                Console.WriteLine($"Network Commands unavailable. Reading RetroArch process memory directly (PID {processReader.ProcessId}).");
            else
                Console.WriteLine("Network Commands unavailable and RetroArch process could not be opened yet. Keep RetroArch running; the bridge will retry.");
        }

        uint? cachedCoreAddress = null;
        ulong? cachedHostAddress = null;
        string? lastRawToken = null;
        EncounterPokemon? cachedState = null;
        bool waitingShown = false;
        var nextSearchAt = DateTimeOffset.MinValue;

        while (true)
        {
            try
            {
                byte[]? raw = null;

                if (cachedCoreAddress.HasValue && coreReader is not null)
                {
                    raw = await coreReader.ReadMemoryAsync(cachedCoreAddress.Value, 0x80);
                    if (!TryParseGen4BattleMon(raw, out _))
                    {
                        cachedCoreAddress = null;
                        raw = null;
                        cachedState = null;
                        lastRawToken = null;
                        resetSend();
                    }
                }
                else if (cachedHostAddress.HasValue && processReader is not null)
                {
                    raw = processReader.Read(cachedHostAddress.Value, 0x80);
                    if (raw is null || !TryParseGen4BattleMon(raw, out _))
                    {
                        cachedHostAddress = null;
                        raw = null;
                        cachedState = null;
                        lastRawToken = null;
                        resetSend();
                    }
                }

                if (raw is null && DateTimeOffset.Now >= nextSearchAt)
                {
                    nextSearchAt = DateTimeOffset.Now.AddSeconds(1.5);

                    if (coreAvailable && coreReader is not null)
                    {
                        var found = await FindGen4OpponentViaRetroArchAsync(coreReader, debug);
                        if (found is not null)
                        {
                            cachedCoreAddress = found.Value.Address;
                            raw = found.Value.Raw;
                            Console.WriteLine($"Gen 4 opponent found in DS RAM at 0x{cachedCoreAddress.Value:X8}.");
                        }
                        else if (OperatingSystem.IsWindows())
                        {
                            // GET_STATUS can work even when the active core does not expose
                            // a usable system memory map. In that case, fall back to scanning
                            // the RetroArch process instead of waiting forever.
                            if (processReader is null || !processReader.IsAlive)
                            {
                                processReader?.Dispose();
                                processReader = WindowsProcessMemoryReader.TryOpenRetroArch();
                            }

                            if (processReader is not null)
                            {
                                var hostFound = processReader.FindGen4Opponent(debug);
                                if (hostFound is not null)
                                {
                                    cachedHostAddress = hostFound.Value.Address;
                                    raw = hostFound.Value.Raw;
                                    Console.WriteLine($"Gen 4 opponent found via Windows process-memory fallback at 0x{cachedHostAddress.Value:X}.");
                                }
                            }
                        }
                    }
                    else
                    {
                        if (processReader is null || !processReader.IsAlive)
                        {
                            processReader?.Dispose();
                            processReader = WindowsProcessMemoryReader.TryOpenRetroArch();
                        }

                        if (processReader is not null)
                        {
                            var found = processReader.FindGen4Opponent(debug);
                            if (found is not null)
                            {
                                cachedHostAddress = found.Value.Address;
                                raw = found.Value.Raw;
                                Console.WriteLine($"Gen 4 opponent found in RetroArch process memory at 0x{cachedHostAddress.Value:X}.");
                            }
                        }
                    }
                }

                if (raw is not null && TryParseGen4BattleMon(raw, out var mon))
                {
                    var token = Convert.ToHexString(raw);
                    if (!string.Equals(token, lastRawToken, StringComparison.Ordinal))
                    {
                        cachedState = ToEncounterPokemon(mon, "RetroArch melonDS LIVE", profile.DisplayName);
                        lastRawToken = token;
                    }

                    waitingShown = false;
                    if (cachedState is not null)
                        await send(cachedState);
                }
                else if (!waitingShown)
                {
                    resetSend();
                    Console.WriteLine("Waiting for Gen 4 wild/opponent Pokemon... scanning RAM");
                    waitingShown = true;
                }
            }
            catch (Exception ex)
            {
                if (debug) Console.WriteLine($"[{DateTime.Now:T}] melonDS read failed: {ex.Message}");

                // If the command interface disappears, switch to Windows process
                // memory on the next pass instead of falling back to a stale Citra profile.
                if (coreAvailable)
                {
                    coreAvailable = false;
                    coreReader?.Dispose();
                    coreReader = null;
                    if (OperatingSystem.IsWindows())
                        processReader ??= WindowsProcessMemoryReader.TryOpenRetroArch();
                    cachedCoreAddress = null;
                }
            }

            await Task.Delay(intervalMs);
        }
    }

    private static async Task<(uint Address, byte[] Raw)?> FindGen4OpponentViaRetroArchAsync(RetroArchReader reader, bool debug)
    {
        // Project Pokemon's Gen 4 RAM research places the in-battle structures
        // at RAM-dump offsets roughly 0x002C0BC2-0x002D5780. RetroArch cores
        // expose this in one of two common ways:
        //   READ_CORE_MEMORY : NDS system addresses (0x02000000 + RAM offset)
        //   READ_CORE_RAM    : zero-based core RAM / achievement addresses
        // Try both. This is important for melonDS builds whose GET_STATUS works
        // but whose system-memory descriptor is incomplete.
        var system = await ScanGen4BattleWindowAsync(
            (a, n) => reader.ReadMemoryAsync(a, n),
            0x022C0000, 0x022D6000,
            "READ_CORE_MEMORY/system", debug);
        if (system is not null)
            return system;

        var coreRam = await ScanGen4BattleWindowAsync(
            (a, n) => reader.ReadCoreRamAsync(a, n),
            0x002C0000, 0x002D6000,
            "READ_CORE_RAM/offset", debug);
        if (coreRam is not null)
            return coreRam;

        return null;
    }

    private static async Task<(uint Address, byte[] Raw)?> ScanGen4BattleWindowAsync(
        Func<uint, int, Task<byte[]>> read,
        uint start,
        uint end,
        string label,
        bool debug)
    {
        const int chunkSize = 0x3000; // keep UDP reply comfortably below 64 KiB
        const int overlap = 0x180;
        var candidates = new SortedDictionary<uint, byte[]>();
        var firstRead = true;

        for (uint address = start; address < end; address += (uint)(chunkSize - overlap))
        {
            var size = (int)Math.Min((uint)chunkSize, end - address);
            byte[] data;
            try
            {
                data = await read(address, size);
                firstRead = false;
            }
            catch (Exception ex)
            {
                // If the first address in this contiguous RAM window is not
                // readable, this addressing mode is not usable for this core.
                if (debug || firstRead)
                    Console.WriteLine($"Gen 4 {label} unavailable at 0x{address:X8}: {ex.Message}");
                if (firstRead) break;
                continue;
            }

            foreach (var offset in FindAllGen4BattleOffsets(data))
            {
                var absolute = address + (uint)offset;
                if (!candidates.ContainsKey(absolute))
                    candidates[absolute] = data.AsSpan(offset, 0x80).ToArray();
                Console.WriteLine($"GEN4 STRUCT FOUND @ 0x{absolute:X8}");
            }
        }

        if (debug)
        {
            Console.WriteLine($"Gen 4 {label}: {candidates.Count} valid battle structure(s) in documented RAM window.");
            foreach (var kv in candidates.Take(8))
            {
                if (TryParseGen4BattleMon(kv.Value, out var mon))
                    Console.WriteLine($"  0x{kv.Key:X8}  {SpeciesName(mon.Species)} Lv.{mon.Level}");
            }
        }

        // The documented order for a normal 1-v-1 battle is player first,
        // opponent second. Do not require them to be exactly 0x80 apart: the
        // research only guarantees appearance order, not adjacency.
        if (candidates.Count > 0)
        {
            foreach (var candidate in candidates)
            {
                if (TryParseGen4BattleMon(candidate.Value, out var mon))
                {
                    Console.WriteLine(
                        $"Gen 4 battle candidate: {SpeciesName(mon.Species)} Lv.{mon.Level} @ 0x{candidate.Key:X8}");

                    // Prefer non-empty opponent candidates.
                    // Wild Pokemon usually have low/zero EVs.
                    if (mon.Species > 0)
                    {
                        return (candidate.Key, candidate.Value);
                    }
                }
            }
        }

        return null;
    }

    private static List<int> FindAllGen4BattleOffsets(ReadOnlySpan<byte> data)
    {
        var found = new SortedSet<int>();

        // Neutral stat stages are normally eight 0x06 bytes at +0x18. Search
        // for at least six consecutive values so Intimidate/Download and other
        // immediate battle effects do not hide an otherwise valid structure.
        for (var run = 0; run + 6 <= data.Length; run++)
        {
            if (data[run] != 0x06 || data[run + 1] != 0x06 || data[run + 2] != 0x06
                || data[run + 3] != 0x06 || data[run + 4] != 0x06 || data[run + 5] != 0x06)
                continue;

            for (var stageOffset = 0x18; stageOffset <= 0x1A; stageOffset++)
            {
                var start = run - stageOffset;
                if (start < 0 || start + 0x80 > data.Length || (start & 1) != 0)
                    continue;
                if (TryParseGen4BattleMon(data.Slice(start, 0x80), out _))
                    found.Add(start);
            }
        }

        return found.ToList();
    }

    private static int FindGen4OpponentOffset(ReadOnlySpan<byte> data)
    {
        var candidates = FindAllGen4BattleOffsets(data);
        return candidates.Count >= 2 ? candidates[1] : -1;
    }

    private static bool LooksLikeGen4BattleMon(ReadOnlySpan<byte> data)
        => TryParseGen4BattleMon(data, out _);

    private static bool TryParseGen4BattleMon(ReadOnlySpan<byte> data, out Gen4BattleMon mon)
    {
        mon = null!;
        if (data.Length < 0x80) return false;

        var species = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x00, 2));
        if (species is < 1 or > 493) return false;

        var attack = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x02, 2));
        var defense = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x04, 2));
        var speed = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x06, 2));
        var spa = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x08, 2));
        var spd = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0A, 2));
        if (attack is 0 or > 999 || defense is 0 or > 999 || speed is 0 or > 999 || spa is 0 or > 999 || spd is 0 or > 999)
            return false;

        var move1 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0C, 2));
        var move2 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0E, 2));
        var move3 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x10, 2));
        var move4 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x12, 2));
        var moves = new[] { move1, move2, move3, move4 };
        if (!moves.Any(m => m is > 0 and <= 467) || moves.Any(m => m > 467)) return false;

        // Stat-stage bytes are normally 06 at neutral and remain in the 00-0C range.
        var stages = data.Slice(0x18, 8);
        if (stages.ToArray().Any(x => x > 0x0C)) return false;
        if (stages.ToArray().Count(x => x == 0x06) < 5) return false;

        var level = data[0x34];
        if (level is < 1 or > 100) return false;

        var currentHP = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x4C, 2));
        var maxHP = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x50, 2));
        if (maxHP is 0 or > 999 || currentHP > maxHP) return false;

        var gender = data[0x7E];
        if (gender > 2) return false;

        var packedIV = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x14, 4));
        var ivHP = (int)(packedIV & 31);
        var ivATK = (int)((packedIV >> 5) & 31);
        var ivDEF = (int)((packedIV >> 10) & 31);
        var ivSPE = (int)((packedIV >> 15) & 31);
        var ivSPA = (int)((packedIV >> 20) & 31);
        var ivSPD = (int)((packedIV >> 25) & 31);

        var pid = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x68, 4));
        var tid = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x74, 2));
        var sid = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x76, 2));
        var ability = data[0x27];

        mon = new Gen4BattleMon(
            species, level, ability, gender, pid, tid, sid,
            currentHP, maxHP, attack, defense, speed, spa, spd,
            ivHP, ivATK, ivDEF, ivSPA, ivSPD, ivSPE,
            move1, move2, move3, move4,
            data.Slice(0, 0x80).ToArray());
        return true;
    }

    private static EncounterPokemon ToEncounterPokemon(Gen4BattleMon mon, string source, string game)
    {
        var natureIndex = (int)(mon.PID % 25);
        var nature = Gen4NatureNames[natureIndex];
        var ivs = new[] { mon.IVHP, mon.IVATK, mon.IVDEF, mon.IVSPA, mon.IVSPD, mon.IVSPE };
        var perfectIVs = ivs.Count(x => x == 31);
        var stars = RateIVs(ivs.Sum(), perfectIVs);
        var shinyValue = (mon.TID ^ mon.SID ^ (ushort)(mon.PID & 0xFFFF) ^ (ushort)(mon.PID >> 16));
        var shiny = shinyValue < 8;
        var gender = mon.Gender switch { 0 => "Male", 1 => "Female", 2 => "Genderless", _ => "Unknown" };

        return new EncounterPokemon(
            source, game, SpeciesName(mon.Species), mon.Species, mon.Level, shiny,
            nature, NatureEffect(nature), AbilityName(mon.Ability), gender, stars, perfectIVs,
            mon.IVHP, mon.IVATK, mon.IVDEF, mon.IVSPA, mon.IVSPD, mon.IVSPE,
            0, 0, 0, 0, 0, 0, // fresh wild Pokemon have no EV training; battle struct does not expose EVs
            mon.MaxHP, mon.Attack, mon.Defense, mon.SpAttack, mon.SpDefense, mon.Speed,
            MoveName(mon.Move1), MoveName(mon.Move2), MoveName(mon.Move3), MoveName(mon.Move4),
            DateTimeOffset.Now);
    }

    private static readonly string[] Gen4NatureNames =
    {
        "Hardy", "Lonely", "Brave", "Adamant", "Naughty",
        "Bold", "Docile", "Relaxed", "Impish", "Lax",
        "Timid", "Hasty", "Serious", "Jolly", "Naive",
        "Modest", "Mild", "Quiet", "Bashful", "Rash",
        "Calm", "Gentle", "Sassy", "Careful", "Quirky"
    };

    private static async Task RunCitraAsync(
        LiveProfile profile,
        string[] args,
        string host,
        int port,
        int intervalMs,
        Func<EncounterPokemon, Task> send,
        Action resetSend,
        bool debug)
    {
        var forcedAddressText = Arg(args, "--address");
        var forcedAddress = string.IsNullOrWhiteSpace(forcedAddressText)
            ? (uint?)null
            : ParseHexOrDefault(forcedAddressText, profile.EncounterAddress);

        Console.WriteLine("Mode: LIVE Citra/Azahar encounter reader");
        Console.WriteLine($"RPC: {host}:{port}");
        if (forcedAddress.HasValue)
            Console.WriteLine($"Forced PK6 data address: 0x{forcedAddress.Value:X8}");
        else
        {
            var enemyBase = Gen6EnemyRosterBase(profile);
            if (enemyBase != 0)
            {
                Console.WriteLine($"Direct enemy roster: 0x{enemyBase:X8}, stride 0x1E4, 6 slots");
                Console.WriteLine("Reading the same enemy-Pokemon roster used by Gen 6 in-battle stat tools.");
            }
        }
        Console.WriteLine("Azahar/Citra RPC server must be enabled. Ctrl+C to stop.");
        Console.WriteLine("The bridge no longer guesses the opponent by scanning the player's party/FCRAM copies.\n");

        using var reader = new CitraRpcReader(host, port);
        Gen6EnemySnapshot? cachedEnemy = null;
        EncounterPokemon? cachedState = null;
        string? forcedToken = null;
        var waitingShown = false;

        while (true)
        {
            try
            {
                if (forcedAddress.HasValue)
                {
                    // Manual mode still caches the decoded Pokemon; only rebuild the
                    // EncounterPokemon when the 0xE8/0x104 bytes actually change.
                    var raw = await reader.ReadMemoryAsync(forcedAddress.Value, profile.PokemonSize);
                    var token = Convert.ToHexString(raw);
                    if (!string.Equals(token, forcedToken, StringComparison.Ordinal))
                    {
                        var decoded = DecodeBestPk6(raw, 721);
                        cachedState = decoded.Pokemon is not null && IsStrongEncounterCandidate(decoded.Pokemon, 721)
                            ? ToEncounterPokemon(decoded.Pokemon, "Citra/Azahar LIVE", profile.DisplayName)
                            : null;
                        forcedToken = token;
                    }
                }
                else
                {
                    var snapshot = await ReadGen6EnemyRosterAsync(reader, profile, cachedEnemy, debug);
                    if (snapshot is null)
                    {
                        cachedEnemy = null;
                        cachedState = null;
                    }
                    else
                    {
                        var changed = cachedEnemy is null
                            || snapshot.Address != cachedEnemy.Address
                            || !string.Equals(snapshot.Token, cachedEnemy.Token, StringComparison.Ordinal);

                        cachedEnemy = snapshot;
                        if (changed || cachedState is null)
                        {
                            cachedState = ToEncounterPokemon(snapshot.Pokemon, "Citra/Azahar LIVE", profile.DisplayName);
                            if (debug)
                                Console.WriteLine($"[{DateTime.Now:T}] enemy slot {snapshot.Slot + 1} @ 0x{snapshot.Address:X8}, decode={snapshot.Score}");
                        }
                    }
                }

                if (cachedState is not null)
                {
                    waitingShown = false;
                    await send(cachedState);
                }
                else if (!waitingShown)
                {
                    resetSend();
                    Console.WriteLine("Waiting for encounter...");
                    waitingShown = true;
                }
            }
            catch (Exception ex)
            {
                if (debug) Console.WriteLine($"[{DateTime.Now:T}] Citra/Azahar read failed: {ex.Message}");
            }

            await Task.Delay(intervalMs);
        }
    }

    private static uint Gen6EnemyRosterBase(LiveProfile profile) => profile.Key switch
    {
        // Gen6CTRPluginFramework's in-battle enemy viewer reads six PK6 records
        // from these fixed enemy-roster bases, separated by 0x1E4 bytes.
        // AutoGameSet(first, second) = XY, ORAS in that project.
        "x" or "y" => 0x081FF744u,
        "or" or "as" => 0x081FEEC8u,
        _ => 0u,
    };

    private static async Task<Gen6EnemySnapshot?> ReadGen6EnemyRosterAsync(
        CitraRpcReader reader,
        LiveProfile profile,
        Gen6EnemySnapshot? cached,
        bool debug)
    {
        const uint stride = 0x1E4;
        const int enemyCoreSize = 0xE8;
        var baseAddress = Gen6EnemyRosterBase(profile);
        if (baseAddress == 0)
            return null;

        // Fast path: eight bytes are enough to identify the same encrypted PK6
        // (encryption constant + checksum/header). If unchanged, reuse the fully
        // decoded Pokemon and all calculated values from the previous poll.
        if (cached is not null)
        {
            try
            {
                var header = await reader.ReadMemoryAsync(cached.Address, 8);
                var token = Convert.ToHexString(header);
                if (string.Equals(token, cached.Token, StringComparison.Ordinal))
                    return cached;
            }
            catch
            {
                // Fall through to a full six-slot probe.
            }
        }

        var valid = new List<Gen6EnemySnapshot>();
        for (var slot = 0; slot < 6; slot++)
        {
            var address = baseAddress + (uint)slot * stride;
            try
            {
                var raw = await reader.ReadMemoryAsync(address, enemyCoreSize);
                var decoded = DecodeEncryptedPk6Core(raw, 721);
                if (decoded.Pokemon is null)
                    continue;

                var pk = decoded.Pokemon;
                var species = GetInt(pk, "Species");
                var level = PokemonLevel(pk);
                if (species is < 1 or > 721 || level is < 1 or > 100 || decoded.Score < 12)
                    continue;

                var token = Convert.ToHexString(raw.AsSpan(0, 8));
                valid.Add(new Gen6EnemySnapshot(pk, address, slot, decoded.Score, raw, token));
            }
            catch
            {
                // Empty/unmapped slot.
            }
        }

        if (valid.Count == 0)
            return null;

        var best = valid.OrderBy(x => x.Slot).ThenByDescending(x => x.Score).First();
        if (debug && valid.Count > 1)
        {
            Console.WriteLine("Direct enemy roster slots:");
            foreach (var c in valid)
            {
                var species = GetInt(c.Pokemon, "Species");
                Console.WriteLine($"  slot {c.Slot + 1}: {SpeciesName(species)} Lv.{PokemonLevel(c.Pokemon)} score={c.Score}");
            }
        }
        return best;
    }

    private static async Task<Gen6AccessorCandidate?> FindOpponentAccessorAsync(
        CitraRpcReader reader,
        LiveProfile profile,
        int expectedSpecies,
        int expectedLevel)
    {
        const int blockSize = 0x1000;
        const int descriptorSize = 0x10;
        var descriptors = new List<(uint Address, uint Vtable, uint PartyFlag, uint DataPointer, bool DataEncrypted, bool EncryptOnExit, string RegionLabel)>();

        foreach (var region in profile.ScanRegions)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Scanning Accessors in {region.Label}...");
            for (var offset = 0; offset < region.Length; offset += blockSize)
            {
                var size = Math.Min(blockSize, region.Length - offset);
                var baseAddress = region.Start + (uint)offset;
                byte[] block;
                try
                {
                    block = await reader.ReadMemoryAsync(baseAddress, size);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i + descriptorSize <= block.Length; i += 4)
                {
                    var span = block.AsSpan(i, descriptorSize);
                    var vtable = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
                    var partyFlag = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
                    var dataPointer = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4));
                    var dataEncrypted = span[0xC];
                    var encryptOnExit = span[0xD];

                    if (!LooksLikeGen6Accessor(vtable, partyFlag, dataPointer, dataEncrypted, encryptOnExit))
                        continue;

                    descriptors.Add((
                        baseAddress + (uint)i,
                        vtable,
                        partyFlag,
                        dataPointer,
                        dataEncrypted != 0,
                        encryptOnExit != 0,
                        region.Label));
                }
            }
        }

        descriptors = descriptors
            .GroupBy(x => x.Address)
            .Select(g => g.First())
            .ToList();

        var opponentDescriptors = descriptors
            .Where(x => x.PartyFlag == 0)
            .Where(x => !IsKnownPersistentGen6Range(profile, x.DataPointer))
            .GroupBy(x => new { x.Address, x.DataPointer })
            .Select(g => g.First())
            .ToList();

        var partyDescriptorCount = descriptors.Count(x => x.PartyFlag == 1);
        Console.WriteLine($"  Accessor patterns: {descriptors.Count} total, {opponentDescriptors.Count} partyFlag=0, {partyDescriptorCount} partyFlag=1, {descriptors.Select(x => x.Vtable).Distinct().Count()} vtables.");

        if (opponentDescriptors.Count == 0)
            return null;

        // Vtables that appear on both party and non-party Accessors are much more likely
        // to be the real pml::pokepara::Accessor class than a random 16-byte RAM pattern.
        var vtableStats = descriptors
            .GroupBy(x => x.Vtable)
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Count(), Party: g.Count(x => x.PartyFlag == 1), NonParty: g.Count(x => x.PartyFlag == 0)));

        var candidates = new List<Gen6AccessorCandidate>();
        var seenPointers = new HashSet<uint>();

        // Test the strongest structural descriptors first. This avoids dereferencing
        // thousands of random pointer-like patterns found in zero-heavy FCRAM.
        var ordered = opponentDescriptors
            .OrderByDescending(x => vtableStats[x.Vtable].Party > 0)
            .ThenByDescending(x => vtableStats[x.Vtable].Total)
            .ThenBy(x => x.Address)
            .Take(512)
            .ToList();

        foreach (var d in ordered)
        {
            if (!seenPointers.Add(d.DataPointer))
                continue;

            try
            {
                var raw = await reader.ReadMemoryAsync(d.DataPointer, profile.PokemonSize);
                var decoded = DecodeBestPk6(raw, 721);
                if (decoded.Pokemon is null || decoded.Score < 12)
                    continue;

                var pk = decoded.Pokemon;
                var level = PokemonLevel(pk);
                if (level is < 1 or > 100)
                    continue;

                var rank = decoded.Score * 10;
                var stats = vtableStats[d.Vtable];
                if (stats.Party > 0 && stats.NonParty > 0) rank += 120;
                else if (stats.Total >= 2) rank += 30;

                // In battle the opponent Accessor is often allocated near one or more
                // party Accessors using the same vtable. Reward that relationship; it is
                // much harder for a random RAM pattern to satisfy than pointer shape alone.
                var nearestPartyDistance = descriptors
                    .Where(x => x.PartyFlag == 1 && x.Vtable == d.Vtable)
                    .Select(x => x.Address > d.Address ? x.Address - d.Address : d.Address - x.Address)
                    .DefaultIfEmpty(uint.MaxValue)
                    .Min();
                if (nearestPartyDistance <= 0x400) rank += 160;
                else if (nearestPartyDistance <= 0x2000) rank += 100;
                else if (nearestPartyDistance <= 0x10000) rank += 50;

                // Accessor +0xC tells whether its Pokemon data is currently encrypted.
                // Reward a decode representation that agrees with the object state.
                if (d.DataEncrypted == decoded.DecryptedFromEncrypted) rank += 80;
                else rank -= 15;

                if (TotalEV(pk) == 0) rank += 30;
                if (expectedSpecies > 0)
                    rank += WildHintScore(pk, expectedSpecies, expectedLevel);

                candidates.Add(new Gen6AccessorCandidate(
                    d.Address,
                    d.Vtable,
                    d.PartyFlag,
                    d.DataPointer,
                    d.DataEncrypted,
                    d.EncryptOnExit,
                    pk,
                    decoded.Score,
                    rank,
                    d.RegionLabel,
                    raw,
                    decoded.DecryptedFromEncrypted));
            }
            catch
            {
                // Pointer was stale/unmapped or did not contain a PK6.
            }
        }

        if (candidates.Count == 0)
        {
            Console.WriteLine("  No partyFlag=0 Accessor target decoded as a valid PK6 yet.");
            return null;
        }

        candidates = candidates
            .OrderByDescending(c => expectedSpecies > 0 && MatchesWildHint(c.Pokemon, expectedSpecies, expectedLevel))
            .ThenByDescending(c => c.RankScore)
            .ThenByDescending(c => c.DecodeScore)
            .ToList();

        Console.WriteLine("  Opponent Accessors:");
        foreach (var c in candidates.Take(8))
        {
            var species = GetInt(c.Pokemon, "Species");
            var level = PokemonLevel(c.Pokemon);
            var match = expectedSpecies <= 0 ? "n/a" : (MatchesWildHint(c.Pokemon, expectedSpecies, expectedLevel) ? "MATCH" : "no");
            Console.WriteLine(
                $"    acc 0x{c.AccessorAddress:X8} -> data 0x{c.DataPointer:X8} " +
                $"party={c.PartyFlag} enc={(c.DataEncrypted ? 1 : 0)} {SpeciesName(species)} Lv.{level} " +
                $"decode={c.DecodeScore} rank={c.RankScore} target={match} " +
                $"format={(c.DecryptedFromEncrypted ? "encrypted->decrypted" : "plain")}");
        }

        if (expectedSpecies > 0)
        {
            var exact = candidates.FirstOrDefault(c => MatchesWildHint(c.Pokemon, expectedSpecies, expectedLevel));
            if (exact is null)
            {
                Console.WriteLine($"  Accessors decoded, but none match the wild-slot target {SpeciesName(expectedSpecies)} Lv.{expectedLevel}; refusing to guess.");
                return null;
            }
            return exact;
        }

        // Without a hint, require a strong decode and a plausible Accessor class vtable.
        // This is intentionally stricter than the old raw-PK6 scanner.
        var best = candidates.FirstOrDefault(c =>
        {
            var stats = vtableStats[c.Vtable];
            // Without an external hint, require the same Accessor vtable to be seen in
            // both party and non-party forms. This is our strongest structural proof that
            // the 14-byte object really is pml::pokepara::Accessor rather than random RAM.
            return c.DecodeScore >= 16 && stats.Party > 0 && stats.NonParty > 0;
        });

        if (best is null)
        {
            Console.WriteLine("  Valid PK6 pointers were found, but no Accessor class was strong enough to trust without a species/level hint.");
            return null;
        }

        return best;
    }

    private static async Task<(PK6? Pokemon, uint DataPointer)> ReadOpponentFromAccessorAsync(
        CitraRpcReader reader,
        LiveProfile profile,
        uint accessorAddress,
        int expectedSpecies,
        int expectedLevel)
    {
        try
        {
            var descriptor = await reader.ReadMemoryAsync(accessorAddress, 0x10);
            if (descriptor.Length < 0x10)
                return (null, 0);

            var vtable = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(0, 4));
            var partyFlag = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4, 4));
            var dataPointer = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(8, 4));
            var encrypted = descriptor[0xC];
            var encryptOnExit = descriptor[0xD];

            if (!LooksLikeGen6Accessor(vtable, partyFlag, dataPointer, encrypted, encryptOnExit) || partyFlag != 0)
                return (null, dataPointer);
            if (IsKnownPersistentGen6Range(profile, dataPointer))
                return (null, dataPointer);

            var raw = await reader.ReadMemoryAsync(dataPointer, profile.PokemonSize);
            var decoded = DecodeBestPk6(raw, 721);
            if (decoded.Pokemon is null || decoded.Score < 12)
                return (null, dataPointer);

            if (expectedSpecies > 0 && !MatchesWildHint(decoded.Pokemon, expectedSpecies, expectedLevel))
                return (null, dataPointer);

            return (decoded.Pokemon, dataPointer);
        }
        catch
        {
            return (null, 0);
        }
    }

    private static bool LooksLikeGen6Accessor(uint vtable, uint partyFlag, uint dataPointer, byte dataEncrypted, byte encryptOnExit)
    {
        if (partyFlag > 1)
            return false;
        if (dataEncrypted > 1 || encryptOnExit > 1)
            return false;
        if ((dataPointer & 3) != 0 || dataPointer < 0x08000000u || dataPointer >= 0x20000000u)
            return false;
        if ((vtable & 3) != 0 || vtable < 0x00100000u || vtable >= 0x20000000u)
            return false;
        return true;
    }

    private static async Task<(int Species, int Level, bool Valid, uint Address)> ProbeGen6WildSlotAsync(CitraRpcReader reader, LiveProfile profile)
    {
        // These legacy CTR plugin offsets can expose a pre-encounter species/level
        // slot on some builds, but Citra Nightly 2104 may not map them the same way.
        // Treat this only as an optional hint; never assume it is available.
        var address = profile.Key switch
        {
            "or" or "as" => 0x008C861Cu,
            "x" or "y" => 0x008D6300u,
            _ => 0u,
        };

        if (address == 0)
            return (0, 0, false, 0);

        try
        {
            var raw = await reader.ReadMemoryAsync(address, 4);
            if (raw.Length < 3)
                return (0, 0, false, address);

            var species = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0, 2));
            var level = raw[2];
            var valid = species is > 0 and <= 721 && level is > 0 and <= 100;
            return (species, level, valid, address);
        }
        catch
        {
            return (0, 0, false, address);
        }
    }

    private static int PokemonLevel(PKM pk)
    {
        // Party-format PK6 exposes Stat_Level, but non-party/core PK6 data can leave
        // that field at zero. CurrentLevel is derivable from EXP and works for both.
        var statLevel = GetInt(pk, "Stat_Level");
        if (statLevel is >= 1 and <= 100)
            return statLevel;
        var currentLevel = GetInt(pk, "CurrentLevel");
        return currentLevel is >= 1 and <= 100 ? currentLevel : statLevel;
    }

    private static bool MatchesWildHint(PKM pk, int expectedSpecies, int expectedLevel)
    {
        if (expectedSpecies <= 0)
            return false;

        var species = GetInt(pk, "Species");
        if (species != expectedSpecies)
            return false;

        if (expectedLevel <= 0)
            return true;

        var level = PokemonLevel(pk);
        return level == expectedLevel;
    }

    private static int WildHintScore(PKM pk, int expectedSpecies, int expectedLevel)
    {
        if (expectedSpecies <= 0)
            return 0;

        var species = GetInt(pk, "Species");
        var level = PokemonLevel(pk);
        if (species == expectedSpecies && (expectedLevel <= 0 || level == expectedLevel))
            return 10000;
        if (species == expectedSpecies)
            return 3000;
        if (expectedLevel > 0 && level == expectedLevel)
            return 100;
        return -500;
    }

    private static async Task<(uint Address, PK6? Pokemon, int Score, string RegionLabel, uint WindowAddress, int WindowSize, int WindowOffset, byte[]? Raw, bool StrictFallback)> FindEncounterPk6Async(
        CitraRpcReader reader,
        LiveProfile profile,
        int expectedSpecies,
        int expectedLevel)
    {
        const int blockSize = 0x1000;
        const int overlap = 0x104;
        var candidates = new List<ScanCandidate>();

        foreach (var region in profile.ScanRegions)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Scanning {region.Label}...");

            for (var offset = 0; offset < region.Length; offset += blockSize - overlap)
            {
                var size = Math.Min(blockSize, region.Length - offset);
                var baseAddress = region.Start + (uint)offset;
                byte[] block;

                try
                {
                    block = await reader.ReadMemoryAsync(baseAddress, size);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i + profile.PokemonSize <= block.Length; i += 4)
                {
                    try
                    {
                        var bytes = block.AsSpan(i, profile.PokemonSize).ToArray();
                        var decoded = DecodeBestPk6(bytes, 721);
                        if (decoded.Pokemon is null || decoded.Score < 12) continue;

                        candidates.Add(new ScanCandidate(
                            baseAddress + (uint)i,
                            decoded.Pokemon,
                            decoded.Score,
                            region.Label,
                            baseAddress,
                            size,
                            i,
                            bytes,
                            decoded.DecryptedFromEncrypted));
                    }
                    catch
                    {
                        // Not a PK6 candidate.
                    }
                }
            }
        }

        if (candidates.Count == 0)
            return (0, null, int.MinValue, "", 0, 0, 0, null, false);

        // Overlapping scan windows can discover the same address more than once.
        // Keep a single copy before ranking possible opponent Pokemon.
        candidates = candidates
            .GroupBy(c => c.Address)
            .Select(g => g.OrderByDescending(c => c.Score).First())
            .ToList();

        var ranked = candidates
            .Select(c => new
            {
                Candidate = c,
                OpponentScore = OpponentCandidateScore(c, candidates) + WildHintScore(c.Pokemon, expectedSpecies, expectedLevel),
                TargetMatch = MatchesWildHint(c.Pokemon, expectedSpecies, expectedLevel),
                EvTotal = TotalEV(c.Pokemon),
                PartyCluster = PartyClusterSize(c, candidates),
                IdentityCopies = IdentityCopies(c, candidates),
                Persistent = IsKnownPersistentGen6Range(profile, c.Address),
            })
            .OrderByDescending(x => x.TargetMatch)
            .ThenByDescending(x => x.OpponentScore)
            .ThenByDescending(x => x.Candidate.Score)
            .ThenBy(x => x.PartyCluster)
            .ThenBy(x => x.EvTotal)
            .ToList();

        Console.WriteLine("  Top opponent candidates:");
        foreach (var x in ranked.Take(6))
        {
            var species = GetInt(x.Candidate.Pokemon, "Species");
            var level = PokemonLevel(x.Candidate.Pokemon);
            Console.WriteLine(
                $"    0x{x.Candidate.Address:X8}  {SpeciesName(species)} Lv.{level}  " +
                $"EVsum={x.EvTotal} base={x.Candidate.Score} opp={x.OpponentScore} " +
                $"partyCluster={x.PartyCluster} copies={x.IdentityCopies} " +
                $"persistent={(x.Persistent ? "yes" : "no")} " +
                $"target={(expectedSpecies <= 0 ? "n/a" : (x.TargetMatch ? "MATCH" : "no"))} " +
                $"strict={(IsStrictUnhintedCandidate(profile, x.Candidate, x.EvTotal, x.PartyCluster) ? "yes" : "no")} " +
                $"format={(x.Candidate.DecryptedFromEncrypted ? "encrypted->decrypted" : "plain")}");
        }

        var strictFallback = false;
        if (expectedSpecies > 0)
        {
            var exact = ranked.FirstOrDefault(x => x.TargetMatch);
            if (exact is null)
            {
                Console.WriteLine($"  No PK6 candidate matches wild-slot target {SpeciesName(expectedSpecies)} Lv.{expectedLevel}; ignoring party/mirror candidates.");
                return (0, null, int.MinValue, "", 0, 0, 0, null, false);
            }
            ranked = ranked.Where(x => x.TargetMatch).Concat(ranked.Where(x => !x.TargetMatch)).ToList();
        }
        else
        {
            // Without a reliable species/level hint, never fall back to a normal party
            // record. Only accept a very strong, isolated, zero-EV PK6 outside known
            // persistent Battle Box/PC storage. This specifically rejects the user's
            // contiguous Lv.15 Poochyena/Torchic party records seen in Citra Nightly 2104.
            var strict = ranked
                .Where(x => IsStrictUnhintedCandidate(profile, x.Candidate, x.EvTotal, x.PartyCluster))
                .ToList();

            if (strict.Count == 0)
            {
                Console.WriteLine("  No strict isolated opponent PK6 found. Refusing to send the player's party as the encounter.");
                return (0, null, int.MinValue, "", 0, 0, 0, null, false);
            }

            var bestStrict = strict[0];
            // If several unrelated persistent records all look equally plausible, do not
            // guess. A live encounter should stand out after the persistent ranges and
            // party clusters are removed.
            if (strict.Count > 1 && bestStrict.OpponentScore - strict[1].OpponentScore < 20)
            {
                Console.WriteLine($"  {strict.Count} strict candidates remain with no clear winner. Refusing to guess; scanning again soon.");
                foreach (var x in strict.Take(4))
                    Console.WriteLine($"    strict 0x{x.Candidate.Address:X8} {SpeciesName(GetInt(x.Candidate.Pokemon, "Species"))} Lv.{PokemonLevel(x.Candidate.Pokemon)} opp={x.OpponentScore}");
                return (0, null, int.MinValue, "", 0, 0, 0, null, false);
            }

            ranked = strict.Concat(ranked.Where(x => !strict.Contains(x))).ToList();
            strictFallback = true;
        }

        var best = ranked[0];
        var c = best.Candidate;
        Console.WriteLine(
            $"  Selected opponent candidate: {SpeciesName(GetInt(c.Pokemon, "Species"))} " +
            $"Lv.{PokemonLevel(c.Pokemon)} at 0x{c.Address:X8} " +
            $"(opponent score {best.OpponentScore}, " +
            $"{(c.DecryptedFromEncrypted ? "encrypted PK6" : "plain PK6")}, " +
            $"{(strictFallback ? "strict no-hint fallback" : "wild-hint match")}).");

        return (c.Address, c.Pokemon, c.Score, c.RegionLabel, c.WindowAddress, c.WindowSize, c.WindowOffset, c.Raw, strictFallback);
    }


    private static (PK6? Pokemon, int Score, bool DecryptedFromEncrypted) DecodeEncryptedPk6Core(byte[] raw, int maxSpecies)
    {
        // Gen 6 enemy-roster entries are EK6 records. The original CTR plugin reads
        // exactly 232 (0xE8) bytes and decrypts them before inspecting species/EXP.
        // Keeping the buffer at 0xE8 also prevents unrelated battle bytes from being
        // interpreted by PKHeX as party-only stats such as Stat_Level.
        if (raw.Length < 0xE8)
            return (null, int.MinValue, true);

        try
        {
            var core = raw.AsSpan(0, 0xE8).ToArray();
            PokeCrypto.Decrypt67(core.AsSpan());
            var pk = new PK6(core);
            var score = EncounterCandidateScore(pk, maxSpecies);
            return score == int.MinValue
                ? (null, score, true)
                : (pk, score, true);
        }
        catch
        {
            return (null, int.MinValue, true);
        }
    }

    private static (PK6? Pokemon, int Score, bool DecryptedFromEncrypted) DecodeBestPk6(byte[] raw, int maxSpecies)
    {
        PK6? best = null;
        var bestScore = int.MinValue;
        var bestWasEncrypted = false;

        // Some Gen 6 RAM copies are already decrypted (the player's party often is),
        // while wild/opponent copies can be stored in encrypted EK6/party form.
        // Try both representations and keep only the structurally stronger decode.
        try
        {
            var plain = new PK6(raw.ToArray());
            var score = EncounterCandidateScore(plain, maxSpecies);
            if (score > bestScore)
            {
                best = plain;
                bestScore = score;
                bestWasEncrypted = false;
            }
        }
        catch { }

        try
        {
            var decryptedBytes = raw.ToArray();
            PokeCrypto.Decrypt67(decryptedBytes.AsSpan());
            var decrypted = new PK6(decryptedBytes);
            var score = EncounterCandidateScore(decrypted, maxSpecies);
            if (score > bestScore)
            {
                best = decrypted;
                bestScore = score;
                bestWasEncrypted = true;
            }
        }
        catch { }

        return (best, bestScore, bestWasEncrypted);
    }

    private static bool IsStrictUnhintedPk6(PKM pk)
    {
        // In this build a checksum-valid zero-EV PK6 scores at least 19. Requiring
        // that strength prevents random RAM and the old all-zero Sudowoodo false hit.
        return EncounterCandidateScore(pk, 721) >= 19 && TotalEV(pk) == 0;
    }

    private static bool IsStrictUnhintedCandidate(LiveProfile profile, ScanCandidate candidate, int evTotal, int partyCluster)
    {
        if (evTotal != 0 || partyCluster != 1 || candidate.Score < 19)
            return false;
        if (IsKnownPersistentGen6Range(profile, candidate.Address))
            return false;
        return IsStrictUnhintedPk6(candidate.Pokemon);
    }

    private static bool IsKnownPersistentGen6Range(LiveProfile profile, uint address)
    {
        // Known NTR/FCRAM storage ranges. They contain perfectly valid PK6 records,
        // but they are not the current battle opponent and must never win an unhinted
        // scan merely because their checksums are valid.
        if (profile.Key is "or" or "as")
        {
            // ORAS Battle Box around 0x08C72330.
            if (address >= 0x08C72330u && address < 0x08C73330u)
                return true;
            // ORAS PC boxes start at 0x08C9E134; reserve enough room for all boxes.
            if (address >= 0x08C9E134u && address < 0x08CD4000u)
                return true;
        }
        else if (profile.Key is "x" or "y")
        {
            // XY Battle Box around 0x08C6AC2C.
            if (address >= 0x08C6AC2Cu && address < 0x08C6BC2Cu)
                return true;
            // XY PC boxes start at 0x08C861C8.
            if (address >= 0x08C861C8u && address < 0x08CBD000u)
                return true;
        }
        return false;
    }

    private static int OpponentCandidateScore(ScanCandidate candidate, IReadOnlyList<ScanCandidate> all)
    {
        // The old scanner simply chose the first highest-validity PK6 it found. In ORAS
        // that frequently lands on a copy of the player's party Pokemon (exactly what
        // happened with the user's Lv.15 Poochyena). Rank by battle-opponent traits too.
        var score = candidate.Score * 10;
        var evTotal = TotalEV(candidate.Pokemon);

        // Fresh wild Pokemon normally have no trained EVs. Trainer opponents also tend
        // to have low/zero EVs, while a player's active party often has earned some.
        if (evTotal == 0)
            score += 80;
        else
            score -= Math.Min(60, evTotal * 3);

        // A player's party is normally stored as a run of 0x104-byte PK6 records.
        // Penalize candidates that sit in such a contiguous party cluster. The live
        // opponent copy is generally isolated from that persistent party array.
        var cluster = PartyClusterSize(candidate, all);
        if (cluster >= 2)
            score -= 90 + Math.Min(60, (cluster - 2) * 15);

        // Persistent party/box copies can occur multiple times in the scanned work RAM.
        // A current opponent tends to have fewer duplicate copies.
        var copies = IdentityCopies(candidate, all);
        if (copies > 1)
            score -= Math.Min(45, (copies - 1) * 10);

        // The A region is the first ORAS/XY live work-RAM area we scan and is preferred
        // slightly over later fallback regions containing more persistent save-like data.
        if (candidate.RegionLabel.EndsWith(" A", StringComparison.OrdinalIgnoreCase))
            score += 10;

        return score;
    }

    private static int TotalEV(PKM pk)
        => GetInt(pk, "EV_HP") + GetInt(pk, "EV_ATK") + GetInt(pk, "EV_DEF") +
           GetInt(pk, "EV_SPA") + GetInt(pk, "EV_SPD") + GetInt(pk, "EV_SPE");

    private static int IdentityCopies(ScanCandidate candidate, IReadOnlyList<ScanCandidate> all)
    {
        var identity = CandidateIdentity(candidate.Pokemon);
        return all.Count(x => CandidateIdentity(x.Pokemon) == identity);
    }

    private static int PartyClusterSize(ScanCandidate candidate, IReadOnlyList<ScanCandidate> all)
    {
        const uint stride = 0x104;
        var addresses = all.Select(x => x.Address).ToHashSet();
        var count = 1;

        for (uint n = 1; n <= 5; n++)
        {
            var delta = stride * n;
            if (candidate.Address >= delta && addresses.Contains(candidate.Address - delta)) count++;
            if (addresses.Contains(candidate.Address + delta)) count++;
        }
        return count;
    }

    private static string CandidateIdentity(PKM pk)
    {
        return string.Join(":", new[]
        {
            GetInt(pk, "Species"),
            PokemonLevel(pk),
            GetInt(pk, "PID", "EncryptionConstant"),
            GetInt(pk, "Move1"),
            GetInt(pk, "Move2"),
            GetInt(pk, "Move3"),
            GetInt(pk, "Move4")
        });
    }

    private static void PrintCandidateDiagnostic(PKM pk, uint address, int score)
    {
        var species = GetInt(pk, "Species");
        var level = PokemonLevel(pk);
        var nature = GetObject(pk, "Nature")?.ToString() ?? "Unknown";
        var iv = new[]
        {
            GetInt(pk, "IV_HP"), GetInt(pk, "IV_ATK"), GetInt(pk, "IV_DEF"),
            GetInt(pk, "IV_SPA"), GetInt(pk, "IV_SPD"), GetInt(pk, "IV_SPE")
        };
        var ev = new[]
        {
            GetInt(pk, "EV_HP"), GetInt(pk, "EV_ATK"), GetInt(pk, "EV_DEF"),
            GetInt(pk, "EV_SPA"), GetInt(pk, "EV_SPD"), GetInt(pk, "EV_SPE")
        };
        var moves = new[]
        {
            MoveName(GetInt(pk, "Move1")), MoveName(GetInt(pk, "Move2")),
            MoveName(GetInt(pk, "Move3")), MoveName(GetInt(pk, "Move4"))
        };

        var checksumText = TryGetBoolProperty(pk, "ChecksumValid", out var checksumValid)
            ? checksumValid.ToString()
            : "n/a";
        Console.WriteLine($"  Candidate decode: {SpeciesName(species)} (#{species}) Lv.{level} | Nature {nature} | score {score} | checksum={checksumText}");
        Console.WriteLine($"  IV: {string.Join("/", iv)}  EV: {string.Join("/", ev)}");
        Console.WriteLine($"  Moves: {string.Join(" | ", moves)}");
        Console.WriteLine($"  Verify this matches the Pokemon visible in battle before trusting IV/Nature data.");
    }

    private static void SaveCandidateDiagnostic(
        LiveProfile profile, uint address, int score, string regionLabel, byte[] raw, PKM pk)
    {
        try
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "diagnostics");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var baseName = $"{profile.Key}_{stamp}_0x{address:X8}";
            var binPath = Path.Combine(dir, baseName + ".bin");
            var txtPath = Path.Combine(dir, baseName + ".txt");
            File.WriteAllBytes(binPath, raw);

            var species = GetInt(pk, "Species");
            var level = PokemonLevel(pk);
            var nature = GetObject(pk, "Nature")?.ToString() ?? "Unknown";
            var iv = new[]
            {
                GetInt(pk, "IV_HP"), GetInt(pk, "IV_ATK"), GetInt(pk, "IV_DEF"),
                GetInt(pk, "IV_SPA"), GetInt(pk, "IV_SPD"), GetInt(pk, "IV_SPE")
            };
            var ev = new[]
            {
                GetInt(pk, "EV_HP"), GetInt(pk, "EV_ATK"), GetInt(pk, "EV_DEF"),
                GetInt(pk, "EV_SPA"), GetInt(pk, "EV_SPD"), GetInt(pk, "EV_SPE")
            };
            var moves = new[]
            {
                MoveName(GetInt(pk, "Move1")), MoveName(GetInt(pk, "Move2")),
                MoveName(GetInt(pk, "Move3")), MoveName(GetInt(pk, "Move4"))
            };

            var report = new StringBuilder();
            report.AppendLine("Pokemon Partner - live encounter RAM diagnostic");
            report.AppendLine($"Captured: {DateTimeOffset.Now:O}");
            report.AppendLine($"Game: {profile.DisplayName}");
            report.AppendLine($"Region: {regionLabel}");
            report.AppendLine($"Address: 0x{address:X8}");
            report.AppendLine($"Candidate score: {score}");
            report.AppendLine($"Species: {SpeciesName(species)} (#{species})");
            report.AppendLine($"Level: {level}");
            report.AppendLine($"Nature: {nature}");
            report.AppendLine($"Shiny: {GetBool(pk, "IsShiny")}");
            report.AppendLine($"IVs HP/Atk/Def/SpA/SpD/Spe: {string.Join("/", iv)}");
            report.AppendLine($"EVs HP/Atk/Def/SpA/SpD/Spe: {string.Join("/", ev)}");
            report.AppendLine($"Moves: {string.Join(" | ", moves)}");
            report.AppendLine();
            report.AppendLine("Raw PK6 candidate (hex):");
            report.AppendLine(Convert.ToHexString(raw));
            File.WriteAllText(txtPath, report.ToString());

            Console.WriteLine($"  Diagnostic saved: {txtPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Could not save diagnostic capture: {ex.Message}");
        }
    }

    private static bool IsStrongEncounterCandidate(PKM pk, int maxSpecies)
        => EncounterCandidateScore(pk, maxSpecies) >= 10;

    private static int EncounterCandidateScore(PKM pk, int maxSpecies)
    {
        var species = GetInt(pk, "Species");
        if (species < 1 || species > maxSpecies) return int.MinValue;

        var level = PokemonLevel(pk);
        if (level is < 1 or > 100) return int.MinValue;

        var moves = new[] { GetInt(pk, "Move1"), GetInt(pk, "Move2"), GetInt(pk, "Move3"), GetInt(pk, "Move4") };
        // A real Gen 6 battle Pokemon has at least one usable move. The previous
        // loose scanner accepted random RAM as a Lv.2 Sudowoodo because all-zero
        // move/IV fields still happened to fit numeric ranges.
        var moveCount = moves.Count(m => m is > 0 and < 1000);
        if (moveCount == 0) return int.MinValue;

        var ivs = new[]
        {
            GetInt(pk, "IV_HP"), GetInt(pk, "IV_ATK"), GetInt(pk, "IV_DEF"),
            GetInt(pk, "IV_SPA"), GetInt(pk, "IV_SPD"), GetInt(pk, "IV_SPE")
        };
        if (!ivs.All(v => v is >= 0 and <= 31)) return int.MinValue;

        var evs = new[]
        {
            GetInt(pk, "EV_HP"), GetInt(pk, "EV_ATK"), GetInt(pk, "EV_DEF"),
            GetInt(pk, "EV_SPA"), GetInt(pk, "EV_SPD"), GetInt(pk, "EV_SPE")
        };
        if (!evs.All(v => v is >= 0 and <= 252) || evs.Sum() > 510) return int.MinValue;

        var score = 8;
        score += Math.Min(4, moveCount);

        // PKHeX exposes checksum/validity properties on PK6. Treat them as strong
        // evidence when present, but keep reflection fallback compatibility across
        // PKHeX.Core versions.
        if (TryGetBoolProperty(pk, "ChecksumValid", out var checksumValid))
        {
            if (!checksumValid) return int.MinValue;
            score += 8;
        }
        else if (TryGetBoolProperty(pk, "Valid", out var valid))
        {
            if (!valid) return int.MinValue;
            score += 6;
        }

        // Wild encounters normally start at zero EVs. This is a ranking bonus,
        // not a hard requirement, so trainer opponents remain possible.
        if (evs.Sum() == 0) score += 3;

        // All-zero IVs are technically possible, but when combined with otherwise
        // weak battle data they are a common false-positive signature.
        if (ivs.All(v => v == 0)) score -= 3;

        return score;
    }

    private static bool TryGetBoolProperty(object obj, string name, out bool value)
    {
        value = false;
        var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p is null) return false;
        var raw = p.GetValue(obj);
        if (raw is bool b)
        {
            value = b;
            return true;
        }
        try
        {
            value = Convert.ToBoolean(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static EncounterPokemon ToEncounterPokemon(PKM pk, string source, string game)
    {
        var species = GetInt(pk, "Species");
        var level = PokemonLevel(pk);
        var nickname = GetString(pk, "Nickname");
        if (string.IsNullOrWhiteSpace(nickname)) nickname = SpeciesName(species);

        var natureValue = GetObject(pk, "Nature");
        var nature = natureValue?.ToString() ?? "Unknown";

        var ivHP = GetInt(pk, "IV_HP");
        var ivATK = GetInt(pk, "IV_ATK");
        var ivDEF = GetInt(pk, "IV_DEF");
        var ivSPA = GetInt(pk, "IV_SPA");
        var ivSPD = GetInt(pk, "IV_SPD");
        var ivSPE = GetInt(pk, "IV_SPE");

        var evHP = GetInt(pk, "EV_HP");
        var evATK = GetInt(pk, "EV_ATK");
        var evDEF = GetInt(pk, "EV_DEF");
        var evSPA = GetInt(pk, "EV_SPA");
        var evSPD = GetInt(pk, "EV_SPD");
        var evSPE = GetInt(pk, "EV_SPE");

        var ivValues = new[] { ivHP, ivATK, ivDEF, ivSPA, ivSPD, ivSPE };
        var totalIV = ivValues.Sum();
        var perfectIVs = ivValues.Count(x => x == 31);
        var stars = RateIVs(totalIV, perfectIVs);
        var natureEffect = NatureEffect(nature);
        var ability = AbilityName(pk);
        var gender = GenderName(pk);

        var stats = CalculateStats(pk);

        return new EncounterPokemon(
            source, game, nickname, species, level, GetBool(pk, "IsShiny"), nature,
            natureEffect, ability, gender, stars, perfectIVs,
            ivHP, ivATK, ivDEF, ivSPA, ivSPD, ivSPE,
            evHP, evATK, evDEF, evSPA, evSPD, evSPE,
            stats.HP, stats.ATK, stats.DEF, stats.SPA, stats.SPD, stats.SPE,
            MoveName(GetInt(pk, "Move1")),
            MoveName(GetInt(pk, "Move2")),
            MoveName(GetInt(pk, "Move3")),
            MoveName(GetInt(pk, "Move4")),
            DateTimeOffset.Now);
    }

    private static int RateIVs(int totalIV, int perfectIVs)
    {
        // Fast 1-4 star encounter rating. Exact IVs stay visible on the OLED,
        // so stars are only a quick quality signal rather than a replacement.
        if (totalIV >= 150 || perfectIVs >= 5) return 4;
        if (totalIV >= 120 || perfectIVs >= 3) return 3;
        if (totalIV >= 90 || perfectIVs >= 1) return 2;
        return 1;
    }

    private static string NatureEffect(string nature) => nature.ToLowerInvariant() switch
    {
        "lonely" => "+Atk -Def",
        "brave" => "+Atk -Spe",
        "adamant" => "+Atk -SpA",
        "naughty" => "+Atk -SpD",
        "bold" => "+Def -Atk",
        "relaxed" => "+Def -Spe",
        "impish" => "+Def -SpA",
        "lax" => "+Def -SpD",
        "timid" => "+Spe -Atk",
        "hasty" => "+Spe -Def",
        "jolly" => "+Spe -SpA",
        "naive" => "+Spe -SpD",
        "modest" => "+SpA -Atk",
        "mild" => "+SpA -Def",
        "quiet" => "+SpA -Spe",
        "rash" => "+SpA -SpD",
        "calm" => "+SpD -Atk",
        "gentle" => "+SpD -Def",
        "sassy" => "+SpD -Spe",
        "careful" => "+SpD -SpA",
        "hardy" or "docile" or "serious" or "bashful" or "quirky" => "Neutral",
        _ => ""
    };

    private static string GenderName(PKM pk)
    {
        var gender = GetInt(pk, "Gender");
        return gender switch
        {
            0 => "Male",
            1 => "Female",
            2 => "Genderless",
            _ => "Unknown"
        };
    }

    private static string AbilityName(int ability)
    {
        if (ability <= 0) return "Unknown";
        try
        {
            var assembly = typeof(PKM).Assembly;
            var gameInfoType = assembly.GetType("PKHeX.Core.GameInfo");
            var stringsProp = gameInfoType?.GetProperty("Strings", BindingFlags.Public | BindingFlags.Static);
            var strings = stringsProp?.GetValue(null);
            if (strings is not null)
            {
                var type = strings.GetType();
                var member = type.GetProperty("abilitylist", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                          ?? type.GetProperty("Ability", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var value = member?.GetValue(strings);
                if (value is string[] names && ability < names.Length && !string.IsNullOrWhiteSpace(names[ability]))
                    return names[ability];
            }
        }
        catch { }
        return $"Ability #{ability}";
    }

    private static string AbilityName(PKM pk)
    {
        var ability = GetInt(pk, "Ability");
        return AbilityName(ability);
    }

    private static (int HP, int ATK, int DEF, int SPA, int SPD, int SPE) CalculateStats(PKM pk)
    {
        try
        {
            // PKHeX returns battle stats in H/A/B/S/C/D order:
            // HP, Attack, Defense, Speed, Sp. Attack, Sp. Defense.
            var values = pk.GetStats(pk.PersonalInfo);
            if (values.Length >= 6)
                return (values[0], values[1], values[2], values[4], values[5], values[3]);
        }
        catch
        {
            // Keep the live reader running even if a malformed transient battle
            // structure cannot provide personal/base-stat information yet.
        }
        return (0, 0, 0, 0, 0, 0);
    }

    private static bool IsPlausible(PKM pk, int maxSpecies)
    {
        var species = GetInt(pk, "Species");
        if (species < 1 || species > maxSpecies) return false;
        var level = PokemonLevel(pk);
        if (level is < 1 or > 100) return false;
        return true;
    }

    private static string SpeciesName(int species)
    {
        if (species <= 0) return "Unknown";
        try
        {
            var assembly = typeof(PKM).Assembly;
            var gameInfoType = assembly.GetType("PKHeX.Core.GameInfo");
            var stringsProp = gameInfoType?.GetProperty("Strings", BindingFlags.Public | BindingFlags.Static);
            var strings = stringsProp?.GetValue(null);
            if (strings is not null)
            {
                var type = strings.GetType();
                var member = type.GetProperty("specieslist", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                          ?? type.GetProperty("Species", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var value = member?.GetValue(strings);
                if (value is string[] names && species < names.Length && !string.IsNullOrWhiteSpace(names[species]))
                    return names[species];
            }
        }
        catch { }
        return $"Species #{species}";
    }

    private static string MoveName(int move)
    {
        if (move <= 0) return "-";
        try
        {
            var assembly = typeof(PKM).Assembly;
            var gameInfoType = assembly.GetType("PKHeX.Core.GameInfo");
            var stringsProp = gameInfoType?.GetProperty("Strings", BindingFlags.Public | BindingFlags.Static);
            var strings = stringsProp?.GetValue(null);
            if (strings is not null)
            {
                var type = strings.GetType();
                var member = type.GetProperty("movelist", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                          ?? type.GetProperty("Move", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var value = member?.GetValue(strings);
                if (value is string[] names && move < names.Length && !string.IsNullOrWhiteSpace(names[move]))
                    return names[move];
            }
        }
        catch { }
        return $"Move #{move}";
    }

    private static int GetInt(object obj, params string[] names)
    {
        foreach (var name in names)
        {
            var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p is null) continue;
            var value = p.GetValue(obj);
            if (value is null) continue;
            try { return Convert.ToInt32(value); } catch { }
        }
        return 0;
    }

    private static bool GetBool(object obj, string name, bool defaultValue = false)
    {
        var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p is null) return defaultValue;
        var value = p.GetValue(obj);
        if (value is bool b) return b;
        try { return Convert.ToBoolean(value); } catch { return defaultValue; }
    }

    private static string GetString(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return p?.GetValue(obj)?.ToString() ?? "";
    }

    private static object? GetObject(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return p?.GetValue(obj);
    }

    private static FolderDetection DetectFromFolder(string folder)
    {
        var fullText = folder.ToLowerInvariant();
        var candidates = new List<SaveCandidate>();

        try
        {
            if (Directory.Exists(folder))
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Take(1000))
                {
                    var ext = Path.GetExtension(path);
                    if (!ext.Equals(".srm", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".sav", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".dsv", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var key = DetectGameKeyFromText(Path.GetFileNameWithoutExtension(path));
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    DateTime modified;
                    try { modified = File.GetLastWriteTimeUtc(path); }
                    catch { modified = DateTime.MinValue; }
                    candidates.Add(new SaveCandidate(path, key, modified));
                }
            }
        }
        catch
        {
            // Fall back to path/title-ID detection below.
        }

        if (candidates.Count > 0)
        {
            var ordered = candidates
                .OrderByDescending(x => x.LastWriteUtc)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var selected = ordered[0];
            var selectedTransport = Profiles.TryGetValue(selected.GameKey, out var selectedProfile)
                ? selectedProfile.Transport
                : "retroarch";
            return new FolderDetection(
                selectedTransport,
                selected.GameKey,
                selected.Path,
                "most recently modified recognized save",
                ordered);
        }

        // Citra/Azahar title IDs and path names.
        if (fullText.Contains("0004000000055d00")) return new FolderDetection("citra", "x", "", "Citra title ID", Array.Empty<SaveCandidate>());
        if (fullText.Contains("0004000000055e00")) return new FolderDetection("citra", "y", "", "Citra title ID", Array.Empty<SaveCandidate>());
        if (fullText.Contains("000400000011c400")) return new FolderDetection("citra", "or", "", "Citra title ID", Array.Empty<SaveCandidate>());
        if (fullText.Contains("000400000011c500")) return new FolderDetection("citra", "as", "", "Citra title ID", Array.Empty<SaveCandidate>());

        var folderKey = DetectGameKeyFromText(fullText);
        var transport = folderKey switch
        {
            "emerald" or "firered" or "leafgreen" or "ruby" or "sapphire" => "retroarch",
            "diamond" or "pearl" or "platinum" or "heartgold" or "soulsilver" => "melonds",
            "x" or "y" or "or" or "as" => "citra",
            _ => ""
        };
        return new FolderDetection(transport, folderKey, "", "folder-name detection", Array.Empty<SaveCandidate>());
    }

    private static string DetectGameKeyFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.ToLowerInvariant();

        if (t.Contains("alpha sapphire")) return "as";
        if (t.Contains("omega ruby")) return "or";
        if (t.Contains("heartgold") || t.Contains("heart gold")) return "heartgold";
        if (t.Contains("soulsilver") || t.Contains("soul silver")) return "soulsilver";
        if (t.Contains("platinum")) return "platinum";
        if (t.Contains("diamond")) return "diamond";
        if (t.Contains("pearl")) return "pearl";
        if (t.Contains("leafgreen") || t.Contains("leaf green")) return "leafgreen";
        if (t.Contains("firered") || t.Contains("fire red")) return "firered";
        if (t.Contains("emerald")) return "emerald";
        if (t.Contains("sapphire")) return "sapphire";
        if (t.Contains("ruby")) return "ruby";
        if (t.Contains("pokemon x") || t.EndsWith(" x") || t == "x") return "x";
        if (t.Contains("pokemon y") || t.EndsWith(" y") || t == "y") return "y";
        return "";
    }

    private static async Task<(string? GameKey, string Emulator)> DetectActiveGameAsync(
        string retroHost, int retroPort, string saveFolder)
    {
        // Prefer the actual loaded RetroArch content over stale save files.
        var retro = await TryDetectRetroArchCurrentGameAsync(retroHost, retroPort);
        if (!string.IsNullOrWhiteSpace(retro.GameKey))
            return (retro.GameKey, "RetroArch");

        if (!OperatingSystem.IsWindows()) return (null, "");
        bool emulatorPresent = false;
        string emulatorName = "";
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    string name = process.ProcessName.ToLowerInvariant();
                    bool isRetro = name.Contains("retroarch");
                    bool isDs = name.Contains("melonds");
                    bool isCitra = name.Contains("citra") || name.Contains("azahar") || name.Contains("lime3ds");
                    if (!isRetro && !isDs && !isCitra) continue;
                    emulatorPresent = true;
                    emulatorName = isCitra ? "Citra / Azahar" : isDs ? "melonDS" : "RetroArch";
                    string title = process.MainWindowTitle ?? "";
                    var key = DetectGameKeyFromText(title);
                    if (!string.IsNullOrEmpty(key))
                        return (key, emulatorName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Emulator scan note: {ex.Message}");
        }

        // Saves alone are not sufficient proof of what is running. Only use a
        // candidate when a matching live emulator is already present.
        if (emulatorPresent && !string.IsNullOrWhiteSpace(saveFolder))
        {
            var detection = DetectFromFolder(saveFolder);
            if (!string.IsNullOrEmpty(detection.GameKey)
                && Profiles.TryGetValue(detection.GameKey, out var candidate)
                && (emulatorName == "Citra / Azahar" ? candidate.Transport == "citra"
                    : emulatorName == "melonDS" ? candidate.Transport == "melonds"
                    : candidate.Transport is "melonds" or "retroarch"))
                return (detection.GameKey, emulatorName + " (save-folder hint)");
        }
        return (null, emulatorName);
    }

    private static string TryDetectRetroArchCoreFromWindow()
    {
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            foreach (var process in Process.GetProcessesByName("retroarch"))
            {
                using (process)
                {
                    var title = process.MainWindowTitle?.ToLowerInvariant() ?? "";
                    if (title.Contains("melonds")) return "melonds";
                    if (title.Contains("gpsp") || title.Contains("mgba") || title.Contains("vba")) return "gba";
                }
            }
        }
        catch { }
        return "";
    }

    private static async Task<(string GameKey, string Status)> TryDetectRetroArchCurrentGameAsync(string host, int port)
    {
        try
        {
            using var reader = new RetroArchReader(host, port);
            var status = await reader.GetStatusAsync();
            if (string.IsNullOrWhiteSpace(status) || status.Contains("CONTENTLESS", StringComparison.OrdinalIgnoreCase))
                return ("", status);

            // GET_STATUS returns: GET_STATUS PLAYING system_id,game_basename,crc32=XXXXXXXX
            // Detect from the basename first, then from the full response as a fallback.
            var firstComma = status.IndexOf(',');
            var secondComma = firstComma >= 0 ? status.IndexOf(',', firstComma + 1) : -1;
            var basename = firstComma >= 0
                ? status.Substring(firstComma + 1, (secondComma > firstComma ? secondComma : status.Length) - firstComma - 1)
                : status;

            var key = DetectGameKeyFromText(basename);
            if (string.IsNullOrWhiteSpace(key))
                key = DetectGameKeyFromText(status);
            return (key, status);
        }
        catch
        {
            return ("", "");
        }
    }

    private static BridgeConfig LoadConfig(string path)
    {
        try
        {
            if (!File.Exists(path)) return new BridgeConfig();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BridgeConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BridgeConfig();
        }
        catch
        {
            return new BridgeConfig();
        }
    }

    private static void SaveConfig(string path, BridgeConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not save config.json: {ex.Message}");
        }
    }

    private static bool HasArg(string[] args, string name)
        => args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static string ReadPathFromConsole()
    {
        var s = (Console.ReadLine() ?? "").Trim();
        return s.Trim('"');
    }

    private static int ParseInt(string? value, int fallback, int min)
        => int.TryParse(value, out var x) ? Math.Max(min, x) : fallback;

    private static uint ParseHexOrDefault(string? value, uint fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        return uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var parsed) ? parsed : fallback;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Pokemon Encounter Display - LIVE");
        Console.WriteLine("\nNormal launches ask for the save folder and ESP32 IP; press Enter to reuse saved values.");
        Console.WriteLine("Use --reset-config to forget saved settings and --debug for detailed RAM logs.");
        Console.WriteLine("Optional tuning: --interval-ms 400 --heartbeat-ms 2000");
        Console.WriteLine("\nCitra/Azahar ORAS:");
        Console.WriteLine("  dotnet run -- --mode live-citra --game or --esp32 http://192.168.68.52");
        Console.WriteLine("  dotnet run -- --mode live-citra --game as --esp32 http://192.168.68.52");
        Console.WriteLine("\nManual exact Gen 6 encounter address:");
        Console.WriteLine("  dotnet run -- --mode live-citra --game or --address 0xXXXXXXXX --esp32 http://192.168.68.52");
        Console.WriteLine("\nRetroArch Gen 3:");
        Console.WriteLine("  dotnet run -- --mode live-retroarch --game emerald --esp32 http://192.168.68.52");
        Console.WriteLine("  dotnet run -- --mode live-melonds --game heartgold --esp32 http://192.168.68.52");
        Console.WriteLine("  Games: ruby, sapphire, emerald, firered, leafgreen");
    }
}

internal sealed class CitraRpcReader : IDisposable
{
    private const uint Version = 1;
    private const uint ReadMemory = 1;
    private const int HeaderSize = 16;
    private const int MaxChunk = 32;

    private readonly UdpClient udp;
    private uint requestId = 1;

    public CitraRpcReader(string host, int port)
    {
        udp = new UdpClient();
        udp.Connect(new IPEndPoint(ResolveIPv4(host), port));
    }

    public async Task<byte[]> ReadMemoryAsync(uint address, int size)
    {
        var result = new byte[size];
        var done = 0;
        while (done < size)
        {
            var chunk = Math.Min(MaxChunk, size - done);
            var id = requestId++;
            var packet = new byte[HeaderSize + 8];
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), Version);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4, 4), id);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8, 4), ReadMemory);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12, 4), 8);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16, 4), address + (uint)done);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20, 4), (uint)chunk);

            await udp.SendAsync(packet, packet.Length);
            using var cts = new CancellationTokenSource(1500);
            var reply = await udp.ReceiveAsync(cts.Token);
            var raw = reply.Buffer;
            if (raw.Length < HeaderSize) throw new IOException("Short RPC reply");

            var version = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0, 4));
            var replyId = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4, 4));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8, 4));
            var payloadSize = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12, 4));
            if (version != Version || replyId != id || type != ReadMemory || raw.Length != HeaderSize + payloadSize)
                throw new IOException("Invalid RPC reply");

            raw.AsSpan(HeaderSize).CopyTo(result.AsSpan(done));
            done += (int)payloadSize;
            if (payloadSize == 0) throw new IOException("RPC returned zero bytes");
        }
        return result;
    }

    private static IPAddress ResolveIPv4(string host)
    {
        if (IPAddress.TryParse(host, out var ip)) return ip;
        return Dns.GetHostAddresses(host).First(a => a.AddressFamily == AddressFamily.InterNetwork);
    }

    public void Dispose() => udp.Dispose();
}

internal sealed class WindowsProcessMemoryReader : IDisposable
{
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_NOACCESS = 0x01;
    private const uint PAGE_GUARD = 0x100;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_WRITECOPY = 0x08;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_WRITECOPY = 0x80;

    private readonly Process process;
    private readonly IntPtr handle;

    public int ProcessId => process.Id;
    public bool IsAlive
    {
        get
        {
            try { return !process.HasExited; }
            catch { return false; }
        }
    }

    private WindowsProcessMemoryReader(Process process, IntPtr handle)
    {
        this.process = process;
        this.handle = handle;
    }

    public static WindowsProcessMemoryReader? TryOpenRetroArch()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var candidates = Process.GetProcessesByName("retroarch")
                .Where(p => !p.HasExited)
                .OrderByDescending(p => p.StartTime)
                .ToArray();
            foreach (var process in candidates)
            {
                var handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, process.Id);
                if (handle != IntPtr.Zero)
                    return new WindowsProcessMemoryReader(process, handle);
                process.Dispose();
            }
        }
        catch { }
        return null;
    }

    public byte[]? Read(ulong address, int size)
    {
        if (handle == IntPtr.Zero || size <= 0) return null;
        var buffer = new byte[size];
        if (!ReadProcessMemory(handle, new IntPtr(unchecked((long)address)), buffer, (UIntPtr)(uint)size, out var read))
            return null;
        var count = checked((int)read.ToUInt64());
        if (count < size) return null;
        return buffer;
    }

    public (ulong Address, byte[] Raw)? FindGen4Opponent(bool debug)
    {
        if (!IsAlive) return null;

        ulong cursor = 0x10000;
        ulong scanned = 0;
        const ulong maxAddress = 0x00007FFFFFFF0000;
        const ulong maxScanBytes = 768UL * 1024 * 1024;
        const int chunkSize = 2 * 1024 * 1024;
        const int overlap = 0x180;

        while (cursor < maxAddress && scanned < maxScanBytes)
        {
            if (VirtualQueryEx(handle, new IntPtr(unchecked((long)cursor)), out var mbi, (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == UIntPtr.Zero)
                break;

            var baseAddress = unchecked((ulong)mbi.BaseAddress.ToInt64());
            var regionSize = mbi.RegionSize.ToUInt64();
            if (regionSize == 0) break;
            cursor = baseAddress + regionSize;

            var readable = mbi.State == MEM_COMMIT
                && (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) == 0;
            var writable = (mbi.Protect & (PAGE_READWRITE | PAGE_WRITECOPY | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY)) != 0;
            if (!readable || !writable) continue;

            // DS main RAM is 4 MiB, but cores can place it inside a somewhat
            // larger writable allocation. Skip tiny bookkeeping pages and huge
            // allocations that would make discovery unnecessarily expensive.
            if (regionSize < 0x10000 || regionSize > 128UL * 1024 * 1024) continue;

            for (ulong off = 0; off < regionSize && scanned < maxScanBytes; off += (ulong)(chunkSize - overlap))
            {
                var want = (int)Math.Min((ulong)chunkSize, regionSize - off);
                var buffer = new byte[want];
                if (!ReadProcessMemory(handle, new IntPtr(unchecked((long)(baseAddress + off))), buffer, (UIntPtr)(uint)want, out var bytesRead))
                    continue;
                var got = checked((int)bytesRead.ToUInt64());
                if (got < 0x100) continue;
                scanned += (ulong)got;

                var enemyOffset = FindGen4OpponentOffsetLocal(buffer.AsSpan(0, got));
                if (enemyOffset >= 0)
                {
                    var raw = buffer.AsSpan(enemyOffset, 0x80).ToArray();
                    var address = baseAddress + off + (ulong)enemyOffset;
                    if (debug) Console.WriteLine($"Process-memory Gen 4 opponent structure: 0x{address:X}");
                    return (address, raw);
                }
            }
        }

        if (debug) Console.WriteLine($"Process-memory scan completed ({scanned / (1024 * 1024)} MiB checked), no Gen 4 battle pair found.");
        return null;
    }

    private static int FindGen4OpponentOffsetLocal(ReadOnlySpan<byte> data)
    {
        var found = new List<int>();
        for (var run = 0; run + 6 <= data.Length; run++)
        {
            if (data[run] != 0x06 || data[run + 1] != 0x06 || data[run + 2] != 0x06
                || data[run + 3] != 0x06 || data[run + 4] != 0x06 || data[run + 5] != 0x06)
                continue;
            for (var stageOffset = 0x18; stageOffset <= 0x1A; stageOffset++)
            {
                var start = run - stageOffset;
                if (start < 0 || start + 0x80 > data.Length || (start & 1) != 0 || found.Contains(start)) continue;
                if (LooksLikeBattleMon(data.Slice(start, 0x80))) found.Add(start);
            }
        }
        found.Sort();
        if (found.Count < 2) return -1;
        for (var i = 0; i < found.Count - 1; i++)
            if (found[i + 1] - found[i] == 0x80) return found[i + 1];
        for (var i = 0; i < found.Count - 1; i++)
        {
            var gap = found[i + 1] - found[i];
            if (gap is >= 0x80 and <= 0x1000) return found[i + 1];
        }
        return -1;
    }

    private static bool LooksLikeBattleMon(ReadOnlySpan<byte> data)
    {
        if (data.Length < 0x80) return false;
        var species = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0, 2));
        if (species is < 1 or > 493) return false;
        var level = data[0x34];
        if (level is < 1 or > 100) return false;

        var moves = new[]
        {
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0C, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0E, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x10, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x12, 2))
        };
        if (!moves.Any(m => m is > 0 and <= 467) || moves.Any(m => m > 467)) return false;

        var neutralStages = 0;
        for (var i = 0x18; i < 0x20; i++)
        {
            if (data[i] > 0x0C) return false;
            if (data[i] == 0x06) neutralStages++;
        }
        if (neutralStages < 5) return false;

        var hp = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x4C, 2));
        var maxHP = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x50, 2));
        if (maxHP is 0 or > 999 || hp > maxHP) return false;
        if (data[0x7E] > 2) return false;
        return true;
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero) CloseHandle(handle);
        process.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, UIntPtr dwSize, out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, UIntPtr dwLength);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);
}

internal sealed class RetroArchReader : IDisposable
{
    private readonly UdpClient udp;

    public RetroArchReader(string host, int port)
    {
        udp = new UdpClient();
        udp.Connect(new IPEndPoint(ResolveIPv4(host), port));
    }

    public async Task<string> GetStatusAsync()
    {
        var command = Encoding.ASCII.GetBytes("GET_STATUS");
        await udp.SendAsync(command, command.Length);
        using var cts = new CancellationTokenSource(1500);
        var reply = await udp.ReceiveAsync(cts.Token);
        return Encoding.ASCII.GetString(reply.Buffer).Trim();
    }

    public async Task<byte[]> ReadMemoryAsync(uint address, int size)
    {
        var command = Encoding.ASCII.GetBytes($"READ_CORE_MEMORY {address:X} {size}");
        await udp.SendAsync(command, command.Length);
        using var cts = new CancellationTokenSource(1500);
        var reply = await udp.ReceiveAsync(cts.Token);
        var text = Encoding.ASCII.GetString(reply.Buffer).Trim();

        if (!text.StartsWith("READ_CORE_MEMORY", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Unexpected RetroArch reply: {text}");
        if (text.Contains(" -1", StringComparison.Ordinal))
            throw new IOException(text);

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Reply format: READ_CORE_MEMORY <address> <byte1> <byte2> ...
        if (parts.Length < 2 + size)
            throw new IOException($"RetroArch returned fewer bytes than requested ({parts.Length - 2}/{size})");

        var data = new byte[size];
        for (var i = 0; i < size; i++)
            data[i] = Convert.ToByte(parts[i + 2], 16);
        return data;
    }

    public async Task<byte[]> ReadCoreRamAsync(uint address, int size)
    {
        var command = Encoding.ASCII.GetBytes($"READ_CORE_RAM {address:X} {size}");
        await udp.SendAsync(command, command.Length);
        using var cts = new CancellationTokenSource(1500);
        var reply = await udp.ReceiveAsync(cts.Token);
        var text = Encoding.ASCII.GetString(reply.Buffer).Trim();

        if (!text.StartsWith("READ_CORE_RAM", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Unexpected RetroArch reply: {text}");
        if (text.Contains(" -1", StringComparison.Ordinal))
            throw new IOException(text);

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 + size)
            throw new IOException($"RetroArch returned fewer core-RAM bytes than requested ({parts.Length - 2}/{size})");

        var data = new byte[size];
        for (var i = 0; i < size; i++)
            data[i] = Convert.ToByte(parts[i + 2], 16);
        return data;
    }

    private static IPAddress ResolveIPv4(string host)
    {
        if (IPAddress.TryParse(host, out var ip)) return ip;
        return Dns.GetHostAddresses(host).First(a => a.AddressFamily == AddressFamily.InterNetwork);
    }

    public void Dispose() => udp.Dispose();
}
