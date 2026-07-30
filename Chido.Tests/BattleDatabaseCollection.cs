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
