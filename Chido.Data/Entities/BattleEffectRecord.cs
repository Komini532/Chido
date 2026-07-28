using Chido.Core.Battle.Effects;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_effect (19): 状態変化保持（戦闘内スコープ）。
/// clear_on_battle_end=true の効果（Player/Enemy問わず）と、Enemy の全ての効果
/// （clear_on_battle_end の値に関わらず）がここに書き込まれ、戦闘終了時に除去される。
///
/// 重複付与時の挙動は「拒否」（アプリ側担保）。モーションは実行され accuracy_rate の判定も行われるが、
/// 状態変化の付与のみがスキップされ、既存インスタンスの remaining_actions は変更されない（延長しない）。
/// 重複の判定キーは entity_id + effect_key + affect_reason + granter_entity_id + grant_source_key の5値。
///
/// DBの UNIQUE では守れない: grant_source_key は affect_reason=Auto のとき NULL を取り、
/// MySQL の UNIQUE は NULL を互いに異なる値として扱うため NULL の行は何行でも入る。
/// 比較は必ず NULL安全等価（&lt;=&gt;）で行うこと。素直にパラメータ化すると
/// auto付与の状態変化だけが無制限に重複するという、テストで気づきにくいバグになる。
/// </summary>
public class BattleEffectRecord
{
    /// <summary>使い捨てGuid。1回の付与ごとに新規発行。併存インスタンスの発動順序のキーでもある。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>chido_battle_participant.entity_id を参照。効果保持者（Player/Enemy両方あり得る）。</summary>
    public Guid EntityId { get; set; }

    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>付与要因。grant_source_key が「何のキーであるか」を示す型タグ。</summary>
    public AffectReason AffectReason { get; set; }

    /// <summary>付与者のentity_id。auto付与時は entity_id と同値（自己付与）。</summary>
    public Guid GranterEntityId { get; set; }

    /// <summary>
    /// 識別キー。skill付与時は skill_key。auto付与時は NULL（付与元がスキルでないことを示す）。
    /// affect_reason は本列が何のキーであるかを示すものであり、本列からは導出できない。
    /// </summary>
    public string? GrantSourceKey { get; set; }

    /// <summary>
    /// 残り有効行動数。付与元（10c または 14番）の duration_actions を複製して初期化する。
    /// 保持者が1ターンに関与するごとに -1 し、0 に達した時点で消失する。
    /// 減衰の契機は時間の経過ではなく保持者がターンに関与したこと（時計ではなくカウンタの消費）。
    /// NULL = 無期限。SQLのNULL伝播により -1 の対象からも消失判定からも自動的に外れる。
    /// </summary>
    public ushort? RemainingActions { get; set; }
}
