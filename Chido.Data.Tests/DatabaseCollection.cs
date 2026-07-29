using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 実DBに接続するテストクラスをまとめるコレクション。
///
/// <para>
/// <b>実DBテストは必ず本コレクションに属させること。</b>
/// <see cref="DatabaseFixture"/> を <c>IClassFixture</c> で受けるとテストクラスごとに
/// フィクスチャが作られ、クラスごとに <c>EnsureDeleted</c> → <c>Migrate</c> が走る。
/// xUnit はテストクラスをコレクション単位で並列実行するため、あるクラスがデータベースを
/// 破棄している最中に別クラスのテストが走り、原因の分かりにくい失敗を生む。
/// </para>
/// <para>
/// <c>ICollectionFixture</c> にすることで、スキーマの準備はテスト実行を通じて1回だけになり、
/// コレクション内のテストクラスは直列に実行される。
/// <c>DisableParallelization</c> は、DBを触らないモデルレベルのテストとの同時実行も止める
/// （それらは接続しないため実害は無いが、失敗時の切り分けを単純に保つ）。
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
