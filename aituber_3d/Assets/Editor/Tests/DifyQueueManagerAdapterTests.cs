using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AiTuber.Services.Dify;
using AiTuber.Services.Dify.Data;
using AiTuber.Services.Dify.Unity;
using AiTuber.Tests.Dify;
using Aituber;
using Cysharp.Threading.Tasks;

namespace AiTuber.Editor.Tests
{
    /// <summary>
    /// DifyQueueManagerAdapterのユニットテスト
    /// 既存のQueueManagerとの統合機能を検証
    /// </summary>
    public class DifyQueueManagerAdapterTests
    {
        private MockDifyApiClient _mockApiClient;
        private DifyServiceConfig _config;
        private DifyService _difyService;
        private DifyQueueManagerAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _mockApiClient = new MockDifyApiClient();
            
            _config = new DifyServiceConfig
            {
                ApiKey = "test-api-key-12345",
                ApiUrl = "https://api.dify.ai/v1/chat-messages",
                EnableAudioProcessing = true
            };

            // Ensure mock is properly configured for success scenarios
            _mockApiClient.ShouldThrowException = false;
            _mockApiClient.ShouldReturnError = false;

            _difyService = new DifyService(_mockApiClient, _config);
            _adapter = new DifyQueueManagerAdapter(_difyService, _mockApiClient, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _adapter = null;
            _difyService = null;
            _config = null;
            _mockApiClient = null;
        }

        #region Constructor Tests

        [Test]
        public void コンストラクタ_有効な引数_インスタンスを作成()
        {
            Assert.IsNotNull(_adapter);
        }

        [Test]
        public void コンストラクタ_NullDifyService_ArgumentNullExceptionを投げる()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new DifyQueueManagerAdapter(null, _mockApiClient, _config));
        }

        [Test]
        public void コンストラクタ_NullApiClient_ArgumentNullExceptionを投げる()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new DifyQueueManagerAdapter(_difyService, null, _config));
        }

        [Test]
        public void コンストラクタ_Null設定_ArgumentNullExceptionを投げる()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new DifyQueueManagerAdapter(_difyService, _mockApiClient, null));
        }

        #endregion

        #region ProcessQuestionAsync Tests

        [UnityTest]
        public IEnumerator 質問処理_有効な質問_成功結果を返す()
        {
            // Arrange
            var question = new Question("テスト質問", "テストユーザー", "test-icon", false);

            // Act
            DifyServiceResult result = null;
            var task = UniTask.ToCoroutine(async () =>
            {
                result = await _adapter.ProcessQuestionAsync(question);
            });
            yield return task;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.TextResponse.Contains("テスト質問"));
            Assert.AreEqual("mock-conv-id", result.ConversationId);
            Assert.AreEqual("mock-msg-id", result.MessageId);
        }

        [UnityTest]
        public IEnumerator 質問処琇_Null質問_ArgumentNullExceptionを投げる()
        {
            // Act & Assert
            bool exceptionThrown = false;
            System.Exception thrownException = null;
            
            yield return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await _adapter.ProcessQuestionAsync(null);
                }
                catch (System.Exception ex)
                {
                    exceptionThrown = true;
                    thrownException = ex;
                }
            });
            
            Assert.IsTrue(exceptionThrown, "Expected ArgumentNullException was not thrown");
            Assert.IsInstanceOf<ArgumentNullException>(thrownException);
        }

        [UnityTest]
        public IEnumerator 質問処理_空の質問テキスト_ArgumentExceptionを投げる()
        {
            // Arrange
            var question = new Question("", "テストユーザー", "test-icon", false);

            // Act & Assert
            bool exceptionThrown = false;
            System.Exception thrownException = null;
            
            yield return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await _adapter.ProcessQuestionAsync(question);
                }
                catch (System.Exception ex)
                {
                    exceptionThrown = true;
                    thrownException = ex;
                }
            });
            
            Assert.IsTrue(exceptionThrown, "Expected ArgumentException was not thrown");
            Assert.IsInstanceOf<ArgumentException>(thrownException);
        }

        [UnityTest]
        public IEnumerator 質問処理_モック設定_成功結果を返す()
        {
            // Arrange
            var question = new Question("テスト質問", "テストユーザー", "test-icon", false);

            // Act
            DifyServiceResult result = null;
            var task = UniTask.ToCoroutine(async () =>
            {
                result = await _adapter.ProcessQuestionAsync(question);
            });
            yield return task;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.TextResponse.Contains("テスト質問"));
        }

        [UnityTest]
        public IEnumerator 質問処理_ユーザー名付き_ユーザー情報を含む()
        {
            // Arrange
            var question = new Question("ユーザーテスト", "TestUser123", "test-icon", false);

            // Act
            DifyServiceResult result = null;
            var task = UniTask.ToCoroutine(async () =>
            {
                result = await _adapter.ProcessQuestionAsync(question);
            });
            yield return task;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.TextResponse.Contains("ユーザーテスト"));
        }

        [UnityTest]
        public IEnumerator 質問処琇_空のユーザー名_匿名ユーザーを処理()
        {
            // Arrange
            var question = new Question("匿名テスト", "", "test-icon", false);

            // Act
            DifyServiceResult result = null;
            var task = UniTask.ToCoroutine(async () =>
            {
                result = await _adapter.ProcessQuestionAsync(question);
            });
            yield return task;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.TextResponse.Contains("匿名テスト"));
        }

        #endregion

        #region CreateConversationFromResult Tests

        [Test]
        public void 結果から会話作成_有効な引数_会話を作成()
        {
            // Arrange
            var question = new Question("テスト質問", "テストユーザー", "test-icon", false);
            var difyResult = new DifyServiceResult
            {
                IsSuccess = true,
                TextResponse = "テスト応答",
                ConversationId = "conv-123"
            };

            // Act
            var conversation = _adapter.CreateConversationFromResult(question, difyResult);

            // Assert
            Assert.IsNotNull(conversation);
            Assert.AreEqual(question, conversation.question);
            Assert.AreEqual("テスト応答", conversation.response);
            Assert.AreEqual("slide_1", conversation.imageFileName);
        }

        [Test]
        public void 結果から会話作成_Null質問_ArgumentNullExceptionを投げる()
        {
            // Arrange
            var difyResult = new DifyServiceResult { IsSuccess = true, TextResponse = "応答" };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _adapter.CreateConversationFromResult(null, difyResult));
        }

        [Test]
        public void 結果から会話作成_Null結果_ArgumentNullExceptionを投げる()
        {
            // Arrange
            var question = new Question("質問", "ユーザー", "icon", false);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _adapter.CreateConversationFromResult(question, null));
        }

        [Test]
        public void 結果から会話作成_失敗結果_デフォルトエラーメッセージを使用()
        {
            // Arrange
            var question = new Question("失敗質問", "テストユーザー", "test-icon", false);
            var difyResult = new DifyServiceResult
            {
                IsSuccess = false,
                ErrorMessage = "API Error"
            };

            // Act
            var conversation = _adapter.CreateConversationFromResult(question, difyResult);

            // Assert
            Assert.IsNotNull(conversation);
            Assert.AreEqual("申し訳ございません。現在、応答を生成できません。", conversation.response);
        }

        [Test]
        public void 結果から会話作成_空のテキスト応答_デフォルトメッセージを使用()
        {
            // Arrange
            var question = new Question("空応答質問", "テストユーザー", "test-icon", false);
            var difyResult = new DifyServiceResult
            {
                IsSuccess = true,
                TextResponse = ""
            };

            // Act
            var conversation = _adapter.CreateConversationFromResult(question, difyResult);

            // Assert
            Assert.IsNotNull(conversation);
            Assert.AreEqual("申し訳ございません。現在、応答を生成できません。", conversation.response);
        }

        #endregion

        #region TestConnectionAsync Tests

        [Test]
        public void 接続テスト_有効な設定_Trueを返す()
        {
            // Act
            var result = _adapter.TestConnection();

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void 接続テスト_無効な設定_Falseを返す()
        {
            // Arrange - 無効な設定のアダプターを作成
            var invalidConfig = new DifyServiceConfig
            {
                ApiKey = "short",
                ApiUrl = "invalid-url"
            };
            var invalidAdapter = new DifyQueueManagerAdapter(_difyService, _mockApiClient, invalidConfig);

            // Act
            var result = invalidAdapter.TestConnection();

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region ValidateConfiguration Tests

        [Test]
        public void 設定検証_有効な設定_Trueを返す()
        {
            // Act
            var result = _adapter.ValidateConfiguration();

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void 設定検証_無効な設定_ArgumentExceptionを投げる()
        {
            // Arrange
            var invalidConfig = new DifyServiceConfig
            {
                ApiKey = "short", // 8文字未満
                ApiUrl = "invalid-url"
            };
            
            // DifyServiceコンストラクタは無効な設定で例外を投げるため、例外をテストする
            Assert.Throws<ArgumentException>(() => 
            {
                var invalidService = new DifyService(_mockApiClient, invalidConfig);
            });

        }

        #endregion

        #region GetConfigurationSummary Tests

        [Test]
        public void 設定サマリー取得_フォーマット済み文字列を返す()
        {
            // Act
            var summary = _adapter.GetConfigurationSummary();

            // Assert
            Assert.IsNotEmpty(summary);
            Assert.IsTrue(summary.Contains("API URL"));
            Assert.IsTrue(summary.Contains("Audio"));
        }

        #endregion

        #region LogProcessingDetails Tests

        [Test]
        public void 処理詳細ログ_有効な結果_正しくログ出力()
        {
            // Arrange
            var question = new Question("ログテスト", "テストユーザー", "test-icon", false);
            var result = new DifyServiceResult
            {
                IsSuccess = true,
                TextResponse = "ログ応答",
                ProcessingTimeMs = 150,
                EventCount = 3,
                AudioData = new byte[] { 1, 2, 3 },
                ConversationId = "conv-123",
                MessageId = "msg-456"
            };

            // Act & Assert (例外が発生しないことを確認)
            Assert.DoesNotThrow(() => _adapter.LogProcessingDetails(result, question));
        }

        [Test]
        public void 処理詳細ログ_Null引数_例外を投げない()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _adapter.LogProcessingDetails(null, null));
        }

        [Test]
        public void 処理詳細ログ_失敗結果_正しくログ出力()
        {
            // Arrange
            var question = new Question("エラーテスト", "テストユーザー", "test-icon", false);
            var result = new DifyServiceResult
            {
                IsSuccess = false,
                ErrorMessage = "テストエラー",
                ProcessingTimeMs = 50
            };

            // Act & Assert - エラーログを抑制してテスト
            Assert.DoesNotThrow(() => _adapter.LogProcessingDetails(result, question, suppressErrorLogs: true));
            
        }

        #endregion
    }
}