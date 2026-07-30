using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Stats;

namespace Chido.Core.Rewards;

/// <summary>
/// 撃破報酬の算出（戦闘システム 6.2・10.2）。
///
/// <b><see cref="BattleEndReason.PlayerVictory"/> 以外の終了理由では、いかなる参加者も報酬を得られない。</b>
/// 敵に十分なダメージを与えていた場合であっても、<c>EnemyEscaped</c> では報酬は発生しない。
///
/// <b>報酬対象の判定は <see cref="ParticipantStatus"/> ではなく実績で行う。</b>
/// 具体的には「そのプレイヤーの台帳の累計与ダメージが 0 より大きいか」であり、
/// 行動を1回以上行ったか（行動実績）ではなく、実際に1以上のダメージが台帳に積まれたか（結果）を基準とする。
/// <c>Defeated</c> はそれ自体では対象外の理由にならない（1対1のターンモデルでは被ダメージが常に
/// 自分の行動への反撃として発生するため、戦闘不能になるには必ず1回以上行動している）。
/// <c>Escaped</c> は台帳の有無に関わらず対象外。
///
/// 報酬は共有プールを分け合うのではなく、各自の貢献度に基づき<b>個別に</b>算出される。
/// </summary>
public static class RewardCalculator
{
    /// <summary>
    /// 全参加者の報酬を算出する。<see cref="BattleEndReason.PlayerVictory"/> 以外では空を返す。
    /// </summary>
    public static IReadOnlyList<PlayerReward> Calculate(
        BattleEndReason reason, RewardContext context, Random rng)
    {
        if (reason != BattleEndReason.PlayerVictory) return [];

        // Escaped 者の与ダメージも分母に含める。除くと「主力が敵を削りきってから /escape すれば
        // 残った参加者の貢献率が跳ね上がり全員が満額を得る」という悪用経路が生まれる。
        // 分母は「この敵を倒すために費やされた仕事の総量」であるべきで、
        // 後から誰が抜けたかで変動してはならない
        var sumDamage = context.Players.Aggregate(
            BigInteger.Zero, (acc, p) => acc + p.TotalDamageDealt);

        var denominator = Apportionment.Denominator(sumDamage, context.SpawnMaxLifeSum);

        return context.Players
            .Where(IsEligible)
            .Select(player => CalculateFor(player, context, denominator, rng))
            .ToList();
    }

    /// <summary>
    /// 報酬ゲート。台帳累計与ダメージ &gt; 0 かつ離脱していないこと。
    ///
    /// とどめ以降の実効0は台帳に寄与しないため、「HP0の対象にしか関与しなかった参加者」は
    /// 累計0でゲートを通らない。与ダメージ帰属・被攻撃TP・本ゲートの三者が同じ実効ダメージを
    /// 基準量とするため、この扱いは三者一貫して「効果なし」に倒れる。
    /// </summary>
    private static bool IsEligible(PlayerContribution player)
        => !player.Escaped && player.TotalDamageDealt > BigInteger.Zero;

    private static PlayerReward CalculateFor(
        PlayerContribution player, RewardContext context, BigInteger denominator, Random rng)
    {
        var own = player.TotalDamageDealt;

        // 基礎経験値 = E × Σ(組の各メンバーの exp_rate) ÷ 10000。
        // 組の全メンバーはレベルが共通であるため、基礎経験値は exp_rate の合算になる
        var exp = Apportionment.Apportion(
            context.EnemyLevel * context.ExpRateSum,
            Ratio.Full.Permyriad,
            own,
            denominator);

        // 通貨は固定値（抽選なし）を撃破した敵ごとに合算し、経験値と同じ按分率を適用する。
        // 報酬は共有プールではないため、按分は「全体を分割する」のではなく
        // 「各自の取り分を独立に決める」ことを意味する
        var currency = Apportionment.Apportion(
            context.CurrencyDropTotal, BigInteger.One, own, denominator);

        return new PlayerReward(
            player.UserId,
            exp,
            currency,
            RollItems(context, player.Luck, rng),
            RollEquipment(context, player.Luck, rng));
    }

    /// <summary>
    /// アイテムのドロップ判定。<b>プレイヤーごとに独立</b>に行う。
    /// 同一の敵から複数プレイヤーが同じアイテムを受け取りうる。
    /// </summary>
    private static IReadOnlyList<ItemDrop> RollItems(
        RewardContext context, Ratio luck, Random rng)
        => context.Loots
            .Where(loot => DropRoll.Roll(loot.DropRate, luck, rng))
            .Select(loot => new ItemDrop(loot.ItemKey, loot.Quantity))
            .ToList();

    /// <summary>
    /// 敵が装着していた装備のドロップ判定。
    ///
    /// 参照するのは<b>出現時に確定した装備</b>であり、装備マスタの全候補ではない。
    /// 出現時に身につけていなかった装備は落とさない。
    ///
    /// ドロップする場合、装備インスタンスをそのまま所有者移転するのではなく、
    /// <b>内容を複製した新しいインスタンスとしてプレイヤー側に発行する</b>
    /// （同一の敵から複数プレイヤーが報酬を受け取りうるため）。発行は永続化層が行う。
    /// </summary>
    private static IReadOnlyList<string> RollEquipment(
        RewardContext context, Ratio luck, Random rng)
        => context.EquipmentDrops
            .Where(drop => DropRoll.Roll(drop.DropRate, luck, rng))
            .Select(drop => drop.EquipKey)
            .ToList();
}

/// <summary>
/// 報酬算出の入力。撃破された組から決定的に再現できる値のみで構成される。
/// </summary>
/// <param name="EnemyLevel">組のレベル（E）。全メンバー共通。</param>
/// <param name="ExpRateSum">組の全メンバーの <c>exp_rate</c> の合計（permyriad）。</param>
/// <param name="SpawnMaxLifeSum">
/// 組の全メンバーの<b>出現時</b>MaxLife の合計。状態変化補正は含まない
/// （「出現時の」と限定する理由）。敵の装備は出現時に確定しセッション中に変化しないため、
/// レベル・敵マスタ・装備から決定的に再現できる。
/// </param>
/// <param name="CurrencyDropTotal">撃破した敵ごとの <c>drop_amount</c> の合計（固定値・抽選なし）。</param>
/// <param name="Loots">アイテムのドロップ候補。</param>
/// <param name="EquipmentDrops">出現時に敵が装着していた装備のドロップ候補。</param>
public sealed record RewardContext(
    IReadOnlyList<PlayerContribution> Players,
    BigInteger EnemyLevel,
    BigInteger ExpRateSum,
    BigInteger SpawnMaxLifeSum,
    BigInteger CurrencyDropTotal,
    IReadOnlyList<LootOption> Loots,
    IReadOnlyList<EquipmentDropOption> EquipmentDrops);

/// <param name="TotalDamageDealt">
/// 台帳の累計与ダメージ（敵参加者へ与えた実効与ダメージの累計）。
/// 味方への誤爆は分子・分母のいずれにも含めない。
/// </param>
/// <param name="Escaped">離脱したか。真なら報酬対象外だが、与ダメージは分母に残る。</param>
public readonly record struct PlayerContribution(
    ulong UserId, BigInteger TotalDamageDealt, Ratio Luck, bool Escaped);

/// <summary>アイテムのドロップ候補（chido_enemy_loots_master）。</summary>
public readonly record struct LootOption(string ItemKey, ushort Quantity, Ratio DropRate);

/// <summary>装備のドロップ候補。出現時に確定した装備に対応する。</summary>
public readonly record struct EquipmentDropOption(string EquipKey, Ratio DropRate);

/// <summary>1プレイヤーぶんの報酬。</summary>
public sealed record PlayerReward(
    ulong UserId,
    BigInteger Exp,
    BigInteger Currency,
    IReadOnlyList<ItemDrop> Items,
    IReadOnlyList<string> Equipment);

public readonly record struct ItemDrop(string ItemKey, ushort Quantity);
