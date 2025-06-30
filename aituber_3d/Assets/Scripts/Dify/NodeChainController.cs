#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// 2つのノードチェーンを管理するコントローラー - DifyProcessingNode + AudioPlaybackNode
    /// </summary>
    public class NodeChainController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string logPrefix = "[NodeChainController]";
        [SerializeField] private int maxChainLength = 5;
        
        private float gapBetweenAudio = 1.0f;
        
        private OneCommeClient? oneCommeClient;
        private AudioPlayer? audioPlayer;
        private DifyClient? difyClient;
        private bool debugLog;

        // ダウンロードチェーン管理
        private DifyProcessingNode? lastDifyProcessingNode = null;
        private CancellationTokenSource? downloadCancellationTokenSource;
        
        // 音声再生チェーン管理
        private AudioPlaybackNode? lastAudioPlaybackNode = null;
        private CancellationTokenSource? playbackCancellationTokenSource;
        
        // ユーザーコメント累積カウンター（荒らし対策）
        private readonly Dictionary<string, int> userCommentCounts = new();
        
        // ノードカウンター（システム負荷監視）
        private static int activeDifyProcessingNodeCount = 0;
        private static int activeAudioPlaybackNodeCount = 0;
        
        /// <summary>
        /// DifyProcessingNodeカウントを增加
        /// </summary>
        public static void IncrementDifyProcessingNodeCount()
        {
            activeDifyProcessingNodeCount++;
            Debug.Log($"[NodeCounter] DifyProcessingNode +1 -> 総数: {activeDifyProcessingNodeCount}");
        }
        
        /// <summary>
        /// DifyProcessingNodeカウントを減少
        /// </summary>
        public static void DecrementDifyProcessingNodeCount()
        {
            activeDifyProcessingNodeCount--;
            Debug.Log($"[NodeCounter] DifyProcessingNode -1 -> 総数: {activeDifyProcessingNodeCount}");
        }
        
        /// <summary>
        /// AudioPlaybackNodeカウントを增加
        /// </summary>
        public static void IncrementAudioPlaybackNodeCount()
        {
            activeAudioPlaybackNodeCount++;
            Debug.Log($"[NodeCounter] AudioPlaybackNode +1 -> 総数: {activeAudioPlaybackNodeCount}");
        }
        
        /// <summary>
        /// AudioPlaybackNodeカウントを減少
        /// </summary>
        public static void DecrementAudioPlaybackNodeCount()
        {
            activeAudioPlaybackNodeCount--;
            Debug.Log($"[NodeCounter] AudioPlaybackNode -1 -> 総数: {activeAudioPlaybackNodeCount}");
        }
        
        /// <summary>
        /// 現在のノード数を取得
        /// </summary>
        public static (int downloadNodes, int commentNodes) GetCurrentNodeCounts()
        {
            return (activeDifyProcessingNodeCount, activeAudioPlaybackNodeCount);
        }

        /// <summary>
        /// コンポーネント初期化（Installerから呼び出し）
        /// </summary>
        /// <param name="client">OneCommeClient</param>
        /// <param name="player">AudioPlayer</param>
        /// <param name="enableDebugLog">DebugLog有効フラグ</param>
        public void Initialize(OneCommeClient client, AudioPlayer player, DifyClient difyClient, float gap, bool enableDebugLog)
        {
            oneCommeClient = client ?? throw new ArgumentNullException(nameof(client));
            audioPlayer = player ?? throw new ArgumentNullException(nameof(player));
            this.difyClient = difyClient ?? throw new ArgumentNullException(nameof(difyClient));
            gapBetweenAudio = gap;
            debugLog = enableDebugLog;
            
            Debug.Log($"{logPrefix} 初期化完了 - Gap: {gap}秒, DebugLog: {enableDebugLog}");
        }

        /// <summary>
        /// 初期化
        /// </summary>
        private void Start()
        {
            SetupEventHandlers();
            
            // CancellationTokenSource初期化
            downloadCancellationTokenSource = new CancellationTokenSource();
            playbackCancellationTokenSource = new CancellationTokenSource();
            
            if (debugLog) Debug.Log($"{logPrefix} AudioPlaybackNode連鎖システム初期化完了");
        }

        /// <summary>
        /// イベントハンドラー設定
        /// </summary>
        private void SetupEventHandlers()
        {
            if (oneCommeClient != null)
            {
                oneCommeClient.OnCommentReceived += HandleOneCommeComment;
                if (debugLog) Debug.Log($"{logPrefix} OneCommeClientイベント設定完了");
            }
            else
            {
                Debug.LogWarning($"{logPrefix} OneCommeClientが設定されていません");
            }

            // DifyProcessingNodeイベント設定
            DifyProcessingNode.OnDifyProcessingChainCompleted += HandleDownloadChainCompleted;
            DifyProcessingNode.OnAudioPlaybackNodeCreated += HandleAudioPlaybackNodeCreated;
            if (debugLog) Debug.Log($"{logPrefix} DifyProcessingNodeイベント設定完了");
            
            // AudioPlaybackNode完了イベント設定
            AudioPlaybackNode.OnChainCompleted += HandleCommentChainCompleted;
            if (debugLog) Debug.Log($"{logPrefix} AudioPlaybackNodeイベント設定完了");
        }

        /// <summary>
        /// OneCommeコメント受信処理
        /// </summary>
        /// <param name="comment">受信したコメント</param>
        private void HandleOneCommeComment(OneCommeComment comment)
        {
            if (comment == null) return;

            try
            {
                var userName = comment.data?.name ?? "匿名";
                
                // DifyProcessingNode作成してチェーンに追加
                if (difyClient != null && audioPlayer != null)
                {
                    var downloadNode = new DifyProcessingNode(
                        comment,
                        userName,
                        difyClient,
                        audioPlayer,
                        gapBetweenAudio,
                        debugLog
                    );
                    
                    AddToDownloadChain(downloadNode);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} コメント処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// DifyProcessingNodeをダウンロードチェーンに追加
        /// </summary>
        /// <param name="newNode">追加するダウンロードノード</param>
        private void AddToDownloadChain(DifyProcessingNode newNode)
        {
            // ユーザーカウンターを更新
            var userName = newNode.UserName;
            if (userCommentCounts.ContainsKey(userName))
            {
                userCommentCounts[userName]++;
            }
            else
            {
                userCommentCounts[userName] = 1;
            }
            
            if (debugLog) Debug.Log($"{logPrefix} ユーザー[{userName}]コメント累積: {userCommentCounts[userName]}");
            
            if (lastDifyProcessingNode != null)
            {
                // 既存チェーンに追加
                lastDifyProcessingNode.Next = newNode;
                if (debugLog) Debug.Log($"{logPrefix} ダウンロードノードをチェーンに追加: {newNode}");
                
                // チェーン長チェックとリビルド処理は後で実装
            }
            else
            {
                // 新しいチェーンを開始
                if (debugLog) Debug.Log($"{logPrefix} 新しいダウンロードチェーン開始: {newNode}");
                newNode.ProcessAndContinue(downloadCancellationTokenSource?.Token ?? default);
            }

            lastDifyProcessingNode = newNode;
        }

        /// <summary>
        /// DifyProcessingNodeからAudioPlaybackNode作成完了イベントハンドラー
        /// </summary>
        /// <param name="commentNode">作成されたAudioPlaybackNode</param>
        private void HandleAudioPlaybackNodeCreated(AudioPlaybackNode commentNode)
        {
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} AudioPlaybackNode受信 - 再生チェーンに追加: [{commentNode.UserName}]");
                
                AddToCommentChain(commentNode);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} AudioPlaybackNode作成イベント処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// AudioPlaybackNodeを音声再生チェーンに追加
        /// </summary>
        /// <param name="newNode">追加するコメントノード</param>
        private void AddToCommentChain(AudioPlaybackNode newNode)
        {
            if (lastAudioPlaybackNode != null)
            {
                // 既存チェーンに追加
                lastAudioPlaybackNode.Next = newNode;
                if (debugLog) Debug.Log($"{logPrefix} コメントノードを再生チェーンに追加: {newNode}");
            }
            else
            {
                // 新しいチェーンを開始
                if (debugLog) Debug.Log($"{logPrefix} 新しい音声再生チェーン開始: {newNode}");
                newNode.ProcessAndContinue(playbackCancellationTokenSource?.Token ?? default);
            }

            lastAudioPlaybackNode = newNode;
        }

        /// <summary>
        /// ダウンロードチェーン完了イベントハンドラー
        /// </summary>
        /// <param name="completedNode">完了したダウンロードノード</param>
        private void HandleDownloadChainCompleted(DifyProcessingNode completedNode)
        {
            if (lastDifyProcessingNode == completedNode)
            {
                lastDifyProcessingNode = null;
                if (debugLog) Debug.Log($"{logPrefix} ダウンロードチェーン完了 - 新しいコメント受付可能");
            }
        }
        
        /// <summary>
        /// 音声再生チェーン完了イベントハンドラー
        /// </summary>
        /// <param name="completedNode">完了したコメントノード</param>
        private void HandleCommentChainCompleted(AudioPlaybackNode completedNode)
        {
            if (lastAudioPlaybackNode == completedNode)
            {
                lastAudioPlaybackNode = null;
                if (debugLog) Debug.Log($"{logPrefix} 音声再生チェーン完了");
            }
        }

        
        /// <summary>
        /// ダウンロードチェーンをキャンセル
        /// </summary>
        public void CancelDownloadChain()
        {
            if (downloadCancellationTokenSource != null && !downloadCancellationTokenSource.IsCancellationRequested)
            {
                if (debugLog) Debug.Log($"{logPrefix} ダウンロードチェーンキャンセル実行");
                downloadCancellationTokenSource.Cancel();
                
                downloadCancellationTokenSource.Dispose();
                downloadCancellationTokenSource = new CancellationTokenSource();
                lastDifyProcessingNode = null;
            }
        }
        
        /// <summary>
        /// 音声再生チェーンをキャンセル
        /// </summary>
        public void CancelPlaybackChain()
        {
            if (playbackCancellationTokenSource != null && !playbackCancellationTokenSource.IsCancellationRequested)
            {
                if (debugLog) Debug.Log($"{logPrefix} 音声再生チェーンキャンセル実行");
                playbackCancellationTokenSource.Cancel();
                
                playbackCancellationTokenSource.Dispose();
                playbackCancellationTokenSource = new CancellationTokenSource();
                lastAudioPlaybackNode = null;
            }
        }
        
        /// <summary>
        /// 両方のチェーンをキャンセル
        /// </summary>
        public void CancelAllChains()
        {
            CancelDownloadChain();
            CancelPlaybackChain();
        }

        /// <summary>
        /// リソース解放
        /// </summary>
        private void OnDestroy()
        {
            if (oneCommeClient != null)
            {
                oneCommeClient.OnCommentReceived -= HandleOneCommeComment;
            }
            
            // イベント解除
            DifyProcessingNode.OnDifyProcessingChainCompleted -= HandleDownloadChainCompleted;
            DifyProcessingNode.OnAudioPlaybackNodeCreated -= HandleAudioPlaybackNodeCreated;
            AudioPlaybackNode.OnChainCompleted -= HandleCommentChainCompleted;
            
            // チェーンをキャンセルしてクリア
            downloadCancellationTokenSource?.Cancel();
            downloadCancellationTokenSource?.Dispose();
            downloadCancellationTokenSource = null;
            
            playbackCancellationTokenSource?.Cancel();
            playbackCancellationTokenSource?.Dispose();
            playbackCancellationTokenSource = null;
            
            lastDifyProcessingNode = null;
            lastAudioPlaybackNode = null;
            
            if (debugLog) Debug.Log($"{logPrefix} 2チェーンシステムリソース解放完了");
        }
    }
}