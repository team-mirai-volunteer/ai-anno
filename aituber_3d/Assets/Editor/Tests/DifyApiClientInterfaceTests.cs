using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using AiTuber.Services.Dify;
using AiTuber.Services.Dify.Data;

namespace AiTuber.Tests.Dify
{
    /// <summary>
    /// IDifyApiClient インターフェースのテスト用モック実装
    /// TDD実装、モックによる動作確認
    /// </summary>
    public class MockDifyApiClient : IDifyApiClient
    {
        public string ApiKey { get; set; }
        public string ApiUrl { get; set; }

        // テスト用のフラグ・カウンター
        public bool ShouldThrowException { get; set; }
        public bool ShouldReturnError { get; set; }
        public int CallCount { get; set; }
        public DifyApiRequest LastRequest { get; set; }

        public async Task<DifyProcessingResult> SendStreamingRequestAsync(
            DifyApiRequest request,
            Action<DifyStreamEvent> onEventReceived,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!request.IsValid())
                throw new ArgumentException("Invalid request", nameof(request));

            if (ShouldThrowException)
                throw new InvalidOperationException("Mock exception");

            // モックイベントの生成・送信
            await Task.Delay(_mockDelayMs, cancellationToken);
            
            // Include the original query in the response for test assertions
            var responseText = ShouldReturnError ? _mockTextResponse : $"{_mockTextResponse}: {request.query}";
            
            onEventReceived?.Invoke(new DifyStreamEvent
            {
                @event = "message",
                answer = responseText,
                conversation_id = _mockConversationId,
                message_id = _mockMessageId
            });

            if (_mockAudioData != null)
            {
                onEventReceived?.Invoke(new DifyStreamEvent
                {
                    @event = "tts_message",
                    audio = Convert.ToBase64String(_mockAudioData),
                    conversation_id = _mockConversationId
                });
            }

            onEventReceived?.Invoke(new DifyStreamEvent
            {
                @event = "message_end",
                conversation_id = _mockConversationId,
                message_id = _mockMessageId
            });

            var result = new DifyProcessingResult
            {
                IsSuccess = !ShouldReturnError,
                ConversationId = _mockConversationId,
                MessageId = _mockMessageId,
                TextResponse = responseText,
                ProcessingTimeMs = _mockDelayMs,
                TotalEventCount = 3,
                ErrorMessage = ShouldReturnError ? _mockErrorMessage : null
            };

            if (_mockAudioData != null)
            {
                result.AudioChunks.Add(_mockAudioData);
            }

            return result;
        }


        public bool IsConfigurationValid()
        {
            return !string.IsNullOrEmpty(ApiKey) && 
                   !string.IsNullOrEmpty(ApiUrl);
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return IsConfigurationValid() && !ShouldThrowException;
        }

        // テスト用セットアップメソッド
        public void SetupStreamingResponse(string textResponse, string conversationId, string messageId)
        {
            ShouldThrowException = false;
            ShouldReturnError = false;
            _mockTextResponse = textResponse;
            _mockConversationId = conversationId;
            _mockMessageId = messageId;
        }

        public void SetupFailureResponse(string errorMessage)
        {
            ShouldThrowException = false;
            ShouldReturnError = true;
            _mockErrorMessage = errorMessage;
        }

        public void SetupConnectionTest(bool shouldSucceed)
        {
            ShouldThrowException = !shouldSucceed;
            ShouldReturnError = !shouldSucceed;
        }

        public void SetupStreamingResponseWithAudio(string textResponse, byte[] audioData)
        {
            SetupStreamingResponse(textResponse, "conv-123", "msg-456");
            _mockAudioData = audioData;
        }

        public void SetupSlowResponse(int delayMs)
        {
            _mockDelayMs = delayMs;
        }

        // テスト用フィールド
        public string _mockTextResponse = "Mock response";
        public string _mockConversationId = "mock-conv-id";
        public string _mockMessageId = "mock-msg-id";
        public string _mockErrorMessage = "Mock error";
        public byte[] _mockAudioData;
        public int _mockDelayMs = 10;
    }

    /// <summary>
    /// IDifyApiClient インターフェースのユニットテスト
    /// モック実装による動作確認、実際のHTTP通信なし
    /// 注意: 非同期テストは Unity Test Runner でデッドロックを起こすため除外
    /// </summary>
    [TestFixture]
    public class DifyApiClientInterfaceTests
    {
        private MockDifyApiClient _mockClient;

        [SetUp]
        public void SetUp()
        {
            _mockClient = new MockDifyApiClient
            {
                ApiKey = "test-api-key",
                ApiUrl = "https://api.dify.ai/v1/chat-messages",
                ShouldThrowException = false,
                ShouldReturnError = false
            };
            
            // デフォルトで音声データを設定（3つのイベントを確実に送信するため）
            _mockClient._mockAudioData = new byte[] { 1, 2, 3, 4, 5 };
        }

        #region Configuration Tests

        [Test]
        public void 設定有効性確認_有効な設定_Trueを返す()
        {
            // Act & Assert
            Assert.IsTrue(_mockClient.IsConfigurationValid());
        }

        [TestCase("", "https://api.dify.ai")]
        [TestCase("key", "")]
        public void 設定有効性確認_無効な設定_Falseを返す(string apiKey, string apiUrl)
        {
            // Arrange
            _mockClient.ApiKey = apiKey;
            _mockClient.ApiUrl = apiUrl;

            // Act & Assert
            Assert.IsFalse(_mockClient.IsConfigurationValid());
        }

        #endregion

        #region Mock Behavior Tests

        [Test]
        public void モッククライアント_プロパティ_正しく設定と取得()
        {
            // Arrange
            var testApiKey = "test-key-123";
            var testApiUrl = "https://custom.api.url";
            var testTimeout = 60;

            // Act
            _mockClient.ApiKey = testApiKey;
            _mockClient.ApiUrl = testApiUrl;

            // Assert
            Assert.AreEqual(testApiKey, _mockClient.ApiKey);
            Assert.AreEqual(testApiUrl, _mockClient.ApiUrl);
        }

        [Test]
        public void モッククライアント_フラグ_正しく設定と取得()
        {
            // Act
            _mockClient.ShouldThrowException = true;
            _mockClient.ShouldReturnError = true;

            // Assert
            Assert.IsTrue(_mockClient.ShouldThrowException);
            Assert.IsTrue(_mockClient.ShouldReturnError);
        }

        #endregion

        #region Async Tests with UnityTest + IEnumerator

        [UnityTest]
        public IEnumerator 接続テスト_有効な設定_Trueを返す()
        {
            // Arrange
            _mockClient.ApiKey = "valid-key";
            _mockClient.ApiUrl = "https://api.dify.ai/v1/chat";

            // Act
            var task = _mockClient.TestConnectionAsync();

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.Result);
        }

        [UnityTest]
        public IEnumerator 接続テスト_無効な設定_Falseを返す()
        {
            // Arrange
            _mockClient.ApiKey = "";

            // Act
            var task = _mockClient.TestConnectionAsync();

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Assert
            Assert.IsFalse(task.Result);
        }

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_有効なリクエスト_成功結果を返す()
        {
            // Arrange
            var request = new DifyApiRequest
            {
                query = "こんにちは",
                user = "test-user"
            };

            var receivedEvents = new System.Collections.Generic.List<DifyStreamEvent>();

            // Act
            var task = _mockClient.SendStreamingRequestAsync(
                request,
                evt => receivedEvents.Add(evt));

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var result = task.Result;

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("mock-conv-id", result.ConversationId);
            Assert.AreEqual("mock-msg-id", result.MessageId);
            Assert.IsTrue(result.TextResponse.Contains("Mock response"));
            Assert.IsTrue(result.TextResponse.Contains("こんにちは"));
            Assert.AreEqual(3, receivedEvents.Count);
            Assert.AreEqual(1, _mockClient.CallCount);
        }


        #endregion
    }
}
