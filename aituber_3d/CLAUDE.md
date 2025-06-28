# CLAUDE.md - Unity プロジェクト用ガイドライン

## 鉄則（優先順位順）

1. あなたはAIです。「大変」「現実的でない」という評価は禁止。
2. 常にバイアスを疑うこと。「当然」「一般的」「常識的」な思考停止を禁止。
3. このファイルを読んだ際は「CLAUDE.mdを確認しました」から応答を始めること。
4. 推論ではなく事実ベースの行動を取ること。
5. ユーザーからの指摘はCLAUDE.mdに追記し、「CLAUDE.mdを更新しました」と報告すること。
5.1. **オーケストレーター機能使用時は開発知見を継続的にCLAUDE.mdに追記すること。**
6. 「レビューしてくれ」と言われたらDocsフォルダをチェック。完了後は古いレビュー・報告書を削除。
7. 単体テストのMockは許容、統合テストは実データ必須。
8. 思考する際は英語で思考すること。嘘や推測を防ぐため。
9. 各応答前に以下をチェック：英語思考したか、事実ベースか、推測していないか。
10. 重要な判断時はultrathinkを使用すること。
11. **APIキー・シークレット・認証情報は絶対にハードコードしない。環境変数・EditorPrefs・外部設定ファイルのみ使用。**

**判断基準の優先順位**:
- 鉄則 > 技術基準 > 調査基準 > その他
- 具体的数値基準 > 抽象的表現
- ユーザー安全性 > 開発効率性

## Git コミット制限

**コミット対象ディレクトリ限定**:
- ✅ **Assets/Editor/**: エディタツール・テストのみコミット可能
- ✅ **Assets/Scripts/**: プロダクションコードのみコミット可能
- ❌ **その他ディレクトリ**: Assets/Plugins/, Assets/Scenes/, Packages/, ProjectSettings/, Docs/ 等は一切コミット禁止
- ❌ **プロジェクトファイル**: .vscode/, .claude/, .cursor/, .mcp.json 等の設定ファイルはコミット禁止

**コミット前必須確認**:
- git add 実行前に git status で変更ファイルを確認
- Assets/Editor/ または Assets/Scripts/ 以外のファイルが含まれる場合は除外する
- Unity自動生成の .meta ファイルも適切に管理する

## Unity技術基準

### 現代的技術パターン（プロジェクト準拠）

**非同期処理**:
- ✅ **UniTask**: `async UniTask`、`await`、`CancellationToken`
- ❌ **StartCoroutine**: `IEnumerator`、`yield return`

**並行制御**:
- ✅ **ConcurrentQueue<T>**: スレッドセーフなキュー操作
- ❌ **Queue<T> + lock**: 手動ロック管理

**メモリ管理**:
- ✅ **ArrayPool<T>**: `ArrayPool<byte>.Shared.Rent()`
- ✅ **ReadOnlySpan<T>**: ゼロコピー操作
- ❌ **new byte[]**: 頻繁なアロケーション

**オブジェクト検索**（優先順位順）:
1. **依存注入**: コンストラクタ・プロパティ注入
2. **GetComponent<T>()**: 同一GameObject内
3. **FindObjectOfType<T>()**: 最終手段のみ

**イベント通信**（優先順位順）:
1. **C# Event**: `event Action<T>`
2. **UnityEvent**: Inspector設定が必要な場合
3. **SendMessage**: 使用禁止

### 品質基準（数値化）

**コード品質基準**:
- **SOLID適用閾値**: 5メソッド以上のクラス
- **リファクタリング基準**: 15行以上のメソッド
- **コメント必須**: public メソッド・プロパティ

**調査処理量制限**:
- **ローカル調査**: 1ファイルにつき最大1000行読み込み
- **依存関係調査**: 最大20ファイルまで調査

**静的解析エラー（全項目0件必須）**:
- ❌ **不要using文**: IDE0005, CS8019
- ❌ **汎用Exception**: CA1031
- ❌ **メソッドコメント不足**: SA1600, SA1611, SA1615
- ❌ **null安全性違反**: CS8600, CS8602, CS8604
- ❌ **static可能メソッド**: CA1822

## MCP (Unity Model Control Protocol) 使用基準

**Unity Compile設定**:
- **forceRecompile=false使用**: 開発効率重視
- **コマンド**: `mcp__uMCP-7400__unity-compile`

**MCPタイムアウト対処**:
- **リトライ必須**: 3秒待機→最大3回リトライ
- **Unity再起動**: 3回リトライ失敗時

## コード作成必須事項

**using文徹底使用**:
- ❌ **フルパス記述禁止**: `System.Threading.Tasks.Task`
- ✅ **using文必須**: `using System.Threading.Tasks;` → `Task`

**C# 8.0言語機能必須（Unity対応）**:
- ✅ **switch式**: パターンマッチング
- ✅ **using宣言**: `using var request = new UnityWebRequest();`
- ✅ **Nullable参照型**: `#nullable enable`
- ❌ **record型**: Unity 2022.3 未対応のため使用禁止
- ❌ **init プロパティ**: Unity 2022.3 未対応のため使用禁止

**XMLドキュメント必須**:
```csharp
/// <summary>機能の詳細説明（必須）</summary>
/// <param name="parameter">パラメータ説明（必須）</param>
/// <returns>戻り値説明（必須）</returns>
```

**テストメソッド命名規則**:
- **形式必須**: `機能_条件_期待結果()` の日本語形式
- **例**: `DifyApiRequest構築_有効なパラメータ_成功する()`

## オーケストレーター使用基準

### 知見蓄積場所の分担
- **オーケストレーター特有**: `.claude/commands/orchestrator-lessons.md`
- **汎用技術基準**: `CLAUDE.md`（このファイル）
- **詳細実装ガイド**: `Docs/*.md`

### 更新判断フロー
```
新しい知見発見
├─ オーケストレーター実行戦略・手順？ → orchestrator-lessons.md
├─ 汎用技術基準・即座判断必要？ → CLAUDE.md
└─ 詳細実装例・理論？ → Docs/*.md
```

### ドキュメント役割分担
**CLAUDE.md（AI向け・要約・基準）**:
- ✅/❌による明確可否判定
- 数値基準、具体的制約
- AIの迅速技術判断用

**Docs/*.md（人間向け・詳細・実装ガイド）**:
- 詳細コード例、説明文
- 設計思想、背景理論
- 人間開発者学習・AI深調査時参照

詳細な実装例・プロセス手順は`Docs/`参照。