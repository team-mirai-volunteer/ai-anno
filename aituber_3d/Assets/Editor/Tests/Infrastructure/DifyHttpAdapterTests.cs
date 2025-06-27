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
            _config = new DifyConfiguration(
                apiKey: "test-api-key-12345",
                apiUrl: "https://api.dify.ai/v1/chat-messages",
                enableAudioProcessing: true,
                enableDebugLogging: true
            );
            _adapter = new DifyHttpAdapter(_mockHttpClient, _config);
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
            // Arrange
            var request = new DifyRequest("こんにちは", "test-user");
            var mockSseData = "data: {\"event\":\"message\",\"answer\":\"こんにちは！\",\"conversation_id\":\"conv-123\",\"message_id\":\"msg-456\"}\n\n" +
                             "data: {\"event\":\"message_end\",\"conversation_id\":\"conv-123\",\"message_id\":\"msg-456\"}\n\n";
            
            _mockHttpClient.SetupStreamingResponse(mockSseData);

            // Act
            QueryResponse result = null;
            var receivedEvents = new System.Collections.Generic.List<DifyStreamEvent>();
            
            yield return PerformAsyncOperation(
                () => _adapter.ExecuteStreamingAsync(
                    request, 
                    evt => receivedEvents.Add(evt), 
                    CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("こんにちは！", result.TextResponse);
            Assert.AreEqual("conv-123", result.ConversationId);
            Assert.AreEqual("msg-456", result.MessageId);
            Assert.AreEqual(2, receivedEvents.Count);
            Assert.IsTrue(receivedEvents[0].IsMessageEvent);
            Assert.IsTrue(receivedEvents[1].IsEndEvent);
        }

        [UnityTest]
        public IEnumerator ストリーミング実行_音声データ含む_音声イベント通知()
        {
            // Arrange
            var request = new DifyRequest("音声テスト", "test-user");
            var audioData = Convert.ToBase64String(new byte[] { 0xFF, 0xF3, 0x01 }); // MP3 header
            var mockSseData = $"data: {{\"event\":\"tts_message\",\"audio\":\"{audioData}\",\"conversation_id\":\"conv-123\"}}\n\n" +
                             "data: {\"event\":\"message_end\",\"conversation_id\":\"conv-123\"}\n\n";
            
            _mockHttpClient.SetupStreamingResponse(mockSseData);

            // Act
            QueryResponse result = null;
            var receivedEvents = new System.Collections.Generic.List<DifyStreamEvent>();
            
            yield return PerformAsyncOperation(
                () => _adapter.ExecuteStreamingAsync(
                    request, 
                    evt => receivedEvents.Add(evt), 
                    CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasAudioData);
            Assert.AreEqual(2, receivedEvents.Count);
            Assert.IsTrue(receivedEvents[0].IsAudioEvent);
            Assert.IsTrue(receivedEvents[0].HasValidAudio);
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
            // Arrange
            var request = new DifyRequest("キャンセルテスト", "test-user");
            var cts = new CancellationTokenSource();
            
            // Setup long running response to allow cancellation
            _mockHttpClient.SetupStreamingResponse("data: {\"event\":\"message\",\"answer\":\"テスト\"}\n\n");
            
            // Cancel token immediately
            cts.Cancel();

            // Act & Assert
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

            if (task.IsFaulted)
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
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldThrowError)
            {
                return new HttpResponse(false, ErrorMessage, "");
            }

            // SSEデータをライン単位で送信シミュレート
            await Task.Delay(10, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            var lines = StreamingResponse.Split('\n');
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(line))
                {
                    onDataReceived?.Invoke(line);
                }
            }

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