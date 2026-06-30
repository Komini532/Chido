namespace RpgBot.Core.Battle.Damage;

public enum ModifierPhase
{
    /// <summary>防御計算前。ATK そのものへの乗算 (ATKバフ・デバフ・バーサク等)</summary>
    PreDefense = 0,

    /// <summary>防御差し引き後。スキル倍率・属性補正・クリティカル等 — 複数あれば乗算連鎖</summary>
    PostDefense = 1,

    /// <summary>乗算確定後のフラット加算 (固定ダメージ付与アイテム等)</summary>
    Flat = 2,

    /// <summary>最終クランプ・上限/下限調整</summary>
    Final = 3,
}
