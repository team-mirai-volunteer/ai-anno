using System;

#nullable enable

namespace AiTuber.Services.Dify.Infrastructure.Http
{
    /// <summary>
    /// Dify API設定クラス
    /// Infrastructure層 Clean Architecture準拠
    /// Legacy DifyEditorSettingsからリファクタリング済み
    /// </summary>
    public class DifyConfiguration
    {
        /// <summary>
        /// Dify API キー
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// Dify API URL
        /// </summary>
        public string ApiUrl { get; }

        /// <summary>
        /// 音声処理有効フラグ
        /// </summary>
        public bool EnableAudioProcessing { get; }

        /// <summary>
        /// タイムアウト時間（秒）
        /// </summary>
        public int TimeoutSeconds { get; }

        /// <summary>
        /// リトライ回数
        /// </summary>
        public int RetryCount { get; }

        /// <summary>
        /// デバッグログ有効フラグ
        /// </summary>
        public bool EnableDebugLogging { get; }

        /// <summary>
        /// DifyConfiguration を作成
        /// </summary>
        /// <param name="apiKey">API キー</param>
        /// <param name="apiUrl">API URL</param>
        /// <param name="enableAudioProcessing">音声処理有効フラグ</param>
        /// <param name="timeoutSeconds">タイムアウト時間</param>
        /// <param name="retryCount">リトライ回数</param>
        /// <param name="enableDebugLogging">デバッグログ有効フラグ</param>
        /// <exception cref="ArgumentException">無効なパラメータが指定された場合</exception>
        public DifyConfiguration(
            string apiKey,
            string apiUrl,
            bool enableAudioProcessing = true,
            int timeoutSeconds = 30,
            int retryCount = 3,
            bool enableDebugLogging = false)
        {
            ApiKey = ValidateApiKey(apiKey);
            ApiUrl = ValidateApiUrl(apiUrl);
            EnableAudioProcessing = enableAudioProcessing;
            TimeoutSeconds = ValidateTimeoutSeconds(timeoutSeconds);
            RetryCount = ValidateRetryCount(retryCount);
            EnableDebugLogging = enableDebugLogging;
        }

        /// <summary>
        /// 設定の妥当性を検証
        /// </summary>
        /// <returns>有効な設定の場合true</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ApiKey) &&
                   !string.IsNullOrWhiteSpace(ApiUrl) &&
                   Uri.IsWellFormedUriString(ApiUrl, UriKind.Absolute) &&
                   TimeoutSeconds > 0 &&
                   RetryCount >= 0;
        }

        /// <summary>
        /// Legacy DifyEditorSettings互換形式に変換
        /// </summary>
        /// <returns>Legacy互換のデータ転送オブジェクト</returns>
        public DifyConfigurationDto ToDto()
        {
            return new DifyConfigurationDto
            {
                ApiKey = ApiKey,
                ApiUrl = ApiUrl,
                EnableAudioProcessing = EnableAudioProcessing,
                TimeoutSeconds = TimeoutSeconds,
                RetryCount = RetryCount,
                EnableDebugLogging = EnableDebugLogging
            };
        }

        /// <summary>
        /// APIキーの妥当性検証
        /// </summary>
        /// <param name="apiKey">検証対象APIキー</param>
        /// <returns>有効なAPIキー</returns>
        /// <exception cref="ArgumentException">無効なAPIキー</exception>
        private static string ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("ApiKey cannot be null or empty", nameof(apiKey));

            if (apiKey.Length < 10)
                throw new ArgumentException("ApiKey must be at least 10 characters", nameof(apiKey));

            return apiKey.Trim();
        }

        /// <summary>
        /// API URLの妥当性検証
        /// </summary>
        /// <param name="apiUrl">検証対象API URL</param>
        /// <returns>有効なAPI URL</returns>
        /// <exception cref="ArgumentException">無効なAPI URL</exception>
        private static string ValidateApiUrl(string apiUrl)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentException("ApiUrl cannot be null or empty", nameof(apiUrl));

            if (!Uri.IsWellFormedUriString(apiUrl, UriKind.Absolute))
                throw new ArgumentException("ApiUrl must be a valid absolute URL", nameof(apiUrl));

            return apiUrl.Trim();
        }

        /// <summary>
        /// タイムアウト時間の妥当性検証
        /// </summary>
        /// <param name="timeoutSeconds">検証対象タイムアウト時間</param>
        /// <returns>有効なタイムアウト時間</returns>
        /// <exception cref="ArgumentException">無効なタイムアウト時間</exception>
        private static int ValidateTimeoutSeconds(int timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
                throw new ArgumentException("TimeoutSeconds must be greater than 0", nameof(timeoutSeconds));

            if (timeoutSeconds > 300) // 5分以上は制限
                throw new ArgumentException("TimeoutSeconds must be less than or equal to 300", nameof(timeoutSeconds));

            return timeoutSeconds;
        }

        /// <summary>
        /// リトライ回数の妥当性検証
        /// </summary>
        /// <param name="retryCount">検証対象リトライ回数</param>
        /// <returns>有効なリトライ回数</returns>
        /// <exception cref="ArgumentException">無効なリトライ回数</exception>
        private static int ValidateRetryCount(int retryCount)
        {
            if (retryCount < 0)
                throw new ArgumentException("RetryCount must be greater than or equal to 0", nameof(retryCount));

            if (retryCount > 10) // 10回以上は制限
                throw new ArgumentException("RetryCount must be less than or equal to 10", nameof(retryCount));

            return retryCount;
        }
    }

    /// <summary>
    /// Legacy API互換用データ転送オブジェクト
    /// Infrastructure層でのシリアライゼーション用
    /// </summary>
    public class DifyConfigurationDto
    {
        public string ApiKey { get; set; } = "";
        public string ApiUrl { get; set; } = "";
        public bool EnableAudioProcessing { get; set; }
        public int TimeoutSeconds { get; set; }
        public int RetryCount { get; set; }
        public bool EnableDebugLogging { get; set; }
    }
}