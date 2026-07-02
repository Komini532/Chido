using Chido.Core.Entities;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 戦闘用アイテムが対象 (自身/味方) に及ぼす効果。アイテムマスタのカラム構成は未確定 (設計ドキュメント 10.4) のため、
/// アイテムデータの解決は呼び出し側に委ね、UseAction はこのインターフェイスを通じて効果適用とログ生成のみを担う。
/// </summary>
public interface IBattleItemEffect
{
    string Apply(IEntity user, IEntity target);
}
