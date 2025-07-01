#nullable enable
using UnityEngine;

namespace AiTuber.Dify
{
    /// <summary>
    /// OneComme + Dify統合用インストーラー（全体設定管理）
    /// </summary>
    public class Installer : MonoBehaviour
    {
        [Header("Global Settings")]
        [SerializeField] private bool enableDebugLogging = true;

        [Header("OneComme Configuration")]
        [SerializeField] private bool autoConnect = true;
        [SerializeField] private float gapBetweenAudio = 1.0f;
        [SerializeField] private float gapBetweenDifyRequests = 10.0f;

        [Header("Chat Components")]
        [SerializeField] private OneCommeClient? oneCommeClient;
        [SerializeField] private NodeChainController? nodeChainController;
        [SerializeField] private AudioPlayer? audioPlayer;
        [SerializeField] private AudioSource? audioSource;

        private DifyClient? difyClient;

        /// <summary>
        /// 初期化フラグ
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// MonoBehaviour初期化
        /// </summary>
        private void Awake()
        {
            InitializeDifyBlockingSystem();
        }

        /// <summary>
        /// OneComme統合システム全体初期化
        /// </summary>
        private void InitializeDifyBlockingSystem()
        {
            try
            {
                // PlayerPrefsから設定読み込み
                var oneCommeUrl = PlayerPrefs.GetString(Constants.PlayerPrefs.OneCommeUrl);
                var difyUrl = PlayerPrefs.GetString(Constants.PlayerPrefs.DifyUrl);
                var apiKey = PlayerPrefs.GetString(Constants.PlayerPrefs.DifyApiKey);

                // 設定バリデーション
                if (!ValidateConfiguration(oneCommeUrl, difyUrl, apiKey))
                {
                    Debug.LogError("[Installer] 設定が無効です");
                    return;
                }

                // 全コンポーネントに依存注入
                InstallComponents(oneCommeUrl);

                // DifyClient作成
                difyClient = new DifyClient(difyUrl, apiKey, enableDebugLogging);

                // NodeChainControllerの依存関係構築
                if (oneCommeClient != null && audioPlayer != null && nodeChainController != null)
                {
                    nodeChainController.Initialize(oneCommeClient, audioPlayer, difyClient, gapBetweenAudio, gapBetweenDifyRequests, enableDebugLogging);
                }

                IsInitialized = true;
                
                if (enableDebugLogging)
                {
                    Debug.Log("[Installer] OneComme統合システム初期化完了");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Installer] 初期化失敗: {ex.Message}");
                IsInitialized = false;
            }
        }

        /// <summary>
        /// 全コンポーネントの依存注入
        /// </summary>
        private void InstallComponents(string oneCommeUrl)
        {
            // OneCommeClientの依存注入
            if (oneCommeClient != null)
            {
                oneCommeClient.Install(oneCommeUrl, autoConnect, enableDebugLogging);
            }

            // AudioPlayerの依存注入
            if (audioPlayer != null && audioSource != null)
            {
                audioPlayer.Install(audioSource, enableDebugLogging);
            }

        }

        /// <summary>
        /// 設定バリデーション
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        private bool ValidateConfiguration(string oneCommeUrl, string difyUrl, string apiKey)
        {
            // OneComme設定バリデーション
            if (string.IsNullOrWhiteSpace(oneCommeUrl))
            {
                Debug.LogError($"[Installer] OneComme URLが設定されていません (PlayerPrefs: {Constants.PlayerPrefs.OneCommeUrl})");
                return false;
            }

            // Dify設定バリデーション
            if (string.IsNullOrWhiteSpace(difyUrl))
            {
                Debug.LogError($"[Installer] Dify URLが設定されていません (PlayerPrefs: {Constants.PlayerPrefs.DifyUrl})");
                return false;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogError($"[Installer] API Keyが設定されていません (PlayerPrefs: {Constants.PlayerPrefs.DifyApiKey})");
                return false;
            }

            return true;
        }

        /// <summary>
        /// PlayerPrefs設定用ヘルパー（エディタ用）
        /// </summary>
        [ContextMenu("Show PlayerPrefs Keys")]
        public void ShowPlayerPrefsKeys()
        {
            Debug.Log("[Installer] PlayerPrefs設定が必要:");
            Debug.Log($"PlayerPrefs.SetString(\"{Constants.PlayerPrefs.OneCommeUrl}\", \"ws://localhost:11180/\")");
            Debug.Log($"PlayerPrefs.SetString(\"{Constants.PlayerPrefs.DifyUrl}\", \"https://dify.seiichirou.jp/v1/chat-messages\")");
            Debug.Log($"PlayerPrefs.SetString(\"{Constants.PlayerPrefs.DifyApiKey}\", \"your-api-key-here\")");
        }

        /// <summary>
        /// システム終了時のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            difyClient?.Dispose();
            IsInitialized = false;
            
            if (enableDebugLogging)
            {
                Debug.Log("[Installer] システム終了・リソース解放完了");
            }
        }
    }
}