#nullable enable
using UnityEngine;
using AiTuber;

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
        [SerializeField] private bool enableAutoReconnect = true;
        [SerializeField] private float reconnectInterval = 0.5f;
        [SerializeField] private float gapBetweenAudio = 1.0f;
        [SerializeField] private float gapBetweenDifyRequests = 10.0f;
        [SerializeField] private float gapAfterAudio = 2.0f;

        [Header("Chat Components")]
        [SerializeField] private OneCommeClient? oneCommeClient;
        [SerializeField] private QueueBasedController? queueBasedController;
        [SerializeField] private AudioSource? audioSource;

        [Header("UI Components")]
        [SerializeField] private MainUIController? mainUIController;
        [SerializeField] private MainUI? mainUI;

        private DifyChunkedClient? difyChunkedClient;
        private DifyAudioFetcher? difyAudioFetcher;

        /// <summary>
        /// 初期化フラグ
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// MonoBehaviour初期化
        /// </summary>
        private void Awake()
        {
            InitializeDifyChunkedSystem();
        }


        /// <summary>
        /// Difyチャンクシステム全体初期化
        /// </summary>
        private void InitializeDifyChunkedSystem()
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
                    Debug.LogError("[Installer] チャンク版設定が無効です");
                    return;
                }

                // 基本コンポーネント初期化
                oneCommeClient = InstallComponents(oneCommeUrl);
                difyChunkedClient = new DifyChunkedClient(difyUrl, apiKey, enableDebugLogging);
                difyAudioFetcher = new DifyAudioFetcher(60, enableDebugLogging);

                // 新キューシステム初期化（既存システム削除により単一システム）
                InitializeNewQueueSystem();

                IsInitialized = true;
                
                if (enableDebugLogging)
                {
                    Debug.Log("[Installer] Difyチャンクシステム初期化完了");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Installer] チャンク版初期化失敗: {ex.Message}");
                IsInitialized = false;
            }
        }

        /// <summary>
        /// キューベースシステム初期化
        /// </summary>
        private void InitializeNewQueueSystem()
        {
            // 依存性チェック
            Debug.Assert(queueBasedController != null, "[Installer] QueueBasedControllerが設定されていません");
            Debug.Assert(audioSource != null, "[Installer] AudioSourceが設定されていません");
            Debug.Assert(oneCommeClient != null, "[Installer] OneCommeClientが設定されていません");
            Debug.Assert(difyChunkedClient != null, "[Installer] DifyChunkedClientが設定されていません");
            Debug.Assert(difyAudioFetcher != null, "[Installer] DifyAudioFetcherが設定されていません");
            
            if (mainUIController != null && mainUI != null)
            {
                // UI統合版初期化
                queueBasedController.InitializeWithUI(
                    oneCommeClient, difyChunkedClient, difyAudioFetcher, audioSource, 
                    mainUIController, mainUI, 60.0f, gapBetweenAudio, gapAfterAudio, gapBetweenDifyRequests, enableDebugLogging);
            }
            else
            {
                // UI無し版初理化
                queueBasedController.Initialize(
                    oneCommeClient, difyChunkedClient, difyAudioFetcher, audioSource, 
                    60.0f, gapBetweenAudio, gapAfterAudio, gapBetweenDifyRequests, enableDebugLogging);
            }
            
            if (enableDebugLogging) Debug.Log("[Installer] キューベースシステム初期化完了");
        }


        /// <summary>
        /// 全コンポーネントの依存注入
        /// </summary>
        private OneCommeClient? InstallComponents(string oneCommeUrl)
        {
            // OneCommeClientの依存注入
            if (oneCommeClient != null)
            {
                oneCommeClient.Install(oneCommeUrl, autoConnect, enableDebugLogging, enableAutoReconnect, reconnectInterval);
            }

            return oneCommeClient;
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
            Debug.Log($"PlayerPrefs.SetString(\"{Constants.PlayerPrefs.DifyUrl}\", \"https://your-dify-server.com/v1/chat-messages\")");
            Debug.Log($"PlayerPrefs.SetString(\"{Constants.PlayerPrefs.DifyApiKey}\", \"your-api-key-here\")");
        }

        /// <summary>
        /// システム終了時のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            difyChunkedClient?.Dispose();
            IsInitialized = false;
            
            if (enableDebugLogging)
            {
                Debug.Log("[Installer] システム終了・リソース解放完了");
            }
        }
    }
}