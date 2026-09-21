using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace TriviaNight;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object gate = new();
    private static readonly Random rng = new();

    // In-memory state only, guarded by gate.
    private static bool running;
    private static int roundsTotal, roundNo;
    private static List<Question> order = new();
    private static Question? current;
    private static DateTime roundStart, nextRoundAt;
    private static bool hintSent;
    private static readonly Dictionary<string, int> scores = new();
    private static readonly Dictionary<string, Player> players = new();

    // PlayerManager.BroadcastToAll(GameMessage) verified in PlayerManager.cs.
    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    private static string Norm(string s) =>
        new string(s.Trim().ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '\'').ToArray());

    // Player.HandleActionTalk(string) verified in Player.cs (public; local chat; runs on the player's actor).
    // Postfix, so the original has already broadcast the answer normally.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionTalk), new Type[] { typeof(string) })]
    public static void PostTalk(Player __instance, string message)
    {
        try
        {
            if (Cfg == null || !Cfg.Enabled || __instance.IsGagged) return;
            string? won = null;
            lock (gate)
            {
                if (!running || current == null) return;
                var m = Norm(message);
                if (!current.Answers.Any(a => Norm(a) == m)) return;
                var q = current;
                current = null; // first answer wins; later ones fall through
                nextRoundAt = DateTime.UtcNow.AddSeconds(Cfg.PauseSeconds);
                scores[__instance.Name] = scores.GetValueOrDefault(__instance.Name) + 1;
                players[__instance.Name] = __instance;
                won = $"{__instance.Name} got it! The answer was \"{q.Answers[0]}\". ({scores[__instance.Name]} point(s))";
            }
            if (won != null) Broadcast(won);
        }
        catch (Exception e) { ModManager.Log($"[TriviaNight] {e.Message}"); }
    }

    private static void Ask() // under gate
    {
        roundNo++;
        current = order[(roundNo - 1) % order.Count];
        roundStart = DateTime.UtcNow;
        hintSent = false;
        Broadcast($"TRIVIA round {roundNo}/{roundsTotal}: {current.Text} (answer in local chat)");
    }

    private static void Finish() // under gate
    {
        running = false;
        current = null;
        if (scores.Count == 0) { Broadcast("Trivia is over - nobody scored. Thanks for playing!"); return; }
        var top = scores.OrderByDescending(x => x.Value).ToList();
        Broadcast("Trivia is over! " + string.Join(", ", top.Take(5).Select((x, i) => $"{i + 1}. {x.Key} {x.Value}")));
        var winners = top.Where(x => x.Value == top[0].Value).ToList();
        if (winners.Count != 1) { Broadcast("It's a tie - no prize this time."); return; }
        var prize = Math.Clamp(Cfg!.WinnerPyreals, 0, Cfg.MaxPyreals);
        if (prize <= 0 || !players.TryGetValue(winners[0].Key, out var p)) return;
        // Timer thread: queue onto the player's actor. Coin creation pattern verified in Player_Commerce.cs
        // (WorldObjectFactory.CreateNewWorldObject("coinstack") + SetStackSize);
        // TryCreateInInventoryWithNetworking verified in Player_Inventory.cs.
        new ActionChain(p, () =>
        {
            try
            {
                var left = prize;
                while (left > 0)
                {
                    var stack = WorldObjectFactory.CreateNewWorldObject("coinstack");
                    if (stack == null) return;
                    var n = Math.Min(left, stack.MaxStackSize ?? 1);
                    stack.SetStackSize(n);
                    if (!p.TryCreateInInventoryWithNetworking(stack)) { stack.Destroy(); return; }
                    left -= n;
                }
                Broadcast($"{p.Name} wins the trivia prize: {prize} pyreals!");
            }
            catch (Exception e) { ModManager.Log($"[TriviaNight] prize: {e.Message}"); }
        }).EnqueueChain();
    }

    private static void Tick()
    {
        lock (gate)
        {
            if (!running || Cfg == null) return;
            var now = DateTime.UtcNow;
            if (current == null)
            {
                if (now < nextRoundAt) return;
                if (roundNo >= roundsTotal) Finish(); else Ask();
                return;
            }
            var age = (now - roundStart).TotalSeconds;
            if (!hintSent && age >= Cfg.HintAfterSeconds)
            {
                hintSent = true;
                var a = current.Answers[0];
                Broadcast($"Hint: the answer starts with '{a[0]}' and has {a.Length} letters.");
            }
            if (age >= Cfg.RoundTimeoutSeconds)
            {
                Broadcast($"Time's up! The answer was \"{current.Answers[0]}\".");
                current = null;
                nextRoundAt = now.AddSeconds(Cfg.PauseSeconds);
            }
        }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (Exception e) { ModManager.Log($"[TriviaNight] {e.Message}"); }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        lock (gate) { running = false; current = null; }
        base.Stop();
    }

    [CommandHandler("trivia", AccessLevel.Admin, CommandHandlerFlag.None, 1,
        "Chat trivia.", "start [rounds] | stop | status")]
    public static void HandleTrivia(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "TriviaNight is disabled (Enabled=false in Settings.json)."); return; }
        switch (parameters[0].ToLowerInvariant())
        {
            case "start":
                lock (gate)
                {
                    if (running) { Say(session, "Trivia is already running."); return; }
                    var n = cfg.DefaultRounds;
                    if (parameters.Length > 1) int.TryParse(parameters[1], out n);
                    roundsTotal = Math.Clamp(n, 1, cfg.MaxRounds);
                    order = cfg.Questions.Where(q => q.Answers.Count > 0).OrderBy(_ => rng.Next()).ToList();
                    if (order.Count == 0) { Say(session, "No usable questions configured."); return; }
                    scores.Clear();
                    players.Clear();
                    roundNo = 0;
                    running = true;
                    current = null;
                    nextRoundAt = DateTime.UtcNow.AddSeconds(cfg.PauseSeconds);
                    Broadcast($"TRIVIA NIGHT starts! {roundsTotal} rounds - the first correct answer in local chat wins each round.");
                }
                Say(session, "Trivia started.");
                break;
            case "stop":
                lock (gate)
                {
                    if (!running) { Say(session, "Trivia is not running."); return; }
                    running = false;
                    current = null;
                    Broadcast("Trivia was stopped by an admin.");
                }
                break;
            case "status":
                lock (gate)
                {
                    if (!running) { Say(session, "Trivia is not running."); return; }
                    Say(session, $"Round {roundNo}/{roundsTotal}; " + (scores.Count == 0 ? "no scores yet." :
                        string.Join(", ", scores.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}"))));
                }
                break;
            default:
                Say(session, "Usage: trivia start [rounds] | stop | status");
                break;
        }
    }
}
