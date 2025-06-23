# わんコメ-Dify統合 改造プラン

## 概要

わんコメ（OneComme）とDifyを統合して、YouTube Liveコメントに対してAI応答を生成するシステムを構築する実装プランです。既存システムの重要機能を維持しながら、段階的にDifyと統合します。

## API仕様調査結果

### わんコメ（OneComme）WebSocket API

**参考ドキュメント**: [OneComme WebSocket API](https://onecomme.com/docs/developer/websocket-api)

- **接続先**: `ws://127.0.0.1:11180/sub?p=comments,config`
- **データ形式**: `{ "type": "event_name", "data": { /* event-specific data */ } }`
- **主要イベント**:
  - `"connected"`: 初期接続イベント
  - `"comments"`: 新しいコメント受信
  - `"config"`: 設定更新
  - `"meta"`: 配信メタデータ変更

### Dify API

**参考ドキュメント**: 
- [Dify API Documentation](https://docs.dify.ai/en/guides/application-publishing/developing-with-apis)
- [Real-Time Speech with Dify API](https://dev.to/ku6ryo/how-to-realize-real-time-speech-with-dify-api-4ii1)

- **エンドポイント**: `POST /v1/chat-messages` (TTS対応)
- **リクエスト形式**:
```json
{
  "inputs": {},
  "query": "コメント内容",
  "response_mode": "streaming",
  "conversation_id": "",
  "user": "user-id",
  "files": []
}
```
- **レスポンス形式**: Server-Sent Events (SSE) ✅ **検証済み** (2025-06-20)
- **主要イベント**:
  - `"workflow_started"`: ワークフロー開始
  - `"node_started"` / `"node_finished"`: 各ノードの実行状態
  - `"message"`: テキストメッセージ（トークン単位でストリーミング）
  - `"message_end"`: メッセージ完了
  - `"tts_message"`: 音声データ（TTS有効時）
  - `"error"`: エラー
- **TTS機能**: 
  - **Base64形式**: `"audio"` プロパティにMP3データをbase64エンコード
  - **Cartesiaプラグイン**: インストール済み、要TTS出力検証

## API検証結果 (2025-06-20)

### ✅ 検証完了項目
- **認証**: Bearer token認証 (`app-xxxxxxxxxxxxxxxxxxxxxxxx`)
- **基本応答**: 日本語テキスト生成正常動作
- **ストリーミング**: SSE形式でトークン単位配信
- **ワークフロー**: 公開済みワークフロー実行可能
- **エラーハンドリング**: 未公開時 `"Workflow not published"` エラー

### ✅ 追加検証完了項目 (2025-06-20)
- **TTS出力**: Cartesiaプラグインからの音声データ受信 ✅
- **Base64音声**: MP3データがbase64エンコードで配信 ✅
- **ストリーミングTTS**: 音声データが分割してリアルタイム配信 ✅

### 🔄 検証待ち項目
- **会話継続**: conversation_idを使った対話履歴管理
- **ファイル添付**: filesパラメータでの画像・文書送信

### 実際のAPI応答例
```json
{"event": "message", "answer": "こんにちは", "from_variable_selector": ["llm", "text"]}
{"event": "message", "answer": "。", "from_variable_selector": ["llm", "text"]}
{"event": "message", "answer": "私は", "from_variable_selector": ["llm", "text"]}
```

## 現在のアーキテクチャ

### データフロー（現状）
```
わんコメ WebSocket → GetCommentFromOne.cs → http://localhost:7200/youtube/chat_message
                          ↓
YouTubeChatDisplay.FetchCommentAsync → http://localhost:7200/youtube/chat_message (定期取得)
                          ↓
QueueManager.ProcessInputQueueAsync → http://localhost:7200/filter (最大10件まとめて)
                          ↓
QueueManager.ProcessReplyGenerateAsync → http://localhost:7200/reply
                          ↓
TextToSpeech.monitorConversationQueue → http://localhost:7200/voice (並行処理)
                          ↓
TextToSpeech.preparedSpeeches → 音声再生
```

### 重要機能の確認

**YouTubeChatDisplay.cs** (Assets/Scripts/Views/YouTubeChatDisplay.cs)：
- 禁止ワードフィルタリング機能 (line:125-133)
- Resources/Textからのストップワード読み込み (line:88-104)
- QueueManagerとの統合 (line:128)
- YouTube API直接統合

## 新しいアーキテクチャ（段階的統合）

### 実装戦略更新 (2025-06-20)

**TDD対応設計に変更**:
- Pure C# + Interface設計でエディタプレイなしのユニットテスト対応
- MonoBehaviourは最小限のアダプターのみ
- 詳細は「Unity-Dify統合_TDD実装プラン.md」を参照

### Dify統合データフロー
```
わんコメ WebSocket → GetCommentFromOne.cs → DifyQueueManager.cs（キュー蓄積）
                                               ↓
                          [バッチ処理] → Dify API（フィルタ + AI応答 + TTS音声）
                                               ↓
                          [音声データ受信] → AudioProcessor.cs → TextToSpeech.preparedSpeeches → 音声再生
```

### 統合アプローチ
1. **段階的移行**: 既存システムと並行運用しながら徐々に統合
2. **機能保持**: YouTubeChatDisplayの禁止ワード機能等を維持
3. **ハイブリッド運用**: わんコメ + YouTubeChatDisplay の併用

## 新規作成コンポーネント

### A. DifyQueueManager.cs (Unity Adapter - 最小限)
```csharp
/// <summary>
/// わんコメからDifyへのバッチ処理を制御するマネージャー
/// Pure C# DifyServiceへの薄いアダプター層
/// 段階的移行のためのパフォーマンス統計機能を内蔵
/// </summary>
public class DifyQueueManager : MonoBehaviour
{
    [Header("Dify統合設定")]
    public bool enableDifyIntegration = false;  // 段階的切り替え用
    public float difyProcessingInterval = 2.0f; // バッチ処理間隔
    
    private DifyService difyService;            // Pure C# Service
    private ConcurrentQueue<CommentData> difyInputQueue = new ConcurrentQueue<CommentData>();
    
    async UniTask ProcessDifyQueue()
    {
        // DifyServiceに委譲
        var result = await difyService.ProcessCommentAsync(comment, userId);
        
        // Unity固有処理（AudioClip再生等）のみここで実行
        await PlayAudioClip(result.AudioData);
    }
}
```

### B. DifyService.cs (Pure C# - TDD対象)
```csharp
/// <summary>
/// Dify API統合のコアビジネスロジック
/// 完全にUnity非依存、モック可能
/// </summary>
public class DifyService
{
    private readonly IDifyApiClient apiClient;
    private readonly IAudioStreamHandler audioHandler;
    
    public async Task<DifyProcessingResult> ProcessCommentAsync(string comment, string userId)
    {
        // 1. APIリクエスト作成
        // 2. SSE受信・パース
        // 3. 音声データ処理
        // 4. 結果返却
    }
}
```

### C. AudioProcessor.cs → AudioStreamHandler.cs (Pure C#)
```csharp
/// <summary>
/// Base64 MP3ストリーミング音声データ処理
/// Unity AudioClip非依存の音声データ処理
/// </summary>
public class AudioStreamHandler : IAudioStreamHandler
{
    public byte[] ProcessBase64Audio(string base64Audio)
    {
        // Base64デコード
        // MP3フォーマット検証
        return Convert.FromBase64String(base64Audio);
    }
    
    public bool ValidateAudioFormat(byte[] audioData)
    {
        // MP3ヘッダー検証
    }
}
```

## 段階的統合戦略

### Phase 1: Pure C# 基盤構築
- DifyApiRequest/Response データクラス
- IDifyApiClient インターフェース定義
- SSEParser 静的クラス実装
- Unit Tests作成

### Phase 2: Infrastructure実装
- DifyApiClient HTTP通信実装
- AudioStreamHandler 音声処理実装
- Mock使用Unit Tests

### Phase 3: Service Layer
- DifyService ビジネスロジック実装
- 統合テスト（実際のDify API使用）

### Phase 4: Unity統合
- DifyQueueManager（薄いアダプター）実装
- 既存システムとの並行動作検証

### Phase 5: 本格運用
- エラーハンドリング強化
- パフォーマンス最適化
- プロダクション環境対応

## 品質保証

### TDD開発フロー
1. **Red**: テスト作成 → 失敗確認
2. **Green**: 最小実装 → テスト通過
3. **Refactor**: リファクタリング → テスト保持

### テスト戦略
- **Unit Tests**: Pure C# レイヤー（Editor Tests）
- **Integration Tests**: 実際のDify API使用
- **E2E Tests**: Unity環境での統合動作確認

### パフォーマンス指標
- Unit Tests実行時間: < 1秒
- Integration Tests: < 10秒
- カバレッジ目標: Pure C# Layer 90%以上

## 従来システムとの互換性

### 既存機能の保持
- **YouTubeChatDisplay**: 禁止ワード機能維持
- **QueueManager**: UI更新ロジック継続使用
- **TextToSpeech**: 音声再生インフラ活用

### 段階的移行
1. **Phase 1-3**: 既存システム + Dify並行動作
2. **Phase 4**: 性能比較・検証
3. **Phase 5**: 段階的切り替え（設定フラグ制御）

## 開発環境

### 必要ツール
- Unity Test Runner
- NUnit Framework
- Mock Framework (Moq等)
- Dify Local Environment

### 開発順序
1. TDD環境構築
2. Pure C# 基盤実装
3. Unity統合
4. E2E検証
5. プロダクション対応

---

## 更新履歴
- 2025-06-20: 初版作成
- 2025-06-20: API検証結果追加
- 2025-06-20: TDD対応設計に変更