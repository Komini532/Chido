using System;
using System.Collections.Generic;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Entities.Enemies;

/// <summary>
/// 敵。種族ごとにクラスを増やすとクラス数が際限なく膨らむため、
/// 敵マスタの内容を流し込む単一のクラスとして表現する（データ駆動＋継承のハイブリッド。戦闘システム 3.1）。
///
/// 出現の都度、新しいインスタンスとして生成される。前の出現インスタンスを引き継ぐ経路は存在せず、
/// HP全快・装備再抽選・auto付与の状態変化の再適用がすべて新規に行われる（戦闘システム 10.3）。
/// </summary>
public sealed class Enemy : EntityBase
{
    /// <summary>chido_enemy_master.enemy_key。どの種族かを示す。</summary>
    public string MasterKey { get; }

    public override string Name { get; }

    /// <summary>
    /// レベル。出現時の累積敵レベルをそのまま複製した値であり、経験値からは導出しない。
    /// 組の全メンバーが同一レベルとなる（強さの差は Shape と強さ倍率で表現する）。
    /// </summary>
    public override BigInteger Level { get; }

    protected override StatShape Shape { get; }

    protected override Ratio StrengthRate { get; }

    protected override int BaseSpeed { get; }

    protected override Element InnateElements { get; }

    /// <summary>経験値倍率。強さ倍率とは独立した値で、報酬の基礎経験値の算出に使う。</summary>
    public Ratio ExpRate { get; }

    /// <summary>
    /// 出現時の初期TP。プレイヤーは常に0で初期化されるため、この非対称は意図的
    /// （初手から require_tp&gt;0 のスキルを撃たせたい敵のための拡張）。
    /// </summary>
    public ushort InitialTp { get; }

    /// <summary>スキルの選択規則。</summary>
    public ActionPatternType ActionPatternType { get; }

    /// <summary>味方対象モーションの対象選択規則。<see cref="ActionPatternType"/> と対をなす。</summary>
    public AllyTargetRule AllyTargetRule { get; }

    /// <summary>
    /// 保有スキル（chido_enemy_skills_master の登録行）。登録順がローテーションの順序になる。
    ///
    /// 件数は<b>戦闘中は不変</b>であり（いかなる要因でも敵の登録スキル数は変動しない）、
    /// ローテーションの法 <c>total</c> はこの件数を指す。1件も持たない場合は通常攻撃へフォールバックする。
    /// </summary>
    public IReadOnlyList<EnemySkillEntry> Skills { get; }

    public Enemy(
        string masterKey,
        string name,
        BigInteger level,
        StatShape shape,
        Ratio strengthRate,
        Ratio expRate,
        int baseSpeed,
        Element innateElements = Element.None,
        ushort initialTp = 0,
        ActionPatternType actionPatternType = ActionPatternType.PureRandom,
        AllyTargetRule allyTargetRule = AllyTargetRule.PureRandom,
        IEnumerable<EnemySkillEntry>? skills = null,
        Guid? entityId = null)
    {
        Skills = skills is null ? [] : [.. skills];

        MasterKey = masterKey;
        Name = name;
        Level = level;
        Shape = shape;
        StrengthRate = strengthRate;
        ExpRate = expRate;
        BaseSpeed = baseSpeed;
        InnateElements = innateElements;
        InitialTp = initialTp;
        ActionPatternType = actionPatternType;
        AllyTargetRule = allyTargetRule;

        if (entityId is { } id) Id = id;
    }
}
