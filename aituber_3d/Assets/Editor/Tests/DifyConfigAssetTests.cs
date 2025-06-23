using System;
using System.Linq;
using UnityEngine;
using NUnit.Framework;
using AiTuber.Services.Dify.Unity;
using AiTuber.Services.Dify;

namespace AiTuber.Editor.Tests
{
    /// <summary>
    /// DifyConfigAssetのユニットテスト
    /// ScriptableObjectベースの設定システムを検証
    /// </summary>
    [TestFixture]
    public class DifyConfigAssetTests
    {
        private DifyConfigAsset _configAsset;

        [SetUp]
        public void SetUp()
        {
            _configAsset = ScriptableObject.CreateInstance<DifyConfigAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_configAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_configAsset);
            }
        }

        #region Validation Tests

        [Test]
        public void 有効性確認_デフォルト設定_Falseを返す()
        {
            // Act
            var isValid = _configAsset.IsValid();

            // Assert
            Assert.IsFalse(isValid);
        }

        [Test]
        public void 設定検証_空のApiKey_エラーを返す()
        {
            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("API Key is required")));
        }

        [Test]
        public void 設定検証_短いApiKey_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "short");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("API Key must be at least 8 characters")));
        }

        [Test]
        public void 設定検証_Appプレフィックスなし_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "invalidkey123");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("API Key should start with 'app-'")));
        }

        [Test]
        public void 設定検証_有効なApiKey_エラーなし()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "https://api.dify.ai/v1/chat-messages");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsFalse(errors.Any(e => e.Contains("API Key")));
        }

        [Test]
        public void 設定検証_空のApiUrl_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("API URL is required")));
        }

        [Test]
        public void 設定検証_無効なApiUrl_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "not-a-valid-url");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("API URL must be a valid HTTP/HTTPS URL")));
        }

        [Test]
        public void 設定検証_有効なHttpsUrl_エラーなし()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "https://api.dify.ai/v1/chat-messages");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsFalse(errors.Any(e => e.Contains("API URL")));
        }

        [Test]
        public void 設定検証_有効なHttpUrl_エラーなし()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "http://localhost:3000/api");

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsFalse(errors.Any(e => e.Contains("API URL")));
        }


        [Test]
        public void 設定検証_ゼロ同時接続数_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "https://api.dify.ai/v1/chat-messages");
            SetPrivateField("_maxConcurrentConnections", 0);

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("Max concurrent connections must be greater than 0")));
        }

        [Test]
        public void 設定検証_高同時接続数_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "https://api.dify.ai/v1/chat-messages");
            SetPrivateField("_maxConcurrentConnections", 10);

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("Max concurrent connections should not exceed 5")));
        }

        [Test]
        public void 設定検証_ゼロリトライ回数_エラーを返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "https://api.dify.ai/v1/chat-messages");
            SetPrivateField("_maxRetryCount", 0);

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Any(e => e.Contains("Max retry count must be greater than 0")));
        }

        [Test]
        public void 設定検証_完全に有効な設定_エラーなし()
        {
            // Arrange
            SetValidConfiguration();

            // Act
            var errors = _configAsset.ValidateConfiguration();

            // Assert
            Assert.AreEqual(0, errors.Count, $"Unexpected errors: {string.Join(", ", errors)}");
        }

        [Test]
        public void 有効性確認_完全に有効な設定_Trueを返す()
        {
            // Arrange
            SetValidConfiguration();

            // Act
            var isValid = _configAsset.IsValid();

            // Assert
            Assert.IsTrue(isValid);
        }

        #endregion

        #region Property Access Tests

        [Test]
        public void プロパティ_デフォルト値_期待されるデフォルトを返す()
        {
            // Assert
            Assert.AreEqual("https://api.dify.ai/v1/chat-messages", _configAsset.ApiUrl);
            Assert.IsTrue(_configAsset.EnableAudioProcessing);
            Assert.AreEqual(2, _configAsset.MaxConcurrentConnections);
            Assert.AreEqual(3, _configAsset.MaxRetryCount);
            Assert.IsTrue(_configAsset.EnableDebugLogging);
        }

        [Test]
        public void ApiKey_値設定_正しい値を返す()
        {
            // Arrange
            SetPrivateField("_apiKey", "app-testkey123");

            // Act & Assert
            Assert.AreEqual("app-testkey123", _configAsset.ApiKey);
        }

        #endregion

        #region Conversion Tests

        [Test]
        public void DifyServiceConfig変換_有効な設定_正しい設定を返す()
        {
            // Arrange
            SetValidConfiguration();

            // Act
            var serviceConfig = _configAsset.ToDifyServiceConfig();

            // Assert
            Assert.IsNotNull(serviceConfig);
            Assert.AreEqual("app-validkey123", serviceConfig.ApiKey);
            Assert.AreEqual("https://api.dify.ai/v1/chat-messages", serviceConfig.ApiUrl);
            Assert.IsTrue(serviceConfig.EnableAudioProcessing);
        }

        [Test]
        public void DifyServiceConfigから設定_有効なサービス設定_アセットを更新する()
        {
            // Arrange
            var serviceConfig = new DifyServiceConfig
            {
                ApiKey = "app-fromservice123",
                ApiUrl = "https://custom.api.com/v1/chat",
                EnableAudioProcessing = false
            };

            // Act
            _configAsset.FromDifyServiceConfig(serviceConfig);

            // Assert
            Assert.AreEqual("app-fromservice123", _configAsset.ApiKey);
            Assert.AreEqual("https://custom.api.com/v1/chat", _configAsset.ApiUrl);
            Assert.IsFalse(_configAsset.EnableAudioProcessing);
        }

        [Test]
        public void DifyServiceConfigから設定_Null設定_変更しない()
        {
            // Arrange
            SetValidConfiguration();
            var originalApiKey = _configAsset.ApiKey;

            // Act
            _configAsset.FromDifyServiceConfig(null);

            // Assert
            Assert.AreEqual(originalApiKey, _configAsset.ApiKey);
        }

        #endregion

        #region Utility Tests

        [Test]
        public void 設定サマリー取得_有効な設定_フォーマット済み文字列を返す()
        {
            // Arrange
            SetValidConfiguration();

            // Act
            var summary = _configAsset.GetConfigurationSummary();

            // Assert
            Assert.IsNotEmpty(summary);
            Assert.IsTrue(summary.Contains("app-valid..."));
            Assert.IsTrue(summary.Contains("https://api.dify.ai"));
            Assert.IsTrue(summary.Contains("Audio: True"));
        }

        [Test]
        public void 設定サマリー取得_ApiKeyなし_未設定を表示()
        {
            // Act
            var summary = _configAsset.GetConfigurationSummary();

            // Assert
            Assert.IsTrue(summary.Contains("Not Set"));
        }

        [Test]
        public void JSON文字列変換_有効な設定_有効なJSONを返す()
        {
            // Arrange
            SetValidConfiguration();

            // Act
            var jsonString = _configAsset.ToJsonString();

            // Assert
            Assert.IsNotEmpty(jsonString);
            Assert.IsTrue(jsonString.Contains("apiKey"));
            Assert.IsTrue(jsonString.Contains("app-valid..."));
            Assert.IsFalse(jsonString.Contains("app-validkey123")); // API Keyは隠蔽されているべき
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// プライベートフィールドに値を設定
        /// </summary>
        /// <param name="fieldName">フィールド名</param>
        /// <param name="value">設定値</param>
        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(DifyConfigAsset).GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_configAsset, value);
        }

        /// <summary>
        /// 有効な設定を設定
        /// </summary>
        private void SetValidConfiguration()
        {
            SetPrivateField("_apiKey", "app-validkey123");
            SetPrivateField("_apiUrl", "https://api.dify.ai/v1/chat-messages");
            SetPrivateField("_enableAudioProcessing", true);
            SetPrivateField("_maxConcurrentConnections", 2);
            SetPrivateField("_maxRetryCount", 3);
            SetPrivateField("_enableDebugLogging", true);
        }

        #endregion
    }
}
