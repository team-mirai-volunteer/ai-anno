using UnityEngine;

namespace AiTuber.Services.Dify.Unity
{
    /// <summary>
    /// Dify設定用ScriptableObject
    /// Unity Inspectorで編集可能なDify API設定を提供
    /// 環境別設定の管理とランタイム設定検証を実装
    /// </summary>
    [CreateAssetMenu(fileName = "DifyConfig", menuName = "AiTuber/Dify Configuration", order = 1)]
    public class DifyConfigAsset : ScriptableObject
    {
        [Header("API Settings")]
        [Tooltip("Dify API Key - app-で始まる文字列")]
        [SerializeField]
        private string _apiKey;

        [Tooltip("Dify API URL - https://api.dify.ai/v1/chat-messages")]
        [SerializeField]
        private string _apiUrl = "https://api.dify.ai/v1/chat-messages";

        [Header("Processing Settings")]
        [Tooltip("音声処理を有効にする")]
        [SerializeField]
        private bool _enableAudioProcessing = true;

        [Header("Advanced Settings")]
        [Tooltip("最大同時接続数")]
        [SerializeField]
        [Range(1, 5)]
        private int _maxConcurrentConnections = 2;

        [Tooltip("リトライ回数")]
        [SerializeField]
        [Range(1, 10)]
        private int _maxRetryCount = 3;

        [Tooltip("デバッグログを有効にする")]
        [SerializeField]
        private bool _enableDebugLogging = true;

        #region Public Properties

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey => _apiKey;

        /// <summary>
        /// API URL
        /// </summary>
        public string ApiUrl => _apiUrl;

        /// <summary>
        /// 音声処理を有効にするかどうか
        /// </summary>
        public bool EnableAudioProcessing => _enableAudioProcessing;

        /// <summary>
        /// 最大同時接続数
        /// </summary>
        public int MaxConcurrentConnections => _maxConcurrentConnections;

        /// <summary>
        /// 最大リトライ回数
        /// </summary>
        public int MaxRetryCount => _maxRetryCount;

        /// <summary>
        /// デバッグログを有効にするかどうか
        /// </summary>
        public bool EnableDebugLogging => _enableDebugLogging;

        #endregion

        #region Validation

        /// <summary>
        /// 設定が有効かどうかを検証
        /// </summary>
        /// <returns>有効な設定の場合true</returns>
        public bool IsValid()
        {
            var errors = ValidateConfiguration();
            return errors.Count == 0;
        }

        /// <summary>
        /// 設定検証の詳細結果を取得
        /// </summary>
        /// <returns>エラーメッセージのリスト（空の場合は有効）</returns>
        public System.Collections.Generic.List<string> ValidateConfiguration()
        {
            var errors = new System.Collections.Generic.List<string>();

            // API Key検証
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                errors.Add("API Key is required");
            }
            else if (_apiKey.Length < 8)
            {
                errors.Add("API Key must be at least 8 characters");
            }
            else if (!_apiKey.StartsWith("app-"))
            {
                errors.Add("API Key should start with 'app-'");
            }

            // API URL検証
            if (string.IsNullOrWhiteSpace(_apiUrl))
            {
                errors.Add("API URL is required");
            }
            else if (!IsValidUrl(_apiUrl))
            {
                errors.Add("API URL must be a valid HTTP/HTTPS URL");
            }


            // 同時接続数検証
            if (_maxConcurrentConnections <= 0)
            {
                errors.Add("Max concurrent connections must be greater than 0");
            }
            else if (_maxConcurrentConnections > 5)
            {
                errors.Add("Max concurrent connections should not exceed 5");
            }

            // リトライ回数検証
            if (_maxRetryCount <= 0)
            {
                errors.Add("Max retry count must be greater than 0");
            }

            return errors;
        }

        /// <summary>
        /// URL形式の検証
        /// </summary>
        /// <param name="url">検証対象URL</param>
        /// <returns>有効なHTTP/HTTPS URLの場合true</returns>
        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return System.Uri.TryCreate(url, System.UriKind.Absolute, out var result) &&
                   (result.Scheme == System.Uri.UriSchemeHttp || result.Scheme == System.Uri.UriSchemeHttps);
        }

        #endregion

        #region Unity Inspector Methods

        /// <summary>
        /// Inspectorから設定検証を実行
        /// </summary>
        [ContextMenu("Validate Configuration")]
        private void ValidateConfigurationFromInspector()
        {
            var errors = ValidateConfiguration();
            
            if (errors.Count == 0)
            {
                Debug.Log($"[DifyConfig] Configuration is valid: {GetConfigurationSummary()}");
            }
            else
            {
                Debug.LogError($"[DifyConfig] Configuration errors:\n{string.Join("\n", errors)}");
            }
        }

        /// <summary>
        /// Inspectorから設定サマリーを表示
        /// </summary>
        [ContextMenu("Show Configuration Summary")]
        private void ShowConfigurationSummaryFromInspector()
        {
            Debug.Log($"[DifyConfig] {GetConfigurationSummary()}");
        }

        /// <summary>
        /// Inspectorからサンプル設定を適用
        /// </summary>
        [ContextMenu("Apply Sample Configuration")]
        private void ApplySampleConfigurationFromInspector()
        {
            _apiUrl = "https://api.dify.ai/v1/chat-messages";
            _enableAudioProcessing = true;
            _maxConcurrentConnections = 2;
            _maxRetryCount = 3;
            _enableDebugLogging = true;
            
            Debug.Log("[DifyConfig] Applied sample configuration. Please set your API Key.");
            
            // Unity Editorでのみ実行される処理
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 設定情報のサマリーを取得
        /// </summary>
        /// <returns>設定情報の文字列</returns>
        public string GetConfigurationSummary()
        {
            var hasApiKey = !string.IsNullOrEmpty(_apiKey);
            var maskedApiKey = hasApiKey ? $"{_apiKey.Substring(0, 9)}..." : "Not Set";
            
            return $"API Key: {maskedApiKey}, " +
                   $"URL: {_apiUrl}, " +
                   $"Audio: {_enableAudioProcessing}, " +
                   $"Max Connections: {_maxConcurrentConnections}, " +
                   $"Max Retry: {_maxRetryCount}, " +
                   $"Debug: {_enableDebugLogging}";
        }

        /// <summary>
        /// DifyServiceConfigオブジェクトに変換
        /// </summary>
        /// <returns>DifyServiceConfig インスタンス</returns>
        public DifyServiceConfig ToDifyServiceConfig()
        {
            return new DifyServiceConfig
            {
                ApiKey = _apiKey,
                ApiUrl = _apiUrl,
                EnableAudioProcessing = _enableAudioProcessing
            };
        }

        /// <summary>
        /// DifyServiceConfigから設定を読み込み
        /// </summary>
        /// <param name="config">読み込むDifyServiceConfig</param>
        public void FromDifyServiceConfig(DifyServiceConfig config)
        {
            if (config == null)
                return;

            _apiKey = config.ApiKey;
            _apiUrl = config.ApiUrl;
            _enableAudioProcessing = config.EnableAudioProcessing;

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// 設定をJSON文字列として出力
        /// デバッグ・ログ用（API Keyは隠蔽）
        /// </summary>
        /// <returns>JSON形式の設定文字列</returns>
        public string ToJsonString()
        {
            var maskedApiKey = string.IsNullOrEmpty(_apiKey) ? "Not Set" : $"{_apiKey.Substring(0, 9)}...";
            
            return $@"{{
    ""apiKey"": ""{maskedApiKey}"",
    ""apiUrl"": ""{_apiUrl}"",
    ""enableAudioProcessing"": {_enableAudioProcessing.ToString().ToLower()},
    ""maxConcurrentConnections"": {_maxConcurrentConnections},
    ""maxRetryCount"": {_maxRetryCount},
    ""enableDebugLogging"": {_enableDebugLogging.ToString().ToLower()}
}}";
        }

        #endregion

        #region Unity Editor Validation

        /// <summary>
        /// Unity Inspector値変更時の検証
        /// </summary>
        private void OnValidate()
        {
            // 範囲チェック
            _maxConcurrentConnections = Mathf.Clamp(_maxConcurrentConnections, 1, 5);
            _maxRetryCount = Mathf.Clamp(_maxRetryCount, 1, 10);

            // URL形式の基本チェック
            if (!string.IsNullOrWhiteSpace(_apiUrl) && !_apiUrl.StartsWith("http"))
            {
                Debug.LogWarning("[DifyConfig] API URL should start with http:// or https://");
            }

            // API Key形式の基本チェック
            if (!string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.StartsWith("app-"))
            {
                Debug.LogWarning("[DifyConfig] API Key should start with 'app-'");
            }
        }

        #endregion
    }
}