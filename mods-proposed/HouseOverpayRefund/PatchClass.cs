using System.Collections.Concurrent;
using System.Linq;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace HouseOverpayRefund;

// Fixes ACEmulator/ACE issue #1723: paying house buy/rent with a trade note (a fixed-denomination
// item, e.g. a Note/Mega-Merchant-Deed) larger than the amount owed consumes the WHOLE note and
// throws away the difference. Verified in current master (2026-09-22):
//
//   HousePayment.GetConsumeItems_Inner (Source/ACE.Server/Network/Structure/HousePayment.cs):
//     for a plain pyreal COIN stack, when the stack is bigger than what's still owed it computes
//     `consumeAmount = remaining` and consumes only that many coins from the stack - the rest of
//     the stack is left behind, untouched, in the player's inventory. Coin overpayment is NOT
//     bugged; there is nothing to refund there and this mod must never touch it.
//     for a TRADE NOTE (IsTradeNote == true, discrete/fixed value), it instead computes
//     `consumeAmount = Ceiling(remaining / baseValue)` - a whole number of NOTES - and the full
//     value of that many whole notes is destroyed via TryConsumeFromInventoryWithNetworking, even
//     when their value exceeds `remaining`. That excess is the bug: it is simply lost.
//
// So the safe fix is narrow: recompute, from the SAME HousePayment/GetConsumeItems call the base
// game is about to make (a pure, side-effect-free read of current inventory state), how much
// pyreal-equivalent VALUE the consume list is actually about to destroy versus how much is
// actually owed (`HousePayment.Num`, read before the real payment call runs). The difference can
// only be positive when a trade note was the item that closed out the remaining balance, and it
// is bounded by construction: refund = consumedValue - owed <= totalSent - owed, so this can never
// mint more than the real overpayment. A Prefix computes and stores that bound; a Postfix mints a
// fresh pyreal stack for it ONLY if the payment actually succeeded (never speculatively, never
// twice - the pending value is removed as soon as it is read).
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const uint PyrealWcid = 273;

    private static Settings? Cfg;

    // One pending refund per player guid, produced by the Prefix and consumed by the matching
    // Postfix in the same call. Never left lying around: Take() removes it whether or not the
    // postfix decides to actually mint anything.
    private static readonly ConcurrentDictionary<uint, uint> Pending = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static uint Take(uint guid) => Pending.TryRemove(guid, out var v) ? v : 0;

    /// <summary>
    /// Reads what the vanilla GetConsumeItems() call is about to do to the player's currency
    /// items, without consuming anything, and returns how much pyreal-equivalent value it will
    /// destroy beyond what is actually owed. Never negative. Returns 0 (and logs nothing) unless
    /// the house's price is expressed in pyreals (WeenieID 273), which is the only currency path
    /// GetConsumeItems_Inner can overshoot.
    /// </summary>
    private static uint ComputeOverpay(Player player, List<HousePayment> paymentList, List<uint> item_ids)
    {
        var payItem = paymentList?.FirstOrDefault(p => p.WeenieID == PyrealWcid);
        if (payItem == null || payItem.Remaining <= 0)
            return 0;

        var owed = payItem.Remaining;

        var items = player.GetInventoryItems(item_ids);
        var consumeItems = payItem.GetConsumeItems(items);

        long consumedValue = 0;
        foreach (var ci in consumeItems)
        {
            var wo = ci.TryGetWorldObject();
            if (wo == null)
                continue;

            // Trade notes: the recorded Value is a COUNT of whole notes (GetConsumeItems_Inner's
            // trade-note branch), each worth StackUnitValue. Everything else (plain pyreal coin
            // stacks) already records Value as the exact pyreal amount consumed - no excess there.
            consumedValue += wo.IsTradeNote ? (long)ci.Value * (wo.StackUnitValue ?? 0) : ci.Value;
        }

        var overpay = consumedValue - owed;
        return overpay > 0 ? (uint)Math.Min(overpay, uint.MaxValue) : 0;
    }

    private static void GiveRefund(Player player, uint amount, string kind)
    {
        if (amount == 0)
            return;

        var cfg = Cfg;
        new ActionChain(player, () =>
        {
            try
            {
                var coin = WorldObjectFactory.CreateNewWorldObject(PyrealWcid);
                if (coin == null)
                    return;

                coin.SetStackSize((int)amount);

                if (player.TryCreateInInventoryWithNetworking(coin))
                {
                    if (cfg?.LogRefunds == true)
                        ModManager.Log($"[HouseOverpayRefund] {player.Name} ({player.Guid}) overpaid house {kind} by {amount} pyreals (trade note change) - refunded.");

                    if (cfg?.NotifyPlayer == true)
                        player.Session?.Network.EnqueueSend(new GameMessageSystemChat(string.Format(cfg.RefundMessage, amount), ChatMessageType.Broadcast));
                }
                else
                {
                    coin.Destroy();
                    ModManager.Log($"[HouseOverpayRefund] {player.Name} ({player.Guid}): refund of {amount} pyreals could not be placed in inventory (pack full?) - not minted.");
                }
            }
            catch (Exception e)
            {
                ModManager.Log($"[HouseOverpayRefund] {e.Message}");
            }
        }).EnqueueChain();
    }

    // --- Buy ---

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionBuyHouse))]
    public static void PreBuy(Player __instance, uint slumlord_id, List<uint> item_ids)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;

            // Reset this player's slot to 0 FIRST, before any early return below, and unconditionally overwrite
            // it again with the real computed value if we get that far. Two ways a stale nonzero value could
            // otherwise survive to an unrelated later call: (1) the vanilla method throws after this prefix runs,
            // so Harmony's postfix never fires; (2) this prefix itself returns early (house already owned, etc.)
            // without reaching the overpay computation. Either way, without this reset an unrelated later
            // successful call for the same player guid could Take() and refund a value that has nothing to do
            // with that transaction.
            Pending[__instance.Guid.Full] = 0;

            var slumlord = __instance.CurrentLandblock?.GetObject(slumlord_id) as SlumLord;
            if (slumlord == null || slumlord.HouseOwner != null) return; // already owned / will fail verification anyway

            var houseProfile = slumlord.GetHouseProfile();
            var overpay = ComputeOverpay(__instance, houseProfile.Buy, item_ids);
            Pending[__instance.Guid.Full] = overpay;
        }
        catch (Exception e) { ModManager.Log($"[HouseOverpayRefund] {e.Message}"); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionBuyHouse))]
    public static void PostBuy(Player __instance, uint slumlord_id, List<uint> item_ids)
    {
        try
        {
            var amount = Take(__instance.Guid.Full);
            if (amount == 0) return;

            // Only refund if the purchase actually went through (owner is now this player) -
            // if VerifyPurchase/TryConsumePurchaseItems failed, nothing was consumed and there is
            // nothing to refund.
            var slumlord = __instance.CurrentLandblock?.GetObject(slumlord_id) as SlumLord;
            if (slumlord?.HouseOwner != __instance.Guid.Full) return;

            GiveRefund(__instance, amount, "purchase");
        }
        catch (Exception e) { ModManager.Log($"[HouseOverpayRefund] {e.Message}"); }
    }

    // --- Rent / maintenance ---

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionRentHouse))]
    public static void PreRent(Player __instance, uint slumlord_id, List<uint> item_ids)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;

            Pending[__instance.Guid.Full] = 0; // reset first - see PreBuy's comment for why this must happen before any early return

            var slumlord = __instance.CurrentLandblock?.GetObject(slumlord_id) as SlumLord;
            if (slumlord == null || slumlord.IsRentPaid()) return; // already paid this period; call will no-op

            var houseProfile = slumlord.GetHouseProfile();
            var overpay = ComputeOverpay(__instance, houseProfile.Rent, item_ids);
            Pending[__instance.Guid.Full] = overpay;
        }
        catch (Exception e) { ModManager.Log($"[HouseOverpayRefund] {e.Message}"); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionRentHouse))]
    public static void PostRent(Player __instance, uint slumlord_id, List<uint> item_ids)
    {
        try
        {
            var amount = Take(__instance.Guid.Full);
            if (amount == 0) return;

            var slumlord = __instance.CurrentLandblock?.GetObject(slumlord_id) as SlumLord;
            // Rent paid this call is the only observable "it went through" signal available here.
            if (slumlord == null || !slumlord.IsRentPaid()) return;

            GiveRefund(__instance, amount, "maintenance");
        }
        catch (Exception e) { ModManager.Log($"[HouseOverpayRefund] {e.Message}"); }
    }
}
