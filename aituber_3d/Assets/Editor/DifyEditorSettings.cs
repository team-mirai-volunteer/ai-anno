using UnityEditor;
using UnityEngine;

namespace AiTuber.Editor.Dify
{
    /// <summary>
    /// Difyエディタツール用設定管理クラス
    /// EditorPrefsを使用してAPIKey/URLを永続化
    /// エディタセッション間で設定を保持
    /// </summary>
    public static class DifyEditorSettings
    {
        #region EditorPrefs Keys

        private const string API_KEY_PREF = "DifyEditor.ApiKey";
        private const string API_URL_PREF = "DifyEditor.ApiUrl";
        private const string DEBUG_LOGGING_PREF = "DifyEditor.DebugLogging";

        #endregion

        #region Default Values

        private const string DEFAULT_API_URL = "https://api.dify.ai/v1/chat-messages";
        private const bool DEFAULT_DEBUG_LOGGING = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// Dify API Key
        /// app-で始まる必要がある
        /// </summary>
        public static string ApiKey => EditorPrefs.GetString(API_KEY_PREF, string.Empty);

        /// <summary>
        /// Dify API URL
        /// </summary>
        public static string ApiUrl => EditorPrefs.GetString(API_URL_PREF, DEFAULT_API_URL);


        /// <summary>
        /// デバッグログを有効にするかどうか
        /// </summary>
        public static bool EnableDebugLogging => EditorPrefs.GetBool(DEBUG_LOGGING_PREF, DEFAULT_DEBUG_LOGGING);

        #endregion

        #region Validation

        /// <summary>
        /// 現在の設定が有効かどうかを検証
        /// </summary>
        /// <returns>有効な設定の場合true</returns>
        public static bool IsValid()
        {
            var errors = ValidateConfiguration();
            return errors.Count == 0;
        }

        /// <summary>
        /// 設定検証の詳細結果を取得
        /// </summary>
        /// <returns>エラーメッセージのリスト（空の場合は有効）</returns>
        public static System.Collections.Generic.List<string> ValidateConfiguration()
        {
            var errors = new System.Collections.Generic.List<string>();

            // API Key検証
            var apiKey = ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                errors.Add("API Key is required");
            }
            else if (apiKey.Length < 8)
            {
                errors.Add("API Key must be at least 8 characters");
            }
            else if (!apiKey.StartsWith("app-"))
            {
                errors.Add("API Key should start with 'app-'");
            }

            // API URL検証
            var apiUrl = ApiUrl;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                errors.Add("API URL is required");
            }
            else if (!IsValidUrl(apiUrl))
            {
                errors.Add("API URL must be a valid HTTP/HTTPS URL");
            }


            return errors;
        }

        /// <summary>
        /// URL形式の検証
        /// </summary>
        /// <param name="url">検証対象URL</param>
        /// <returns>有効なHTTP/HTTPS URLの場合true</returns>
        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return System.Uri.TryCreate(url, System.UriKind.Absolute, out var result) &&
                   (result.Scheme == System.Uri.UriSchemeHttp || result.Scheme == System.Uri.UriSchemeHttps);
        }

        #endregion

        #region Configuration Management
        
        /// <summary>
        /// API Keyを設定（Editor専用）
        /// </summary>
        /// <param name="apiKey">設定するAPI Key</param>
        internal static void SetApiKey(string apiKey)
        {
            EditorPrefs.SetString(API_KEY_PREF, apiKey ?? string.Empty);
            OnSettingsChanged();
        }
        
        /// <summary>
        /// API URLを設定（Editor専用）
        /// </summary>
        /// <param name="apiUrl">設定するAPI URL</param>
        internal static void SetApiUrl(string apiUrl)
        {
            EditorPrefs.SetString(API_URL_PREF, apiUrl ?? DEFAULT_API_URL);
            OnSettingsChanged();
        }
        
        
        /// <summary>
        /// デバッグログを設定（Editor専用）
        /// </summary>
        /// <param name="enableDebugLogging">デバッグログを有効にするかどうか</param>
        internal static void SetEnableDebugLogging(bool enableDebugLogging)
        {
            EditorPrefs.SetBool(DEBUG_LOGGING_PREF, enableDebugLogging);
            OnSettingsChanged();
        }



        /// <summary>
        /// 設定をDifyConfigAssetに保存
        /// </summary>
        /// <param name="configAsset">保存先DifyConfigAsset</param>
        public static void SaveToConfigAsset(AiTuber.Services.Dify.Unity.DifyConfigAsset configAsset)
        {
            if (configAsset == null)
                return;

            var serviceConfig = new AiTuber.Services.Dify.DifyServiceConfig
            {
                ApiKey = ApiKey,
                ApiUrl = ApiUrl,
                EnableAudioProcessing = true // デフォルトで有効
            };

            configAsset.FromDifyServiceConfig(serviceConfig);
            EditorUtility.SetDirty(configAsset);

            Debug.Log($"[DifyEditor] Settings saved to {configAsset.name}");
        }

        /// <summary>
        /// DifyApiClientインスタンスを作成
        /// 現在の設定を使用して設定済みクライアントを生成
        /// </summary>
        /// <returns>設定済みDifyApiClient</returns>
        public static AiTuber.Services.Dify.DifyApiClient CreateApiClient()
        {
            var client = new AiTuber.Services.Dify.DifyApiClient
            {
                ApiKey = ApiKey,
                ApiUrl = ApiUrl
            };

            return client;
        }

        #endregion

        #region Configuration Summary

        /// <summary>
        /// 設定情報のサマリーを取得
        /// </summary>
        /// <returns>設定情報の文字列</returns>
        public static string GetConfigurationSummary()
        {
            var apiKeyDisplay = string.IsNullOrEmpty(ApiKey) ? "Not Set" : ApiKey;

            return $"API Key: {apiKeyDisplay}, " +
                   $"URL: {ApiUrl}, " +
                   $"Debug: {EnableDebugLogging}";
        }

        /// <summary>
        /// JSON形式の設定文字列を生成
        /// </summary>
        /// <returns>JSON形式の設定文字列</returns>
        public static string ToJsonString()
        {
            var apiKeyDisplay = string.IsNullOrEmpty(ApiKey) ? "Not Set" : ApiKey;

            return $@"{{
    ""apiKey"": ""{apiKeyDisplay}"",
    ""apiUrl"": ""{ApiUrl}"",
    ""enableDebugLogging"": {EnableDebugLogging.ToString().ToLower()}
}}";
        }

        #endregion

        #region Events

        /// <summary>
        /// 設定変更イベント
        /// </summary>
        public static event System.Action SettingsChanged;

        /// <summary>
        /// 設定変更通知
        /// </summary>
        private static void OnSettingsChanged()
        {
            SettingsChanged?.Invoke();

            if (EnableDebugLogging)
            {
                Debug.Log($"[DifyEditor] Settings changed: {GetConfigurationSummary()}");
            }
        }

        #endregion

    }
}