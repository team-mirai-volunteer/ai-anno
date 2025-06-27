using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Networking;
using AiTuber.Services.Legacy.Dify;
using AiTuber.Services.Legacy.Dify.Data;

namespace AiTuber.Tests.Legacy.Dify
{
    /// <summary>
    /// Dify実サーバー統合テスト
    /// ローカルDifyサーバー (localhost:60606) との実際の通信をテスト
    /// 
    /// 実行前提条件:
    /// 1. Difyサーバーがローカルで起動している (docker compose up)
    /// 2. APIキーが有効である
    /// 
    /// テスト実行コマンド例:
    /// cd ../dify && docker compose up -d
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    [Category("RequiresDifyServer")]
    public class DifyIntegrationTests
    {
        private DifyApiClient _apiClient;
        private DifyServiceConfig _config;
        private DifyService _difyService;
        
        // 設定はDifyEditorSettingsから読み込み（EditorPrefs）
        private string DIFY_API_KEY => AiTuber.Editor.Dify.DifyEditorSettings.ApiKey;
        private string DIFY_API_URL => AiTuber.Editor.Dify.DifyEditorSettings.ApiUrl;

        [SetUp]
        public void SetUp()
        {
            // DifyEditorSettingsから設定を検証
            ValidateIntegrationTestConfiguration();
            
            // Difyサーバー接続可能性を事前チェック
            CheckDifyServerAvailability();
            
            // 実際のAPIクライアント
            _apiClient = new DifyApiClient
            {
                ApiKey = DIFY_API_KEY,
                ApiUrl = DIFY_API_URL,
            };

            // サービス設定
            _config = new DifyServiceConfig
            {
                ApiKey = DIFY_API_KEY,
                ApiUrl = DIFY_API_URL,
                EnableAudioProcessing = true,
            };

            _difyService = new DifyService(_apiClient, _config);
        }
        
        /// <summary>
        /// 統合テスト実行前の設定検証
        /// DifyEditorSettingsの設定が有効かチェック
        /// </summary>
        private void ValidateIntegrationTestConfiguration()
        {
            // APIキー設定状況の確認
            var apiKey = DIFY_API_KEY;
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[DifyIntegration] ❌ API Key is not set. Please configure via Window > AiTuber > Dify Editor Tool.");
            }
            else
            {
                Debug.Log($"[DifyIntegration] ✅ API Key configured (length: {apiKey.Length} chars)");
            }
            
            // 設定の有効性チェック
            if (!AiTuber.Editor.Dify.DifyEditorSettings.IsValid())
            {
                var errors = AiTuber.Editor.Dify.DifyEditorSettings.ValidateConfiguration();
                var errorMessage = string.Join(", ", errors);
                Assert.Ignore($"Integration tests require valid Dify configuration. " +
                             $"Please configure via Window > AiTuber > Dify Editor Tool. " +
                             $"Errors: {errorMessage}");
            }
            
            // APIキーの基本チェックのみ
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning($"[DifyIntegration] ❌ API Key is not set. Skipping integration tests.");
                Assert.Ignore("Integration tests require valid API key. " +
                             "Please configure via Window > AiTuber > Dify Editor Tool.");
            }
            
            Debug.Log($"[DifyIntegration] Using API configuration from DifyEditorSettings: {DIFY_API_URL}");
        }
        
        private void CheckDifyServerAvailability()
        {
            var testUrl = DIFY_API_URL.Replace("/v1/chat-messages", "/");
            Debug.Log($"[DifyIntegration] Checking server availability at: {testUrl}");
            
            // 簡易的な接続チェック（UnityWebRequestを使用）
            // Difyはhealthエンドポイントがないので、GETでルートを確認
            using (var request = UnityWebRequest.Get(testUrl))
            {
                request.timeout = 2; // 2秒でタイムアウト
                var operation = request.SendWebRequest();
                
                // 同期的に待機（テストSetUp内なので許容）
                while (!operation.isDone)
                {
                    System.Threading.Thread.Sleep(10);
                }
                
                // 詳細なログ出力
                Debug.Log($"[DifyIntegration] Server response: {request.responseCode} - {request.result}");
                
                // 何らかのレスポンスがあればOK（404含む）
                if (request.result == UnityWebRequest.Result.ConnectionError || 
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogError($"[DifyIntegration] ❌ Dify server is not available at {DIFY_API_URL}");
                    Debug.LogError($"[DifyIntegration] Connection error: {request.error}");
                    Debug.LogError($"[DifyIntegration] Please start the server with: cd ../dify && docker compose up -d");
                    
                    Assert.Ignore($"Dify server is not available at {DIFY_API_URL}. " +
                                $"Please start the server with: cd ../dify && docker compose up -d");
                }
                else
                {
                    Debug.Log($"[DifyIntegration] ✅ Server is available (Response: {request.responseCode})");
                }
            }
        }

        [Test]
        [Ignore("Token cost optimization - temporarily disabled")]
        public void 統合テスト認識確認_常にパス_テスト()
        {
            Debug.Log("[DifyIntegration] Simple test method executed");
            Assert.Pass("Integration test class is recognized");
        }

        [UnityTest]
        [Category("Integration")]
        [Ignore("Token cost optimization - temporarily disabled")]
        public IEnumerator 実際のDifyサーバーへの接続テスト()
        {
            Debug.Log("[DifyIntegration] Testing connection to real Dify server...");
            
            var task = _difyService.TestConnectionAsync();
            
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            if (task.Exception != null)
            {
                Debug.LogError($"[DifyIntegration] Connection test failed: {task.Exception.GetBaseException().Message}");
            }
            
            Assert.IsTrue(task.Result, "Failed to connect to Dify server at " + DIFY_API_URL);
            Debug.Log("[DifyIntegration] Connection test successful!");
        }

        [UnityTest]
        [Category("Integration")]
        [Ignore("Token cost optimization - temporarily disabled")]
        public IEnumerator シンプルな質問処理テスト()
        {
            Debug.Log("[DifyIntegration] Testing simple query processing...");
            
            var task = _difyService.ProcessUserQueryAsync(
                "こんにちは、今日はいい天気ですね。", 
                "test-user-001",
                conversationId: null,
                onStreamEvent: null,
                cancellationToken: default
            );
            
            var timeout = Time.realtimeSinceStartup + 30;
            while (!task.IsCompleted && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            
            Assert.IsTrue(task.IsCompleted, "Task did not complete within timeout");
            
            if (task.Exception != null)
            {
                Debug.LogError($"[DifyIntegration] Query processing failed: {task.Exception.GetBaseException().Message}");
                Assert.Fail($"Exception occurred: {task.Exception.GetBaseException().Message}");
            }
            
            var result = task.Result;
            
            // 基本的な検証
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.IsSuccess, $"Processing failed: {result.ErrorMessage}");
            Assert.IsTrue(result.HasTextResponse, "Should have text response");
            Assert.IsFalse(string.IsNullOrEmpty(result.ConversationId), "Should have conversation ID");
            Assert.IsFalse(string.IsNullOrEmpty(result.MessageId), "Should have message ID");
            Assert.Greater(result.ProcessingTimeMs, 0, "Processing time should be positive");
            
            Debug.Log($"[DifyIntegration] Response: {result.TextResponse}");
            Debug.Log($"[DifyIntegration] Conversation ID: {result.ConversationId}");
            Debug.Log($"[DifyIntegration] Processing time: {result.ProcessingTimeMs}ms");
            Debug.Log($"[DifyIntegration] Event count: {result.EventCount}");
            Debug.Log($"[DifyIntegration] Has text: {result.HasTextResponse}, Text length: {result.TextResponse?.Length ?? 0}");
            
            // 音声データの検証（有効な場合）
            if (result.HasAudioData)
            {
                Assert.IsNotNull(result.AudioData, "Audio data should not be null");
                Assert.Greater(result.AudioData.Length, 0, "Audio data should not be empty");
                Debug.Log($"[DifyIntegration] Audio data received: {result.AudioData.Length} bytes");
            }
        }

        [UnityTest]
        [Category("Integration")]
        [Ignore("Token cost optimization - temporarily disabled")]
        public IEnumerator ストリーミングモード複数イベント受信テスト()
        {
            Debug.Log("[DifyIntegration] Testing streaming mode...");
            
            
            // カスタムイベントハンドラーを持つAPIクライアント
            var streamingClient = new DifyApiClient
            {
                ApiKey = DIFY_API_KEY,
                ApiUrl = DIFY_API_URL,
            };
            
            var streamingService = new DifyService(streamingClient, _config);
            
            var task = streamingService.ProcessUserQueryAsync(
                "AIについて3つの重要なポイントを教えてください。", 
                "test-user-003",
                conversationId: null,
                onStreamEvent: null,
                cancellationToken: default
            );
            
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            Assert.IsTrue(task.Result.IsSuccess, "Streaming query should succeed");
            Assert.Greater(task.Result.EventCount, 1, "Should receive multiple events in streaming mode");
            
            Debug.Log($"[DifyIntegration] Received {task.Result.EventCount} events");
            Debug.Log($"[DifyIntegration] Full response: {task.Result.TextResponse}");
        }

        [UnityTest]
        [Category("Integration")]
        [Ignore("Token cost optimization - temporarily disabled")]
        public IEnumerator 無効なAPIキーエラーハンドリングテスト()
        {
            Debug.Log("[DifyIntegration] Testing invalid API key handling...");
            
            // 無効なAPIキーでクライアント作成
            var invalidClient = new DifyApiClient
            {
                ApiKey = "invalid-api-key-123",
                ApiUrl = DIFY_API_URL,
            };
            
            var invalidConfig = new DifyServiceConfig
            {
                ApiKey = "invalid-api-key-123",
                ApiUrl = DIFY_API_URL,
            };
            
            var invalidService = new DifyService(invalidClient, invalidConfig);
            
            var task = invalidService.ProcessUserQueryAsync(
                "This should fail", 
                "test-user-005",
                conversationId: null,
                onStreamEvent: null,
                cancellationToken: default
            );
            
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            Assert.IsFalse(task.Result.IsSuccess, "Should fail with invalid API key");
            Assert.IsNotNull(task.Result.ErrorMessage, "Should have error message");
            
            Debug.Log($"[DifyIntegration] Expected error: {task.Result.ErrorMessage}");
        }

        [TearDown]
        public void TearDown()
        {
            _apiClient = null;
            _difyService = null;
        }
    }
}