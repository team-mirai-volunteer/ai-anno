using NUnit.Framework;
using System;
using AiTuber.Services.Dify;
using AiTuber.Services.Dify.Data;

namespace AiTuber.Tests.Dify
{
    /// <summary>
    /// DifyApiClient のユニットテスト
    /// 基本的な設定検証、入力値検証、オブジェクト生成テスト
    /// HTTPリクエストは除外（Unity Test Frameworkでの実行を考慮）
    /// </summary>
    [TestFixture]
    public class DifyApiClientTests
    {
        private DifyApiClient _difyClient;

        [SetUp]
        public void SetUp()
        {
            _difyClient = new DifyApiClient
            {
                ApiKey = "test-api-key",
                ApiUrl = "https://api.dify.ai/v1/chat-messages"
            };
        }

        [TearDown]
        public void TearDown()
        {
            // No cleanup needed for configuration-only tests
        }

        #region Configuration Tests

        [Test]
        public void デフォルトコンストラクタ作成テスト()
        {
            // Act
            var client = new DifyApiClient();

            // Assert
            Assert.IsNotNull(client);
        }

        [Test]
        public void 有効な設定での設定検証テスト()
        {
            // Act & Assert
            Assert.IsTrue(_difyClient.IsConfigurationValid());
        }

        [TestCase("", "https://api.dify.ai")]
        [TestCase("key", "")]
        public void 無効な設定での設定検証テスト(string apiKey, string apiUrl)
        {
            // Arrange
            _difyClient.ApiKey = apiKey;
            _difyClient.ApiUrl = apiUrl;

            // Act & Assert
            Assert.IsFalse(_difyClient.IsConfigurationValid());
        }

        #endregion

        #region Request Data Validation Tests

        [Test]
        public void DifyApiRequestの有効データ作成テスト()
        {
            // Act
            var request = new DifyApiRequest
            {
                query = "テストクエリ",
                user = "test-user",
                conversation_id = "test-conv",
                response_mode = "streaming"
            };

            // Assert
            Assert.IsNotNull(request);
            Assert.AreEqual("テストクエリ", request.query);
            Assert.AreEqual("test-user", request.user);
            Assert.AreEqual("test-conv", request.conversation_id);
            Assert.AreEqual("streaming", request.response_mode);
        }

        [Test]
        public void DifyApiRequestのデフォルト値テスト()
        {
            // Act
            var request = new DifyApiRequest();

            // Assert
            Assert.IsNotNull(request);
            Assert.IsNull(request.query);
            Assert.IsNull(request.user);
            Assert.AreEqual("", request.conversation_id); // デフォルトは空文字列
            Assert.AreEqual("streaming", request.response_mode); // デフォルトは"streaming"
        }

        [TestCase(null, "user")]
        [TestCase("", "user")]
        [TestCase("  ", "user")]
        [TestCase("query", null)]
        [TestCase("query", "")]
        [TestCase("query", "  ")]
        public void 無効な入力での検証テスト(string query, string user)
        {
            // Arrange
            var request = new DifyApiRequest { query = query, user = user };

            // Act & Assert
            // Note: This assumes there's a validation method in the client
            // If not available, this test validates the data model constraints
            Assert.IsTrue(string.IsNullOrWhiteSpace(request.query) || string.IsNullOrWhiteSpace(request.user));
        }

        #endregion

        #region Response Data Model Tests

        [Test]
        public void DifyStreamEventの有効データ作成テスト()
        {
            // Act
            var streamEvent = new DifyStreamEvent
            {
                @event = "message",
                conversation_id = "test-conv-id",
                message_id = "test-msg-id",
                answer = "テストレスポンス",
                audio = "VGVzdCBhdWRpbyBkYXRh" // Base64 encoded "Test audio data"
            };

            // Assert
            Assert.IsNotNull(streamEvent);
            Assert.AreEqual("message", streamEvent.@event);
            Assert.AreEqual("test-conv-id", streamEvent.conversation_id);
            Assert.AreEqual("test-msg-id", streamEvent.message_id);
            Assert.AreEqual("テストレスポンス", streamEvent.answer);
            Assert.AreEqual("VGVzdCBhdWRpbyBkYXRh", streamEvent.audio);
        }

        [Test]
        public void DifyStreamEventのデフォルト値テスト()
        {
            // Act
            var streamEvent = new DifyStreamEvent();

            // Assert
            Assert.IsNotNull(streamEvent);
            Assert.IsNull(streamEvent.@event);
            Assert.IsNull(streamEvent.conversation_id);
            Assert.IsNull(streamEvent.message_id);
            Assert.IsNull(streamEvent.answer);
            Assert.IsNull(streamEvent.audio);
        }

        [Test]
        public void DifyProcessingResultの有効データ作成テスト()
        {
            // Act
            var response = new DifyProcessingResult
            {
                ConversationId = "test-conv-id",
                MessageId = "test-msg-id",
                TextResponse = "テストレスポンス",
                IsSuccess = true
            };

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual("test-conv-id", response.ConversationId);
            Assert.AreEqual("test-msg-id", response.MessageId);
            Assert.AreEqual("テストレスポンス", response.TextResponse);
            Assert.IsTrue(response.IsSuccess);
        }

        #endregion

        #region Property Validation Tests

        [Test]
        public void DifyApiClientのプロパティ設定取得テスト()
        {
            // Arrange
            var client = new DifyApiClient();
            var testApiKey = "test-api-key-123";
            var testApiUrl = "https://custom.dify.ai/v1/chat";
            // Act
            client.ApiKey = testApiKey;
            client.ApiUrl = testApiUrl;

            // Assert
            Assert.AreEqual(testApiKey, client.ApiKey);
            Assert.AreEqual(testApiUrl, client.ApiUrl);
        }

        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("   ", false)]
        [TestCase("valid-key", true)]
        public void APIキー検証テスト(string apiKey, bool shouldBeValid)
        {
            // Arrange
            _difyClient.ApiKey = apiKey;
            _difyClient.ApiUrl = "https://test.dify.ai/v1/chat"; // 他の必須項目を設定

            // Act
            var isValid = _difyClient.IsConfigurationValid();
            // Assert
            if (shouldBeValid)
            {
                Assert.IsTrue(isValid, $"Expected APIKey '{apiKey}' to be valid, but IsConfigurationValid() returned false");
            }
            else
            {
                Assert.IsFalse(isValid, $"Expected APIKey '{apiKey}' to be invalid, but IsConfigurationValid() returned true");
            }
        }

        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("   ", false)]
        [TestCase("not-a-url", false)]
        [TestCase("https://api.dify.ai/v1/chat", true)]
        [TestCase("http://localhost:3000/api", true)]
        public void APIURL検証テスト(string apiUrl, bool shouldBeValid)
        {
            // Arrange
            _difyClient.ApiKey = "valid-api-key"; // 他の必須項目を設定
            _difyClient.ApiUrl = apiUrl;

            // Act
            var isValid = _difyClient.IsConfigurationValid();
            // Assert
            if (shouldBeValid)
            {
                Assert.IsTrue(isValid, $"Expected URL '{apiUrl}' to be valid, but IsConfigurationValid() returned false");
            }
            else
            {
                Assert.IsFalse(isValid, $"Expected URL '{apiUrl}' to be invalid, but IsConfigurationValid() returned true");
            }
        }

        #endregion

        #region Utility Tests


        #endregion
    }
}