using Chido.Core.Battle.Skills;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_skill_motion_master: スキルモーション（スーパータイプ）。
/// motion_type を判別子として4つのサブタイプテーブル（10a・10b・10c・10d）を持つ
/// （戦闘離脱のみ可変パラメータを持たずサブタイプなし）。
/// 全モーションが共通して持つ属性のみを本テーブルに置く。
/// </summary>
public class SkillMotionMasterRecord
{
    /// <summary>chido_skill_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>再生順序。スキルはこの昇順にモーションを再生する。</summary>
    public byte MotionIndex { get; set; }

    /// <summary>モーション種別。サブタイプの判別子であり、子テーブルと1対1に対応する（離脱を除く）。</summary>
    public MotionType MotionType { get; set; }

    /// <summary>対象の解決規則。影響範囲は常に単体固定であり「範囲」という概念は存在しない。</summary>
    public TargetRule TargetRule { get; set; }

    /// <summary>
    /// 命中率（攻撃・回復）／成功率（状態変化付与・解除・戦闘離脱）。
    /// 4種すべてが使用する真の共通列であるため親に置く。Attack/Defend のモーションは 10000 固定（運用制約）。
    /// </summary>
    public Ratio AccuracyRate { get; set; }

    /// <summary>
    /// 命中の依存グループ。NULL = 他モーションに依存せず単独で判定する。
    /// 同一 skill_key 内で同値の行が1グループを成し、motion_index 最小の行を先頭とする。
    /// 先頭が効果適用に到達しなかった場合、同一グループの他メンバーは抽選を行わずスキップされる。
    /// 「攻撃が命中したら n% で毒付与」のような道連れ失敗を表現する。
    /// 行間の関係を表す列であり単一行のCHECKでは守れないため、整合性検証はアプリ側の責務。
    /// </summary>
    public ushort? AccuracyGateGroup { get; set; }
}
