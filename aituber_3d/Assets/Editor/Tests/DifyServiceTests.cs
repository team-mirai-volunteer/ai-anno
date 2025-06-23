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
    /// DifyService テスト用モック実装
    /// </summary>
    public class MockDifyServiceApiClient : IDifyApiClient
    {
        public string ApiKey { get; set; }
        public string ApiUrl { get; set; }

        // テスト制御用フラグ
        public bool ShouldThrowException { get; set; }
        public bool ShouldReturnError { get; set; }
        public bool ShouldReturnAudio { get; set; } = true;
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
                throw new InvalidOperationException("Mock service exception");

            await Task.Delay(10, cancellationToken);

            // テキストメッセージイベント
            onEventReceived?.Invoke(new DifyStreamEvent
            {
                @event = "message",
                answer = "Mock service response",
                conversation_id = "mock-conv-123",
                message_id = "mock-msg-456"
            });

            // 音声イベント（設定により制御）
            if (ShouldReturnAudio)
            {
                onEventReceived?.Invoke(new DifyStreamEvent
                {
                    @event = "tts_message",
                    audio = "SGVsbG8gU2VydmljZQ==", // "Hello Service" in base64
                    conversation_id = "mock-conv-123"
                });
            }

            // 終了イベント
            onEventReceived?.Invoke(new DifyStreamEvent
            {
                @event = "message_end",
                conversation_id = "mock-conv-123",
                message_id = "mock-msg-456"
            });

            var result = new DifyProcessingResult
            {
                IsSuccess = !ShouldReturnError,
                ConversationId = "mock-conv-123",
                MessageId = "mock-msg-456",
                TextResponse = "Mock service response",
                ProcessingTimeMs = 50,
                TotalEventCount = ShouldReturnAudio ? 3 : 2,
                ErrorMessage = ShouldReturnError ? "Mock service error" : null
            };

            // 音声データをチャンクとして追加
            if (ShouldReturnAudio)
            {
                var audioBytes = Convert.FromBase64String("SGVsbG8gU2VydmljZQ=="); // "Hello Service"
                result.AudioChunks.Add(audioBytes);
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
            await Task.Delay(5, cancellationToken);
            return IsConfigurationValid() && !ShouldThrowException;
        }
    }

    /// <summary>
    /// DifyService クラスのユニットテスト
    /// モック実装による動作確認、実際のHTTP通信なし
    /// </summary>
    [TestFixture]
    public class DifyServiceTests
    {
        private MockDifyServiceApiClient _mockApiClient;
        private DifyServiceConfig _validConfig;
        private DifyService _difyService;

        [SetUp]
        public void SetUp()
        {
            _mockApiClient = new MockDifyServiceApiClient
            {
                ApiKey = "mock-service-key",
                ApiUrl = "https://mock-service.dify.ai/v1/chat-messages"
            };

            _validConfig = new DifyServiceConfig
            {
                ApiKey = "mock-service-key",
                ApiUrl = "https://mock-service.dify.ai/v1/chat-messages",
                EnableAudioProcessing = true
            };

            _difyService = new DifyService(_mockApiClient, _validConfig);
        }

        #region Constructor Tests

        [Test]
        public void コンストラクタ_有効なパラメータ_成功してサービスを作成()
        {
            // Act & Assert
            Assert.IsNotNull(_difyService);
            Assert.IsTrue(_difyService.ValidateConfiguration());
            
        }

        [Test]
        public void コンストラクタ_NullApiClient_ArgumentNullExceptionを投げる()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new DifyService(null, _validConfig));
            
            Assert.AreEqual("apiClient", ex.ParamName);
        }

        [Test]
        public void コンストラクタ_Null設定_ArgumentNullExceptionを投げる()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new DifyService(_mockApiClient, null));
            
            Assert.AreEqual("config", ex.ParamName);
        }

        [Test]
        public void コンストラクタ_無効な設定_ArgumentExceptionを投げる()
        {
            // Arrange
            var invalidConfig = new DifyServiceConfig
            {
                ApiKey = "", // Invalid
                ApiUrl = "https://api.dify.ai"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                new DifyService(_mockApiClient, invalidConfig));
            
            Assert.AreEqual("config", ex.ParamName);
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void 設定検証_有効な設定_Trueを返す()
        {
            // Act
            bool result = _difyService.ValidateConfiguration();
            Assert.IsTrue(result);
        }

        [Test]
        public void 設定取得_設定のコピーを返す()
        {
            // Act
            var config = _difyService.GetConfiguration();
            
            Assert.AreEqual(_validConfig.ApiKey, config.ApiKey);
            Assert.AreEqual(_validConfig.ApiUrl, config.ApiUrl);
            Assert.AreEqual(_validConfig.EnableAudioProcessing, config.EnableAudioProcessing);
        }

        #endregion

        #region Connection Tests

        [UnityTest]
        public IEnumerator 接続テスト_有効な設定_Trueを返す()
        {
            // Act
            var task = _difyService.TestConnectionAsync();
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            // Assert
            Assert.IsTrue(task.Result);
        }

        [UnityTest]
        public IEnumerator 接続テスト_ApiClient例外発生_Falseを返す()
        {
            // Arrange
            _mockApiClient.ShouldThrowException = true;
            
            // Act
            var task = _difyService.TestConnectionAsync();
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            // Assert
            Assert.IsFalse(task.Result);
        }

        #endregion

        #region ProcessUserQueryAsync Tests

        [UnityTest]
        public IEnumerator ユーザークエリ処理_有効なストリーミングリクエスト_成功結果を返す()
        {
            // Arrange
            string userQuery = "こんにちは、サービステストです";
            string userId = "test-service-user";
            
            // Act
            var task = _difyService.ProcessUserQueryAsync(userQuery, userId, conversationId: null, onStreamEvent: null, cancellationToken: default);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            var result = task.Result;
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasTextResponse);
            Assert.IsTrue(result.HasAudioData);
            Assert.AreEqual("mock-conv-123", result.ConversationId);
            Assert.AreEqual("mock-msg-456", result.MessageId);
            Assert.Greater(result.EventCount, 0);
        }


        [UnityTest]
        public IEnumerator ユーザークエリ処理_空のクエリ_ArgumentExceptionを投げる()
        {
            // Act
            var task = _difyService.ProcessUserQueryAsync("", "test-user", conversationId: null, onStreamEvent: null, cancellationToken: default);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            // Assert
            Assert.IsNotNull(task.Exception);
            Assert.IsInstanceOf<ArgumentException>(task.Exception.InnerException);
        }

        [UnityTest]
        public IEnumerator ユーザークエリ処理_空のユーザーID_ArgumentExceptionを投げる()
        {
            // Act
            var task = _difyService.ProcessUserQueryAsync("test query", "", conversationId: null, onStreamEvent: null, cancellationToken: default);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            // Assert
            Assert.IsNotNull(task.Exception);
            Assert.IsInstanceOf<ArgumentException>(task.Exception.InnerException);
        }

        [UnityTest]
        public IEnumerator ユーザークエリ処理_ApiClientエラー_エラー結果を返す()
        {
            // Arrange
            _mockApiClient.ShouldReturnError = true;
            
            string userQuery = "エラーテスト";
            string userId = "test-error-user";
            
            // Act
            var task = _difyService.ProcessUserQueryAsync(userQuery, userId, conversationId: null, onStreamEvent: null, cancellationToken: default);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            var result = task.Result;
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator ユーザークエリ処理_会話ID付き_正しく渡す()
        {
            // Arrange
            string userQuery = "継続会話テスト";
            string userId = "test-continuation-user";
            string conversationId = "existing-conv-789";
            
            // Act
            var task = _difyService.ProcessUserQueryAsync(userQuery, userId, conversationId, onStreamEvent: null, cancellationToken: default);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            var result = task.Result;
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(conversationId, _mockApiClient.LastRequest.conversation_id);
        }

        #endregion

        #region Audio Processing Tests

        [UnityTest]
        public IEnumerator ユーザークエリ処理_音声無効_結果に音声なし()
        {
            // Arrange
            _validConfig.EnableAudioProcessing = false;
            _difyService = new DifyService(_mockApiClient, _validConfig);
            
            string userQuery = "音声無効テスト";
            string userId = "test-no-audio-user";
            
            // Act
            var task = _difyService.ProcessUserQueryAsync(userQuery, userId, conversationId: null, onStreamEvent: null, cancellationToken: default);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            var result = task.Result;
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.HasAudioData);
            Assert.IsTrue(result.HasTextResponse);
        }

        #endregion

        #region DifyServiceConfig Tests

        [Test]
        public void DifyServiceConfig_有効な設定_IsValidがTrueを返す()
        {
            // Arrange
            var config = new DifyServiceConfig
            {
                ApiKey = "valid-key",
                ApiUrl = "https://api.dify.ai/v1/chat"
            };
            
            // Act & Assert
            Assert.IsTrue(config.IsValid);
        }

        [TestCase("", "https://api.dify.ai")]
        [TestCase("key", "")]
        public void DifyServiceConfig_無効な設定_IsValidがFalseを返す(string apiKey, string apiUrl)
        {
            // Arrange
            var config = new DifyServiceConfig
            {
                ApiKey = apiKey,
                ApiUrl = apiUrl
            };
            
            // Act & Assert
            Assert.IsFalse(config.IsValid);
        }

        #endregion

        #region DifyServiceResult Tests

        [Test]
        public void DifyServiceResult_プロパティ_正常に動作する()
        {
            // Arrange
            var result = new DifyServiceResult
            {
                IsSuccess = true,
                TextResponse = "Test response",
                AudioData = new byte[] { 1, 2, 3, 4, 5 },
                ConversationId = "conv-123",
                MessageId = "msg-456",
                ProcessingTimeMs = 100,
                EventCount = 3
            };
            
            // Act & Assert
            Assert.IsTrue(result.HasTextResponse);
            Assert.IsTrue(result.HasAudioData);
            Assert.AreEqual("conv-123", result.ConversationId);
            Assert.AreEqual("msg-456", result.MessageId);
        }

        #endregion
    }
}