#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// 2つのチャンクノードチェーンを管理するコントローラー - DifyProcessingChunkedNode + SubtitleAudioNode
    /// </summary>
    public class NodeChainChunkedController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string logPrefix = "[NodeChainChunkedController]";
        [SerializeField] private int maxChainLength = 5;

        private float gapBetweenAudio = 1.0f;
        private float gapBetweenDifyRequests = 15.0f;

        private OneCommeClient? oneCommeClient;
        private DifyChunkedClient? difyChunkedClient;
        private BufferedAudioPlayer? bufferedAudioPlayer;
        private bool debugLog;

        // チャンクダウンロードチェーン管理
        private DifyProcessingChunkedNode? lastDifyProcessingChunkedNode = null;
        private CancellationTokenSource? downloadCancellationTokenSource;

        // 字幕音声再生チェーン管理
        private SubtitleAudioNode? lastSubtitleAudioNode = null;
        private CancellationTokenSource? playbackCancellationTokenSource;

        // ユーザーコメント累積カウンター（荒らし対策）
        private readonly Dictionary<string, int> userCommentCounts = new();

        /// <summary>
        /// コンポーネント初期化（Installerから呼び出し）
        /// </summary>
        /// <param name="client">OneCommeClient</param>
        /// <param name="difyChunkedClient">DifyChunkedClient</param>
        /// <param name="bufferedAudioPlayer">BufferedAudioPlayer</param>
        /// <param name="audioGap">音声再生間隔</param>
        /// <param name="difyGap">Dify API呼び出し間隔</param>
        /// <param name="enableDebugLog">DebugLog有効フラグ</param>
        public void Initialize(OneCommeClient client, DifyChunkedClient difyChunkedClient, BufferedAudioPlayer bufferedAudioPlayer, float audioGap, float difyGap, bool enableDebugLog)
        {
            oneCommeClient = client ?? throw new ArgumentNullException(nameof(client));
            this.difyChunkedClient = difyChunkedClient ?? throw new ArgumentNullException(nameof(difyChunkedClient));
            this.bufferedAudioPlayer = bufferedAudioPlayer ?? throw new ArgumentNullException(nameof(bufferedAudioPlayer));
            gapBetweenAudio = audioGap;
            gapBetweenDifyRequests = difyGap;
            debugLog = enableDebugLog;

            Debug.Log($"{logPrefix} 初期化完了 - AudioGap: {audioGap}秒, DifyGap: {difyGap}秒, DebugLog: {enableDebugLog}");
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

            if (debugLog) Debug.Log($"{logPrefix} チャンク2チェーンシステム初期化完了");
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

            // DifyProcessingChunkedNodeイベント設定
            DifyProcessingChunkedNode.OnDifyProcessingChainCompleted += HandleDownloadChainCompleted;
            DifyProcessingChunkedNode.OnSubtitleAudioNodeCreated += HandleSubtitleAudioNodeCreated;
            if (debugLog) Debug.Log($"{logPrefix} DifyProcessingChunkedNodeイベント設定完了");

            // SubtitleAudioNode完了イベント設定
            SubtitleAudioNode.OnChainCompleted += HandleSubtitleAudioChainCompleted;
            if (debugLog) Debug.Log($"{logPrefix} SubtitleAudioNodeイベント設定完了");
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

                if (IsCommentOutMessage(comment))
                {
                    Debug.Log($"{logPrefix} コメントアウトメッセージ - ユーザー: {userName}, コメント: {comment.data?.comment}");
                }
                // DifyProcessingChunkedNode作成してチェーンに追加
                else if (difyChunkedClient != null && bufferedAudioPlayer != null)
                {
                    var downloadNode = new DifyProcessingChunkedNode(
                        comment,
                        userName,
                        difyChunkedClient,
                        bufferedAudioPlayer,
                        gapBetweenAudio,
                        gapBetweenDifyRequests,
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
        private bool IsCommentOutMessage(OneCommeComment comment)
        {
            // コメントアウト（質問の回答を作らない）メッセージかを判定
            return comment.data?.comment?.StartsWith("#") ?? false;
        }

        /// <summary>
        /// DifyProcessingChunkedNodeをダウンロードチェーンに追加
        /// </summary>
        /// <param name="newNode">追加するダウンロードノード</param>
        private void AddToDownloadChain(DifyProcessingChunkedNode newNode)
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

            if (lastDifyProcessingChunkedNode != null)
            {
                // 既存チェーンに追加
                lastDifyProcessingChunkedNode.Next = newNode;
                if (debugLog) Debug.Log($"{logPrefix} チャンクダウンロードノードをチェーンに追加: {newNode}");
            }
            else
            {
                // 新しいチェーンを開始
                if (debugLog) Debug.Log($"{logPrefix} 新しいチャンクダウンロードチェーン開始: {newNode}");
                newNode.ProcessAndContinue(downloadCancellationTokenSource?.Token ?? default);
            }

            lastDifyProcessingChunkedNode = newNode;
        }

        /// <summary>
        /// DifyProcessingChunkedNodeからSubtitleAudioNode作成完了イベントハンドラー
        /// </summary>
        /// <param name="subtitleAudioNode">作成されたSubtitleAudioNode</param>
        private void HandleSubtitleAudioNodeCreated(SubtitleAudioNode subtitleAudioNode)
        {
            try
            {
                if (debugLog) Debug.Log($"{logPrefix} SubtitleAudioNode受信 - 字幕音声チェーンに追加: [{subtitleAudioNode.UserName}]");

                AddToSubtitleAudioChain(subtitleAudioNode);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} SubtitleAudioNode作成イベント処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// SubtitleAudioNodeを字幕音声再生チェーンに追加
        /// </summary>
        /// <param name="newNode">追加する字幕音声ノード</param>
        private void AddToSubtitleAudioChain(SubtitleAudioNode newNode)
        {
            if (lastSubtitleAudioNode != null)
            {
                // 既存チェーンに追加
                lastSubtitleAudioNode.Next = newNode;
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声ノードをチェーンに追加: {newNode}");
            }
            else
            {
                // 新しいチェーンを開始
                if (debugLog) Debug.Log($"{logPrefix} 新しい字幕音声再生チェーン開始: {newNode}");
                newNode.ProcessAndContinue(playbackCancellationTokenSource?.Token ?? default);
            }

            lastSubtitleAudioNode = newNode;
        }

        /// <summary>
        /// ダウンロードチェーン完了イベントハンドラー
        /// </summary>
        /// <param name="completedNode">完了したダウンロードノード</param>
        private void HandleDownloadChainCompleted(DifyProcessingChunkedNode completedNode)
        {
            if (lastDifyProcessingChunkedNode == completedNode)
            {
                lastDifyProcessingChunkedNode = null;
                if (debugLog) Debug.Log($"{logPrefix} チャンクダウンロードチェーン完了 - 新しいコメント受付可能");
            }
        }

        /// <summary>
        /// 字幕音声再生チェーン完了イベントハンドラー
        /// </summary>
        /// <param name="completedNode">完了した字幕音声ノード</param>
        private void HandleSubtitleAudioChainCompleted(SubtitleAudioNode completedNode)
        {
            if (lastSubtitleAudioNode == completedNode)
            {
                lastSubtitleAudioNode = null;
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生チェーン完了");
            }
        }

        /// <summary>
        /// ダウンロードチェーンをキャンセル
        /// </summary>
        public void CancelDownloadChain()
        {
            if (downloadCancellationTokenSource != null && !downloadCancellationTokenSource.IsCancellationRequested)
            {
                if (debugLog) Debug.Log($"{logPrefix} チャンクダウンロードチェーンキャンセル実行");
                downloadCancellationTokenSource.Cancel();

                downloadCancellationTokenSource.Dispose();
                downloadCancellationTokenSource = new CancellationTokenSource();
                lastDifyProcessingChunkedNode = null;
            }
        }

        /// <summary>
        /// 字幕音声再生チェーンをキャンセル
        /// </summary>
        public void CancelPlaybackChain()
        {
            if (playbackCancellationTokenSource != null && !playbackCancellationTokenSource.IsCancellationRequested)
            {
                if (debugLog) Debug.Log($"{logPrefix} 字幕音声再生チェーンキャンセル実行");
                playbackCancellationTokenSource.Cancel();

                playbackCancellationTokenSource.Dispose();
                playbackCancellationTokenSource = new CancellationTokenSource();
                lastSubtitleAudioNode = null;
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
            DifyProcessingChunkedNode.OnDifyProcessingChainCompleted -= HandleDownloadChainCompleted;
            DifyProcessingChunkedNode.OnSubtitleAudioNodeCreated -= HandleSubtitleAudioNodeCreated;
            SubtitleAudioNode.OnChainCompleted -= HandleSubtitleAudioChainCompleted;

            // チェーンをキャンセルしてクリア
            downloadCancellationTokenSource?.Cancel();
            downloadCancellationTokenSource?.Dispose();
            downloadCancellationTokenSource = null;

            playbackCancellationTokenSource?.Cancel();
            playbackCancellationTokenSource?.Dispose();
            playbackCancellationTokenSource = null;

            lastDifyProcessingChunkedNode = null;
            lastSubtitleAudioNode = null;

            if (debugLog) Debug.Log($"{logPrefix} チャンク2チェーンシステムリソース解放完了");
        }
    }
}