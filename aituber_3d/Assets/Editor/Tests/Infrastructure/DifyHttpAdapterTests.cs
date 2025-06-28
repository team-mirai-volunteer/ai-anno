using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using AiTuber.Services.Dify.Infrastructure.Http;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Domain.Entities;

namespace AiTuber.Tests.Dify.Infrastructure
{
    /// <summary>
    /// DifyHttpAdapter のユニットテスト
    /// Infrastructure層 Clean Architecture準拠
    /// Legacy DifyApiClientからのリファクタリング版
    /// Push型インターフェース対応テスト
    /// </summary>
    [TestFixture]
    public class DifyHttpAdapterTests
    {
        private DifyHttpAdapter _adapter;
        private MockHttpClient _mockHttpClient;
        private DifyConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _mockHttpClient = new MockHttpClient();
            UnityEngine.Debug.Log($"[TEST] SetUp: MockHttpClient created");
            _config = new DifyConfiguration(
                apiKey: "test-api-key-12345",
                apiUrl: "https://api.dify.ai/v1/chat-messages",
                enableAudioProcessing: true,
                enableDebugLogging: true
            );
            UnityEngine.Debug.Log($"[TEST] SetUp: DifyConfiguration created");
            _adapter = new DifyHttpAdapter(_mockHttpClient, _config);
            UnityEngine.Debug.Log($"[TEST] SetUp: DifyHttpAdapter created");
        }

        #region Constructor Tests

        [Test]
        public void DifyHttpAdapter作成_有効な依存関係_正常にインスタンス作成()
        {
            // Act & Assert
            Assert.IsNotNull(_adapter);
        }

        [Test]
        public void DifyHttpAdapter作成_NullHttpClient_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new DifyHttpAdapter(null, _config));
        }

        [Test]
        public void DifyHttpAdapter作成_Null設定_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new DifyHttpAdapter(_mockHttpClient, null));
        }

        [Test]
        public void DifyHttpAdapter作成_無効な設定_ArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                new DifyConfiguration("", "", false));
        }

        #endregion

        #region ExecuteStreamingAsync Tests

        [UnityTest]
        public IEnumerator ストリーミング実行_有効なリクエスト_成功レスポンスを返す()
        {
            // Arrange - 実際のDifyデータフォーマットで完全テスト
            var request = new DifyRequest("こんにちは", "test-user");
            var realConversationId = "1213080f-f863-4a53-9149-bbe05096f758";
            var realMessageId = "9e5da094-804f-4d84-a68d-26a59f158231";
            
            // 実際のDifySSEフォーマット（実データから抽出）
            var mockSseData = $"data: {{\"event\":\"message\",\"answer\":\"こんにちは\",\"audio\":\"\",\"conversation_id\":\"{realConversationId}\",\"message_id\":\"{realMessageId}\",\"taskId\":\"\"}}\n\n" +
                             $"data: {{\"event\":\"message_end\",\"answer\":\"\",\"audio\":\"\",\"conversation_id\":\"{realConversationId}\",\"message_id\":\"{realMessageId}\",\"taskId\":\"\"}}\n\n";
            
            _mockHttpClient.SetupStreamingResponse(mockSseData);

            // Act - 実際のDifyHttpAdapterを通した完全統合テスト
            QueryResponse result = null;
            var receivedEvents = new System.Collections.Generic.List<DifyStreamEvent>();
            
            yield return PerformAsyncOperation(
                () => _adapter.ExecuteStreamingAsync(
                    request, 
                    evt => {
                        UnityEngine.Debug.Log($"[TEST] Received event: {evt.EventType}");
                        receivedEvents.Add(evt);
                    }, 
                    CancellationToken.None),
                r => result = r);

            // Assert - DifyHttpAdapterの実際の処理結果を検証
            Assert.IsNotNull(result, "QueryResponse should not be null");
            Assert.IsTrue(_mockHttpClient.WasCalled, "MockHttpClient should have been called");
            Assert.IsTrue(result.IsSuccess, "Request should be successful");
            Assert.AreEqual("こんにちは", result.TextResponse, "TextResponse should match");
            Assert.AreEqual(realConversationId, result.ConversationId, "ConversationId should match");
            Assert.AreEqual(realMessageId, result.MessageId, "MessageId should match");
            Assert.AreEqual(2, receivedEvents.Count, "Should receive 2 events");
            Assert.IsTrue(receivedEvents[0].IsMessageEvent, "First event should be message event");
            Assert.IsTrue(receivedEvents[1].IsEndEvent, "Second event should be end event");
        }

        [UnityTest]
        public IEnumerator ストリーミング実行_音声データ含む_音声イベント通知()
        {
            // Arrange - 実際のDifyの音声データで完全テスト
            var request = new DifyRequest("音声テスト", "test-user");
            var realConversationId = "1213080f-f863-4a53-9149-bbe05096f758";
            var realMessageId = "9e5da094-804f-4d84-a68d-26a59f158231";
            var realAudioData = "//PExABatDnYAVnAADrrO3E88zvpN09NkEFGk0bzByvm62a6JljmKGYIJggmCCBgwEIWQLIFkDAAMAAwACzCABFNU6p1TqBpjororpFoOIqIqJEJiKkXY1yHLf5yuG3bct/7deN0/akYdhrC7FB1jtfh+3TvuregHMAAEAswXgQcSITEVIqRItY7X3ff9y2dtfh+WSh/H8hyWblb/uQ1hrjuQ5jK5f2np6enp7dyGGsKnUDRXQDoB0A6Rag7E2uNYZwzhrjuP5DkOP/G6enhty2drvYmsRdjEGuOQ5D+Q5LM68NuW5bluW/7vuQ7ksxlb/w/fhhh7E3Lh+nzrw25axF2MQZwwxdi7GIOI/7/v+5bW2dsTZ277+P4/j+P4/j+P5DkPv+5bW2drvXeu9iaxF2LsXYuxdi7F2LsTHVOqdd6713sTZ219rjkOQ5DkOQ1hYRFQtOWnLxoPorpFqDuPWcty4vlKH8nG5l/zIQzEARC8C6KKG4fyTEFNRTMuMTA";
            
            // 実際のDifySSEフォーマット（tts_messageイベント）
            var mockSseData = $"data: {{\"event\":\"tts_message\",\"answer\":\"\",\"audio\":\"{realAudioData}\",\"conversation_id\":\"{realConversationId}\",\"message_id\":\"{realMessageId}\",\"taskId\":\"\"}}\n\n" +
                             $"data: {{\"event\":\"message_end\",\"answer\":\"\",\"audio\":\"\",\"conversation_id\":\"{realConversationId}\",\"message_id\":\"{realMessageId}\",\"taskId\":\"\"}}\n\n";
            
            _mockHttpClient.SetupStreamingResponse(mockSseData);

            // Act - 実際のDifyHttpAdapterを通した完全統合テスト
            QueryResponse result = null;
            var receivedEvents = new System.Collections.Generic.List<DifyStreamEvent>();
            
            yield return PerformAsyncOperation(
                () => _adapter.ExecuteStreamingAsync(
                    request, 
                    evt => {
                        UnityEngine.Debug.Log($"[TEST] Received audio event: {evt.EventType}");
                        receivedEvents.Add(evt);
                    }, 
                    CancellationToken.None),
                r => result = r);

            // Assert - DifyHttpAdapterの実際の音声処理結果を検証
            Assert.IsTrue(result.IsSuccess, "Audio request should be successful");
            Assert.IsTrue(result.HasAudioData, "Result should contain audio data");
            Assert.AreEqual(2, receivedEvents.Count, "Should receive 2 events (audio + end)");
            Assert.IsTrue(receivedEvents[0].IsAudioEvent, "First event should be audio event");
            Assert.IsTrue(receivedEvents[0].HasValidAudio, "Audio event should have valid audio data");
            Assert.IsTrue(receivedEvents[1].IsEndEvent, "Second event should be end event");
        }

        [UnityTest]
        public IEnumerator ストリーミング実行_NullRequest_ArgumentNullException()
        {
            // Act & Assert
            yield return PerformAsyncOperationExpectingException<ArgumentNullException>(
                () => _adapter.ExecuteStreamingAsync(null, null, CancellationToken.None));
        }

        [UnityTest]
        public IEnumerator ストリーミング実行_HTTP通信エラー_失敗レスポンスを返す()
        {
            // Arrange
            var request = new DifyRequest("エラーテスト", "test-user");
            _mockHttpClient.SetupHttpError("Connection timeout");

            // Act
            QueryResponse result = null;
            yield return PerformAsyncOperation(
                () => _adapter.ExecuteStreamingAsync(request, null, CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Connection timeout", result.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator ストリーミング実行_キャンセルトークン_OperationCancelledException()
        {
            // Arrange - 実際のDifyHttpAdapterでキャンセル処理をテスト
            var request = new DifyRequest("キャンセルテスト", "test-user");
            var cts = new CancellationTokenSource();
            
            // すぐにキャンセル
            cts.Cancel();
            
            // MockHttpClientをセットアップ（遅延付きで確実にキャンセルを検出させる）
            _mockHttpClient.SetupStreamingResponse("data: {\"event\":\"message\",\"answer\":\"テスト\",\"conversation_id\":\"test-id\",\"message_id\":\"test-msg\"}\n\n");

            // Act & Assert - 実際のDifyHttpAdapterのキャンセル処理を検証
            yield return PerformAsyncOperationExpectingException<OperationCanceledException>(
                () => _adapter.ExecuteStreamingAsync(request, null, cts.Token));
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void 設定取得_有効な設定_正しい値を返す()
        {
            // Act
            var config = _adapter.GetConfiguration();

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("test-api-key-12345", config.ApiKey);
            Assert.AreEqual("https://api.dify.ai/v1/chat-messages", config.ApiUrl);
            Assert.IsTrue(config.EnableAudioProcessing);
        }

        [Test]
        public void 設定検証_有効な設定_Trueを返す()
        {
            // Act
            var result = _adapter.ValidateConfiguration();

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Connection Tests

        [UnityTest]
        public IEnumerator 接続テスト_正常な接続_Trueを返す()
        {
            // Arrange
            _mockHttpClient.SetupConnectionSuccess();

            // Act
            bool result = false;
            yield return PerformAsyncOperation(
                () => _adapter.TestConnectionAsync(CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsTrue(result);
        }

        [UnityTest]
        public IEnumerator 接続テスト_接続失敗_Falseを返す()
        {
            // Arrange
            _mockHttpClient.SetupConnectionFailure();

            // Act
            bool result = true;
            yield return PerformAsyncOperation(
                () => _adapter.TestConnectionAsync(CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Unity Test Helper Methods

        /// <summary>
        /// 非同期操作をUnityTest用IEnumeratorで実行
        /// </summary>
        private IEnumerator PerformAsyncOperation<T>(System.Func<Task<T>> asyncOperation, System.Action<T> onResult)
        {
            var task = asyncOperation();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ?? new System.Exception("Unknown async error");
            }

            onResult(task.Result);
        }

        /// <summary>
        /// 例外が期待される非同期操作をUnityTest用で実行
        /// </summary>
        private IEnumerator PerformAsyncOperationExpectingException<TException>(System.Func<Task> asyncOperation)
            where TException : System.Exception
        {
            var task = asyncOperation();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsCanceled)
            {
                if (typeof(TException) == typeof(OperationCanceledException))
                {
                    // Expected cancellation
                    yield break;
                }
                Assert.Fail($"Task was canceled but expected {typeof(TException).Name}");
            }
            else if (task.IsFaulted)
            {
                var baseException = task.Exception?.GetBaseException();
                if (baseException is TException)
                {
                    // Expected exception
                    yield break;
                }
                throw baseException ?? new System.Exception("Unexpected exception type");
            }

            Assert.Fail($"Expected {typeof(TException).Name} was not thrown");
        }

        #endregion
    }

    #region Mock Classes

    /// <summary>
    /// IHttpClient のモック実装
    /// </summary>
    public class MockHttpClient : IHttpClient
    {
        public bool ShouldThrowError { get; set; }
        public string ErrorMessage { get; set; } = "Mock HTTP error";
        public string StreamingResponse { get; set; } = "";
        public bool ConnectionSuccess { get; set; } = true;
        public bool WasCalled { get; private set; } = false;

        public void SetupStreamingResponse(string sseData)
        {
            ShouldThrowError = false;
            StreamingResponse = sseData;
        }

        public void SetupHttpError(string errorMessage)
        {
            ShouldThrowError = true;
            ErrorMessage = errorMessage;
        }

        public void SetupConnectionSuccess()
        {
            ConnectionSuccess = true;
        }

        public void SetupConnectionFailure()
        {
            ConnectionSuccess = false;
        }

        public async Task<HttpResponse> SendStreamingRequestAsync(
            HttpRequest request, 
            System.Action<string> onDataReceived, 
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            UnityEngine.Debug.Log($"[TEST] MockHttpClient called - Request URL: {request.Url}");
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldThrowError)
            {
                return new HttpResponse(false, ErrorMessage, "");
            }

            // 少し待機してから処理
            await Task.Delay(10, cancellationToken);
            
            // SSEデータを正しく分割して送信
            var lines = StreamingResponse.Split('\n');
            UnityEngine.Debug.Log($"[TEST] Processing {lines.Length} lines");
            
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine) && trimmedLine.StartsWith("data:"))
                {
                    UnityEngine.Debug.Log($"[TEST] Sending SSE line: {trimmedLine}");
                    onDataReceived?.Invoke(trimmedLine);
                    // SSEデータ間に少し間隔を空ける
                    await Task.Delay(5, cancellationToken);
                }
            }

            UnityEngine.Debug.Log("[TEST] MockHttpClient completed successfully");
            return new HttpResponse(true, "", StreamingResponse);
        }

        public async Task<bool> TestConnectionAsync(string url, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return ConnectionSuccess;
        }
    }

    #endregion
}