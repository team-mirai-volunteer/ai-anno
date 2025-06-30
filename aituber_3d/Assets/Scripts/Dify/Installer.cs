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
        [SerializeField] private string oneCommeUrl = "ws://localhost:11180/";
        [SerializeField] private bool autoConnect = true;

        [Header("Dify Configuration")]
        [SerializeField] private string difyUrl = "http://localhost/v1/chat-messages";
        [SerializeField] private string apiKey = "";
        [SerializeField] private float gapBetweenAudio = 1.0f;

        [Header("Chat Components")]
        [SerializeField] private OneCommeClient? oneCommeClient;
        [SerializeField] private NodeChainController? nodeChainController;
        [SerializeField] private AudioPlayer? audioPlayer;

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
                // 設定バリデーション
                if (!ValidateConfiguration())
                {
                    Debug.LogError("[Installer] 設定が無効です");
                    return;
                }

                // 全コンポーネントにDebugLog設定を注入
                ConfigureComponents();

                // DifyClient作成
                difyClient = new DifyClient(difyUrl, apiKey, enableDebugLogging);

                // NodeChainControllerの依存関係構築
                if (oneCommeClient != null && audioPlayer != null && nodeChainController != null)
                {
                    nodeChainController.Initialize(oneCommeClient, audioPlayer, difyClient, gapBetweenAudio, enableDebugLogging);
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
        /// 全コンポーネントの設定統一
        /// </summary>
        private void ConfigureComponents()
        {
            // OneCommeClientの設定注入
            if (oneCommeClient != null)
            {
                oneCommeClient.Configure(oneCommeUrl, autoConnect, enableDebugLogging);
            }

            // AudioPlayerのDebugLog設定
            if (audioPlayer != null)
            {
                audioPlayer.Configure(enableDebugLogging);
            }

        }

        /// <summary>
        /// 設定バリデーション
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        private bool ValidateConfiguration()
        {
            // OneComme設定バリデーション
            if (string.IsNullOrWhiteSpace(oneCommeUrl))
            {
                Debug.LogError("[Installer] OneComme URLが設定されていません");
                return false;
            }

            if (!System.Uri.TryCreate(oneCommeUrl, System.UriKind.Absolute, out _))
            {
                Debug.LogError($"[Installer] 無効なOneComme URL形式: {oneCommeUrl}");
                return false;
            }

            // Dify設定バリデーション
            if (string.IsNullOrWhiteSpace(difyUrl))
            {
                Debug.LogError("[Installer] Dify URLが設定されていません");
                return false;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogError("[Installer] API Keyが設定されていません");
                return false;
            }

            if (!System.Uri.TryCreate(difyUrl, System.UriKind.Absolute, out _))
            {
                Debug.LogError($"[Installer] 無効なDify URL形式: {difyUrl}");
                return false;
            }

            return true;
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