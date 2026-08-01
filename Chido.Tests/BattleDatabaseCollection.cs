using Chido.Data.Tests;
using Xunit;

namespace Chido.Tests;

/// <summary>
/// 本アセンブリ専用の実DBフィクスチャ。
///
/// <para>
/// <b>接続先のデータベースを <c>Chido.Data.Tests</c> と分ける。</b>フィクスチャは実行のたびに
/// <c>EnsureDeleted</c> でデータベースごと破棄するため、同じDBを指したまま2つのテスト
/// アセンブリが並行して走ると、片方が破棄している最中にもう片方のテストが実行されうる。
/// <c>DisableParallelization</c> はアセンブリ内にしか効かないため、接続先を分けて
/// 構造的に衝突しないようにしている。
/// </para>
/// </summary>
public sealed class BattleDatabaseFixture : DatabaseFixture
{
    protected override string? DatabaseSuffix => "_battle";
}

/// <summary>
/// 実DBに接続するテストクラスをまとめるコレクション。
///
/// <para>
/// コレクション定義はアセンブリごとに解決されるため、参照先の <c>DatabaseCollection</c> を
/// そのまま使うことはできない（フィクスチャが供給されない）。本アセンブリ側に定義を別途置く。
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BattleDatabaseCollection : ICollectionFixture<BattleDatabaseFixture>
{
    public const string Name = "battle-database";
}

/// <summary>
/// 出荷するマスタデータの検証専用のフィクスチャ。
///
/// <para>
/// <b>戦闘オーケストレーションの検証とは接続先を分ける。</b>出荷するマスタは草原の
/// <c>Common</c> に自前の組を紐づけるが、<c>BattleWorld</c> の試験用マスタも同じ枠を使う。
/// 同じDBに同居させると、組の抽選がどちらを引くかで結果が変わり、
/// 「1発で沈む敵」を前提にした検証が抽選の運次第で崩れる。
/// </para>
/// <para>
/// どちらか一方の投入をもう一方に合わせる案は採らない。出荷するマスタは
/// <b>それだけで成立すること</b>を確かめる対象であり、試験用の行が混ざった状態で
/// 通っても確かめたことにならない。
/// </para>
/// </summary>
public sealed class MasterDatabaseFixture : DatabaseFixture
{
    protected override string? DatabaseSuffix => "_master";
}

/// <summary>出荷するマスタデータの検証をまとめるコレクション。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MasterDatabaseCollection : ICollectionFixture<MasterDatabaseFixture>
{
    public const string Name = "master-database";
}
