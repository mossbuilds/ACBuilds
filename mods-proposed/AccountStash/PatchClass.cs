using System.Text.Json;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AccountStash;

// A shared pyreal balance across every character on one account. Keyed by Session.Account (ACE.Server.Network,
// verified: `public string Account { get; private set; }`, set via SetAccount() at login) - never by anything
// from ace_auth/ace_shard, and never read from the database. The balance itself lives in a single mod-owned
// JSON file (Account -> long pyreals) next to the mod, written and read only through this file - no other
// account data (login name aside, which ACE already hands this session), no character data, no per-character
// balances, no luminance/keys (item-identity handling this mod does not attempt).
//
// A single process-wide lock guards every read-modify-write of the balance file. The idea note called for a
// per-account lock, but the balance store is one shared JSON file: two accounts transacting at the same
// moment still serialize on that file's I/O either way, so one lock keeps the code simple without changing
// the actual concurrency (transactions are expected to be rare and instantaneous, not a hot path).
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const uint PyrealWcid = 273;

    private static Settings? Cfg;
    private static readonly object FileLock = new();
    private static readonly Dictionary<string, DateTime> LastActionUtc = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Send(Session session, string text) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));

    private static Dictionary<string, long> LoadLocked(Settings cfg)
    {
        try
        {
            if (!File.Exists(cfg.BalanceFile))
                return new Dictionary<string, long>();
            var json = File.ReadAllText(cfg.BalanceFile);
            return JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? new Dictionary<string, long>();
        }
        catch (Exception e)
        {
            ModManager.Log($"[AccountStash] failed to read {cfg.BalanceFile}: {e.Message}");
            return new Dictionary<string, long>();
        }
    }

    private static void SaveLocked(Settings cfg, Dictionary<string, long> balances)
    {
        var tmp = cfg.BalanceFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(balances));
        File.Copy(tmp, cfg.BalanceFile, overwrite: true);
        File.Delete(tmp);
    }

    private static long GetBalance(Settings cfg, string account)
    {
        lock (FileLock)
        {
            var balances = LoadLocked(cfg);
            return balances.TryGetValue(account, out var v) ? v : 0;
        }
    }

    // Returns true and the new balance if the change was applied; false if it was rejected (caller sends
    // no message on false - the command handler decides what to say).
    private static bool TryChangeBalance(Settings cfg, string account, long delta, out long newBalance)
    {
        lock (FileLock)
        {
            var balances = LoadLocked(cfg);
            balances.TryGetValue(account, out var current);
            var updated = current + delta;
            if (updated < 0) { newBalance = current; return false; }
            if (updated > cfg.MaxBalance) { newBalance = current; return false; }
            balances[account] = updated;
            SaveLocked(cfg, balances);
            newBalance = updated;
            return true;
        }
    }

    private static bool CheckCooldown(Settings cfg, string account)
    {
        lock (LastActionUtc)
        {
            var now = DateTime.UtcNow;
            if (LastActionUtc.TryGetValue(account, out var last) && (now - last).TotalSeconds < cfg.CooldownSeconds)
                return false;
            LastActionUtc[account] = now;
            return true;
        }
    }

    [CommandHandler(
        "stash",
        AccessLevel.Player,
        CommandHandlerFlag.RequiresWorld,
        1,
        "Deposits/withdraws pyreals from a stash shared by every character on your account.",
        "deposit <amount> | withdraw <amount> | balance"
    )]
    public static void HandleStash(Session session, params string[] parameters)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;

            var player = session.Player;
            if (player == null) return;

            var account = session.Account; // ACE.Server.Network.Session.Account - the per-account key, never the DB
            if (string.IsNullOrEmpty(account)) return;

            var sub = parameters[0].ToLowerInvariant();

            if (sub == "balance")
            {
                var bal = GetBalance(cfg, account);
                Send(session, string.Format(cfg.BalanceMessage, bal));
                return;
            }

            if ((sub != "deposit" && sub != "withdraw") || parameters.Length < 2 || !long.TryParse(parameters[1], out var amount) || amount <= 0)
            {
                Send(session, cfg.BadAmount);
                return;
            }

            if (!CheckCooldown(cfg, account))
            {
                Send(session, cfg.OnCooldown);
                return;
            }

            if (sub == "deposit")
            {
                if (!player.TryConsumeFromInventoryWithNetworking(PyrealWcid, (int)Math.Min(amount, int.MaxValue)))
                {
                    Send(session, cfg.NotEnoughInventory);
                    return;
                }

                if (!TryChangeBalance(cfg, account, amount, out var newBalance))
                {
                    // Rejected (would exceed MaxBalance): give the pyreals back rather than destroying them.
                    var chain = new ActionChain(player, () =>
                    {
                        var coin = WorldObjectFactory.CreateNewWorldObject(PyrealWcid);
                        if (coin == null) return;
                        coin.SetStackSize((int)amount);
                        if (!player.TryCreateInInventoryWithNetworking(coin))
                        {
                            coin.Destroy();
                            ModManager.Log($"[AccountStash] {player.Name} ({player.Guid}): deposit refund of {amount} pyreals could not be returned (pack full?) - pyreals lost.");
                        }
                    });
                    chain.EnqueueChain();
                    Send(session, string.Format(cfg.OverMax, cfg.MaxBalance));
                    return;
                }

                Send(session, string.Format(cfg.DepositOk, amount, newBalance));
                ModManager.Log($"[AccountStash] {account} deposited {amount} pyreals via {player.Name} ({player.Guid}). New balance: {newBalance}.");
            }
            else // withdraw
            {
                if (!TryChangeBalance(cfg, account, -amount, out var newBalance))
                {
                    Send(session, cfg.NotEnoughStash);
                    return;
                }

                var chain = new ActionChain(player, () =>
                {
                    var coin = WorldObjectFactory.CreateNewWorldObject(PyrealWcid);
                    if (coin == null)
                    {
                        TryChangeBalance(cfg, account, amount, out _); // refund the stash
                        return;
                    }
                    coin.SetStackSize((int)amount);
                    if (!player.TryCreateInInventoryWithNetworking(coin))
                    {
                        coin.Destroy();
                        TryChangeBalance(cfg, account, amount, out _); // inventory couldn't hold it - refund the stash
                        Send(session, cfg.InventoryFull);
                        return;
                    }
                    Send(session, string.Format(cfg.WithdrawOk, amount, newBalance));
                    ModManager.Log($"[AccountStash] {account} withdrew {amount} pyreals via {player.Name} ({player.Guid}). New balance: {newBalance}.");
                });
                chain.EnqueueChain();
            }
        }
        catch (Exception e)
        {
            ModManager.Log($"[AccountStash] {e.Message}");
        }
    }
}
