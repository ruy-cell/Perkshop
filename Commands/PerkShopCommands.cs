using PerkShop.Services;
using VampireCommandFramework;

namespace PerkShop.Commands;

[CommandGroup("perkshop", "perk")]
internal static class PerkShopCommands
{
    [Command("menu", adminOnly: false)]
    public static void Menu(ChatCommandContext ctx)
        => ShopService.Menu(ctx.Event.SenderUserEntity, ctx.Reply);
    [Command("status", adminOnly: false)]
    public static void Status(ChatCommandContext ctx, int page = 1)
        => ShopService.Status(ctx.Event.SenderUserEntity, page, ctx.Reply);

    [Command("search", adminOnly: false)]
    public static void Search(ChatCommandContext ctx, string query = "")
        => ShopService.Search(ctx.Event.SenderUserEntity, query, ctx.Reply);

    // Preferred buff commands, matching the stat command prefix style.
    [Command("bufflist", adminOnly: false)]
    public static void BuffList(ChatCommandContext ctx, int page = 1)
        => ShopService.ListBuffKeys(ctx.Event.SenderUserEntity, page, ctx.Reply);
    [Command("buffdet", adminOnly: false)]
    public static void BuffDetail(ChatCommandContext ctx, string buffKey = "")
        => ShopService.BuffDetails(ctx.Event.SenderUserEntity, buffKey, ctx.Reply);

    [Command("buffbuy", adminOnly: false)]
    public static void BuffBuy(ChatCommandContext ctx, string buffKey = "")
        => ShopService.BuyBuff(ctx.Event.SenderUserEntity, ctx.Event.SenderCharacterEntity, buffKey, ctx.Reply);

    [Command("buffremove", adminOnly: false)]
    public static void BuffRemove(ChatCommandContext ctx, string buffKey = "")
        => ShopService.RemoveBuff(ctx.Event.SenderUserEntity, ctx.Event.SenderCharacterEntity, buffKey, ctx.Reply);
    // Stat commands.
    [Command("statlist", adminOnly: false)]
    public static void StatList(ChatCommandContext ctx, int page = 1)
        => ShopService.ListStatKeys(ctx.Event.SenderUserEntity, page, ctx.Reply);
    [Command("statdet", adminOnly: false)]
    public static void StatDetail(ChatCommandContext ctx, string statKey = "")
        => ShopService.StatDetails(ctx.Event.SenderUserEntity, statKey, ctx.Reply);

    [Command("statbuy", adminOnly: false)]
    public static void StatBuy(ChatCommandContext ctx, string statKey = "")
        => ShopService.BuyStat(ctx.Event.SenderUserEntity, ctx.Event.SenderCharacterEntity, statKey, ctx.Reply);
    [Command("statremove", adminOnly: false)]
    public static void StatRemove(ChatCommandContext ctx, string statKey = "")
        => ShopService.RemoveStat(ctx.Event.SenderUserEntity, ctx.Event.SenderCharacterEntity, statKey, ctx.Reply);
    [Command("sync", adminOnly: false)]
    public static void Sync(ChatCommandContext ctx)
        => ShopService.Sync(ctx.Event.SenderUserEntity, ctx.Reply);

    // Admin ownership commands.
    [Command("admin", adminOnly: true)]
    public static void Admin(ChatCommandContext ctx)
        => ShopService.AdminMenu(ctx.Reply);
    [Command("info", adminOnly: true)]
    public static void Info(ChatCommandContext ctx, string playerRef = "")
        => ShopService.AdminInfo(playerRef, ctx.Reply);

    [Command("giftbuff", adminOnly: true)]
    public static void GiftBuff(ChatCommandContext ctx, string playerRef = "", string buffKey = "")
        => ShopService.AdminGiftBuff(playerRef, buffKey, ctx.Reply);

    [Command("revokebuff", adminOnly: true)]
    public static void RevokeBuff(ChatCommandContext ctx, string playerRef = "", string buffKey = "")
        => ShopService.AdminRevokePurchasedBuff(playerRef, buffKey, ctx.Reply);

    [Command("giftstat", adminOnly: true)]
    public static void GiftStat(ChatCommandContext ctx, string playerRef = "", string statKey = "", int ranks = 1)
        => ShopService.AdminGiftStat(playerRef, statKey, ranks, ctx.Reply);

    [Command("revokestat", adminOnly: true)]
    public static void RevokeStat(ChatCommandContext ctx, string playerRef = "", string statKey = "", int ranks = 1)
        => ShopService.AdminRevokePurchasedStat(playerRef, statKey, ranks, ctx.Reply);

    [Command("addbuff", adminOnly: true)]
    public static void AddBuff(ChatCommandContext ctx, string playerRef = "", string buffKey = "")
        => ShopService.AdminAdd(playerRef, buffKey, ctx.Reply);

    [Command("clearbuff", adminOnly: true)]
    public static void ClearBuff(ChatCommandContext ctx, string playerRef = "", string buffKey = "")
        => ShopService.AdminRemove(playerRef, buffKey, ctx.Reply);

    [Command("addflat", adminOnly: true)]
    public static void AddFlat(ChatCommandContext ctx, string playerRef = "", string unitStat = "", float amount = 0f)
        => ShopService.AdminAddFlatStat(playerRef, unitStat, amount, ctx.Reply);

    [Command("clearflat", adminOnly: true)]
    public static void ClearFlat(ChatCommandContext ctx, string playerRef = "", string unitStat = "")
        => ShopService.AdminClearFlatStat(playerRef, unitStat, ctx.Reply);

    // Admin whitelist commands.
    [Command("wlstatus", adminOnly: true)]
    public static void WhitelistStatus(ChatCommandContext ctx)
        => ShopService.WhitelistStatus(ctx.Reply);

    [Command("wlcheckbuff", adminOnly: true)]
    public static void WhitelistCheckBuff(ChatCommandContext ctx)
        => ShopService.WhitelistList("buff", ctx.Reply);

    [Command("wlcheckstat", adminOnly: true)]
    public static void WhitelistCheckStat(ChatCommandContext ctx)
        => ShopService.WhitelistList("stat", ctx.Reply);

    [Command("wlcheckall", adminOnly: true)]
    public static void WhitelistCheckAll(ChatCommandContext ctx)
        => ShopService.WhitelistList("all", ctx.Reply);

    [Command("wlplayer", adminOnly: true)]
    public static void WhitelistPlayer(ChatCommandContext ctx, string playerRef = "")
        => ShopService.WhitelistPlayer(playerRef, ctx.Reply);

    [Command("wladdbuff", adminOnly: true)]
    public static void WhitelistAddBuff(ChatCommandContext ctx, string playerRef = "")
        => ShopService.WhitelistAdd("buff", playerRef, ctx.Reply);

    [Command("wlremovebuff", adminOnly: true)]
    public static void WhitelistRemoveBuff(ChatCommandContext ctx, string playerRef = "")
        => ShopService.WhitelistRemove("buff", playerRef, ctx.Reply);

    [Command("wladdstat", adminOnly: true)]
    public static void WhitelistAddStat(ChatCommandContext ctx, string playerRef = "")
        => ShopService.WhitelistAdd("stat", playerRef, ctx.Reply);

    [Command("wlremovestat", adminOnly: true)]
    public static void WhitelistRemoveStat(ChatCommandContext ctx, string playerRef = "")
        => ShopService.WhitelistRemove("stat", playerRef, ctx.Reply);

    [Command("reload", adminOnly: true)]
    public static void Reload(ChatCommandContext ctx)
        => ShopService.ReloadConfig(ctx.Reply);

    [Command("diag", adminOnly: true)]
    public static void Diagnostics(ChatCommandContext ctx)
        => ShopService.Diagnostics(ctx.Reply);

    [Command("validate", adminOnly: true)]
    public static void Validate(ChatCommandContext ctx)
        => ShopService.Validate(ctx.Reply);

    [Command("syncall", adminOnly: true)]
    public static void SyncAll(ChatCommandContext ctx)
        => ShopService.SyncAll(ctx.Reply);
}
