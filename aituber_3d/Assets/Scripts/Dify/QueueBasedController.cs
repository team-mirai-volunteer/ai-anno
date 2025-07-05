#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UniRx;

namespace AiTuber.Dify
{
    /// <summary>
    /// キューベース制御クラス - DifyQueue と EventQueue の2段階キューシステム
    /// </summary>
    public class QueueBasedController : MonoBehaviour
    {
        private float taskTimeoutSeconds;
        private float gapBetweenAudioSeconds;
        private float gapAfterAudioSeconds;
        private float initialCommentCooldownSeconds;
        private bool enableDebugLog;
        
        private bool isFirstCommentReceived = false;
        private float firstCommentTime;
        private bool cooldownEndLogged = false;
        
        private DifyChunkedClient? difyClient;
        private DifyAudioFetcher? audioFetcher;
        private AudioSource? audioSource;
        
        // UI統合用
        private MainUIController? mainUIController;
        private MainUI? mainUI;
        
        private readonly Queue<DifyProcessingTask> difyQueue = new();
        private readonly Queue<IEventNode> eventQueue = new();
        private readonly string logPrefix = "[QueueBasedController]";
        
        private bool isProcessing = false;
        private bool isDifyProcessing = false;
        private bool isEventProcessing = false;
        private CancellationTokenSource? cancellationTokenSource;
        private CompositeDisposable? disposables;
        
        /// <summary>
        /// DifyQueue サイズ（デバッグ用）
        /// </summary>
        public int DifyQueueSize => difyQueue.Count;
        
        /// <summary>
        /// EventQueue サイズ（デバッグ用）
        /// </summary>
        public int EventQueueSize => eventQueue.Count;
        
        /// <summary>
        /// 現在処理中かどうか
        /// </summary>
        public bool IsProcessing => isProcessing;

        private void Awake()
        {
            // 初期化処理
        }

        /// <summary>
        /// QueueBasedController初期化（依存注入）
        /// </summary>
        /// <param name="oneCommeClient">OneCommeクライアント</param>
        /// <param name="difyChunkedClient">Difyクライアント</param>
        /// <param name="difyAudioFetcher">音声取得クライアント</param>
        /// <param name="audioSource">音声再生用AudioSource</param>
        /// <param name="taskTimeoutSeconds">タスクタイムアウト秒数</param>
        /// <param name="gapBetweenAudioSeconds">音声再生前のギャップ秒数</param>
        /// <param name="gapAfterAudioSeconds">音声終了後のギャップ秒数</param>
        /// <param name="initialCommentCooldownSeconds">初期コメントクールダウン秒数</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public void Initialize(OneCommeClient oneCommeClient, DifyChunkedClient difyChunkedClient, DifyAudioFetcher difyAudioFetcher, AudioSource audioSource, float taskTimeoutSeconds = 60.0f, float gapBetweenAudioSeconds = 1.0f, float gapAfterAudioSeconds = 2.0f, float initialCommentCooldownSeconds = 10.0f, bool enableDebugLog = true)
        {
            if (oneCommeClient == null) throw new ArgumentNullException(nameof(oneCommeClient));
            this.difyClient = difyChunkedClient ?? throw new ArgumentNullException(nameof(difyChunkedClient));
            this.audioFetcher = difyAudioFetcher ?? throw new ArgumentNullException(nameof(difyAudioFetcher));
            this.audioSource = audioSource ?? throw new ArgumentNullException(nameof(audioSource));
            this.taskTimeoutSeconds = taskTimeoutSeconds;
            this.gapBetweenAudioSeconds = gapBetweenAudioSeconds;
            this.gapAfterAudioSeconds = gapAfterAudioSeconds;
            this.initialCommentCooldownSeconds = initialCommentCooldownSeconds;
            this.enableDebugLog = enableDebugLog;
            
            // OneCommeClientのイベントを購読
            oneCommeClient.OnCommentReceived += ProcessComment;
            
            if (enableDebugLog) Debug.Log($"{logPrefix} QueueBasedController初期化完了");
            
            StartProcessing();
        }
        
        /// <summary>
        /// QueueBasedController初期化（UI統合版）
        /// </summary>
        /// <param name="oneCommeClient">OneCommeクライアント</param>
        /// <param name="difyChunkedClient">Difyクライアント</param>
        /// <param name="difyAudioFetcher">音声取得クライアント</param>
        /// <param name="audioSource">音声再生用AudioSource</param>
        /// <param name="mainUIController">MainUIController</param>
        /// <param name="mainUI">MainUI</param>
        /// <param name="taskTimeoutSeconds">タスクタイムアウト秒数</param>
        /// <param name="gapBetweenAudioSeconds">音声再生前のギャップ秒数</param>
        /// <param name="gapAfterAudioSeconds">音声終了後のギャップ秒数</param>
        /// <param name="initialCommentCooldownSeconds">初期コメントクールダウン秒数</param>
        /// <param name="enableDebugLog">デバッグログ有効フラグ</param>
        public void InitializeWithUI(OneCommeClient oneCommeClient, DifyChunkedClient difyChunkedClient, DifyAudioFetcher difyAudioFetcher, AudioSource audioSource, MainUIController mainUIController, MainUI mainUI, float taskTimeoutSeconds = 60.0f, float gapBetweenAudioSeconds = 1.0f, float gapAfterAudioSeconds = 2.0f, float initialCommentCooldownSeconds = 10.0f, bool enableDebugLog = true)
        {
            // 基本初理化
            Initialize(oneCommeClient, difyChunkedClient, difyAudioFetcher, audioSource, taskTimeoutSeconds, gapBetweenAudioSeconds, gapAfterAudioSeconds, initialCommentCooldownSeconds, enableDebugLog);
            
            // UI統合
            this.mainUIController = mainUIController ?? throw new ArgumentNullException(nameof(mainUIController));
            this.mainUI = mainUI ?? throw new ArgumentNullException(nameof(mainUI));
            
            // SubtitleAudioTaskの字幕イベントをMainUIControllerに接続
            SubtitleAudioTask.OnChunkStarted += mainUIController.HandleChunkStarted;
            
            if (enableDebugLog) Debug.Log($"{logPrefix} UI統合初期化完了");
        }

        private void Start()
        {
            if (enableDebugLog) Debug.Log($"{logPrefix} QueueBasedController開始");
        }

        private void OnDestroy()
        {
            StopProcessing();
            disposables?.Dispose();
            if (enableDebugLog) Debug.Log($"{logPrefix} QueueBasedController終了");
        }

        /// <summary>
        /// OneCommeコメントを処理してDifyTaskを作成・キューに追加
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        public void ProcessComment(OneCommeComment comment)
        {
            if (comment?.data?.comment == null) return;
            if (difyClient == null || audioFetcher == null)
            {
                Debug.LogError($"{logPrefix} 初期化されていません。Initialize()を先に呼び出してください");
                return;
            }
            
            var userName = comment.data.name ?? "匿名";
            
            // 初回コメント受信時刻を記録
            if (!isFirstCommentReceived)
            {
                isFirstCommentReceived = true;
                firstCommentTime = Time.realtimeSinceStartup;
                if (enableDebugLog) Debug.Log($"{logPrefix} 初回コメント受信: [{userName}] クールダウン開始 ({initialCommentCooldownSeconds}秒)");
            }
            
            // 初期コメントクールダウン中は処理をスキップ
            if (isFirstCommentReceived && (Time.realtimeSinceStartup - firstCommentTime) < initialCommentCooldownSeconds)
            {
                var remainingTime = initialCommentCooldownSeconds - (Time.realtimeSinceStartup - firstCommentTime);
                if (enableDebugLog) Debug.Log($"{logPrefix} 初期クールダウン中: [{userName}] 残り{remainingTime:F1}秒");
                return;
            }
            
            // クールダウン終了時のログ（1回だけ）
            if (isFirstCommentReceived && !cooldownEndLogged)
            {
                cooldownEndLogged = true;
                if (enableDebugLog) Debug.Log($"{logPrefix} 初期クールダウン終了: コメント処理を再開します");
            }
            
            // コメントアウト機能: #または全角＃で始まるコメントはDifyリクエストを送らない
            if (IsCommentOutMessage(comment.data.comment))
            {
                if (enableDebugLog) Debug.Log($"{logPrefix} コメントアウト: [{userName}] {comment.data.comment}");
                return;
            }
            
            var difyTask = new DifyProcessingTask(
                comment: comment,
                userName: userName,
                difyClient: difyClient,
                audioFetcher: audioFetcher,
                timeoutSeconds: taskTimeoutSeconds,
                gapBetweenAudioSeconds: gapBetweenAudioSeconds,
                gapAfterAudioSeconds: gapAfterAudioSeconds,
                enableDebugLog: enableDebugLog
            );
            
            EnqueueDifyTask(difyTask);
        }

        /// <summary>
        /// コメントアウト機能: #または全角＃で始まるコメントかどうかを判定
        /// </summary>
        /// <param name="comment">OneCommeコメント</param>
        /// <returns>コメントアウト対象の場合はtrue</returns>
        private bool IsCommentOutMessage(OneCommeComment comment)
        {
            var commentText = comment.data?.comment;
            return commentText?.StartsWith("#") == true || commentText?.StartsWith("＃") == true;
        }

        /// <summary>
        /// DifyProcessingTaskをキューに追加（順次処理待ち）
        /// </summary>
        /// <param name="task">追加するDifyProcessingTask</param>
        public void EnqueueDifyTask(DifyProcessingTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            
            // キューに追加のみ（順次処理はProcessDifyQueueAsyncが担当）
            difyQueue.Enqueue(task);
            if (enableDebugLog) Debug.Log($"{logPrefix} DifyTaskキュー追加: [{task.UserName}] DifyQueue={difyQueue.Count}個");
        }

        /// <summary>
        /// IEventNodeをキューに追加
        /// </summary>
        /// <param name="eventNode">追加するIEventNode</param>
        public void EnqueueEventNode(IEventNode eventNode)
        {
            if (eventNode == null) throw new ArgumentNullException(nameof(eventNode));
            
            eventQueue.Enqueue(eventNode);
            if (enableDebugLog) Debug.Log($"{logPrefix} EventNodeキューに追加: EventQueue={eventQueue.Count}個");
        }

        /// <summary>
        /// キュー処理開始
        /// </summary>
        private void StartProcessing()
        {
            if (isProcessing)
            {
                if (enableDebugLog) Debug.Log($"{logPrefix} 既に処理中のため、処理開始をスキップ");
                return;
            }
            
            cancellationTokenSource = new CancellationTokenSource();
            isProcessing = true;
            
            if (enableDebugLog) Debug.Log($"{logPrefix} キュー処理開始");
            
            // DifyQueueとEventQueueをObservableで独立処理
            disposables = new CompositeDisposable();
            StartDifyQueueObservable();
            StartEventQueueObservable();
        }

        /// <summary>
        /// キュー処理停止
        /// </summary>
        private void StopProcessing()
        {
            if (!isProcessing)
            {
                return;
            }
            
            disposables?.Dispose();
            disposables = null;
            
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            
            isProcessing = false;
            
            if (enableDebugLog) Debug.Log($"{logPrefix} キュー処理停止");
        }

        /// <summary>
        /// DifyQueue専用Observable（順次処理・ノンストップ）
        /// </summary>
        private void StartDifyQueueObservable()
        {
            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    if (isDifyProcessing) return;
                    if (difyQueue.Count == 0) return;
                    
                    isDifyProcessing = true;
                    var task = difyQueue.Dequeue();
                    if (enableDebugLog) Debug.Log($"{logPrefix} {System.DateTime.Now:HH:mm:ss.fff} DifyTask順次処理開始: [{task.UserName}] 残り={difyQueue.Count}個");
                    
                    // Fire-and-forget で非同期処理開始
                    ProcessSingleDifyTaskAsync(task).Forget();
                })
                .AddTo(disposables);
        }
        
        /// <summary>
        /// 単一DifyTask処理（Fire-and-forget用）
        /// </summary>
        /// <param name="task">処理するDifyProcessingTask</param>
        private async UniTaskVoid ProcessSingleDifyTaskAsync(DifyProcessingTask task)
        {
            try
            {
                // タイムアウト付きでDify処理実行（順次・1個ずつ）
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource?.Token ?? default);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(taskTimeoutSeconds));
                await task.StartProcessing(timeoutCts.Token);
                
                // 完了したタスクをEventQueueに追加
                ProcessCompletedDifyTask(task);
            }
            finally
            {
                isDifyProcessing = false;
            }
        }

        /// <summary>
        /// EventQueue専用Observable（音声再生・独立実行）
        /// </summary>
        private void StartEventQueueObservable()
        {
            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    if (isEventProcessing) return;
                    if (eventQueue.Count == 0) return;
                    
                    isEventProcessing = true;
                    var eventNode = eventQueue.Dequeue();
                    if (enableDebugLog) Debug.Log($"{logPrefix} {System.DateTime.Now:HH:mm:ss.fff} EventNode音声再生開始: {eventNode.GetType().Name}");
                    
                    // Fire-and-forget で音声再生開始
                    ProcessSingleEventNodeAsync(eventNode).Forget();
                })
                .AddTo(disposables);
        }
        
        /// <summary>
        /// 単一EventNode処理（Fire-and-forget用）
        /// </summary>
        /// <param name="eventNode">処理するIEventNode</param>
        private async UniTaskVoid ProcessSingleEventNodeAsync(IEventNode eventNode)
        {
            try
            {
                await ProcessEventNode(eventNode, cancellationTokenSource?.Token ?? default);
            }
            finally
            {
                isEventProcessing = false;
            }
        }

        /// <summary>
        /// DifyQueueから完了済みタスクを取得
        /// </summary>
        /// <returns>完了済みDifyProcessingTask（ない場合はnull）</returns>
        private DifyProcessingTask? DequeueDifyTask()
        {
            if (difyQueue.Count > 0)
            {
                var task = difyQueue.Peek();
                if (task.IsReadyToDequeue)
                {
                    difyQueue.Dequeue();
                    if (enableDebugLog) Debug.Log($"{logPrefix} DifyTask完了取得: [{task.UserName}] 残りDifyQueue={difyQueue.Count}個");
                    return task;
                }
            }
            return null;
        }

        /// <summary>
        /// EventQueueからノードを取得
        /// </summary>
        /// <returns>取得されたIEventNode（キューが空の場合はnull）</returns>
        private IEventNode? DequeueEventNode()
        {
            if (eventQueue.Count > 0)
            {
                var eventNode = eventQueue.Dequeue();
                if (enableDebugLog) Debug.Log($"{logPrefix} EventNode取得: 残りEventQueue={eventQueue.Count}個");
                return eventNode;
            }
            return null;
        }

        /// <summary>
        /// 完了済みDifyProcessingTaskをEventQueueに追加
        /// </summary>
        /// <param name="task">完了済みDifyProcessingTask</param>
        private void ProcessCompletedDifyTask(DifyProcessingTask task)
        {
            try
            {
                if (enableDebugLog) Debug.Log($"{logPrefix} 完了済みDifyTask処理: [{task.UserName}]");
                
                // 処理結果チェック
                if (task.HasError)
                {
                    Debug.LogWarning($"{logPrefix} DifyTask処理エラー: [{task.UserName}]");
                    return;
                }
                
                if (!task.IsCompleted)
                {
                    Debug.LogWarning($"{logPrefix} DifyTask未完了: [{task.UserName}]");
                    return;
                }
                
                // 完了したIEventNodeをEventQueueに追加
                var completedEventNode = task.CompletedEventNode;
                if (completedEventNode != null)
                {
                    EnqueueEventNode(completedEventNode);
                    if (enableDebugLog) Debug.Log($"{logPrefix} DifyTask完了→EventQueue追加: [{task.UserName}]");
                    
                    // UIイベント: 音声再生待ち状態通知（Dify処理・音声ダウンロード完了後）
                    if (mainUIController != null && task.DifyResponse != null)
                    {
                        mainUIController.HandleNewCommentQueued(task.Comment, task.UserName);
                        mainUIController.HandleProcessingCompleted(task, task.DifyResponse);
                    }
                }
                else
                {
                    Debug.LogWarning($"{logPrefix} DifyTask完了したがEventNodeがnull: [{task.UserName}]");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} 完了済みDifyTask処理エラー: [{task.UserName}] {ex.Message}");
            }
        }


        /// <summary>
        /// IEventNodeを処理
        /// </summary>
        /// <param name="eventNode">処理するIEventNode</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async UniTask ProcessEventNode(IEventNode eventNode, CancellationToken cancellationToken)
        {
            try
            {
                if (enableDebugLog) Debug.Log($"{logPrefix} EventNode処理開始: {eventNode.GetType().Name}");
                
                // AudioSourceが必要なSubtitleAudioTaskの場合は設定
                if (eventNode is SubtitleAudioTask subtitleTask)
                {
                    if (audioSource == null)
                    {
                        Debug.LogError($"{logPrefix} AudioSourceが未設定: SubtitleAudioTaskを実行できません");
                        return;
                    }
                    
                    // SubtitleAudioTaskにAudioSourceを設定
                    subtitleTask.SetAudioSource(audioSource);
                    
                    if (enableDebugLog) Debug.Log($"{logPrefix} SubtitleAudioTaskにAudioSource設定完了");
                }
                
                // UIイベント: 音声再生開始通知
                if (eventNode is SubtitleAudioTask subtitleTaskForUI)
                {
                    mainUIController?.HandleSubtitleTaskPlayStart(subtitleTaskForUI);
                }
                
                await eventNode.Execute();
                
                // UIイベント: 音声再生完了通知
                if (eventNode is SubtitleAudioTask completedSubtitleTask)
                {
                    mainUIController?.HandleSubtitleTaskCompleted(completedSubtitleTask);
                }
                
                if (enableDebugLog) Debug.Log($"{logPrefix} EventNode処理完了: {eventNode.GetType().Name}");
            }
            catch (OperationCanceledException)
            {
                if (enableDebugLog) Debug.Log($"{logPrefix} EventNode処理キャンセル: {eventNode.GetType().Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{logPrefix} EventNode処理エラー: {eventNode.GetType().Name} {ex.Message}");
            }
        }

        /// <summary>
        /// 全キューをクリア
        /// </summary>
        public void ClearAllQueues()
        {
            var difyCount = difyQueue.Count;
            var eventCount = eventQueue.Count;
            
            difyQueue.Clear();
            eventQueue.Clear();
            
            if (enableDebugLog) Debug.Log($"{logPrefix} 全キュークリア: DifyQueue={difyCount}個, EventQueue={eventCount}個");
        }

        /// <summary>
        /// コメントアウトメッセージかどうかを判定
        /// </summary>
        /// <param name="comment">コメント文字列</param>
        /// <returns>コメントアウトメッセージの場合true</returns>
        private bool IsCommentOutMessage(string comment)
        {
            if (string.IsNullOrEmpty(comment)) return false;
            return comment.StartsWith("#") || comment.StartsWith("＃");
        }

        /// <summary>
        /// キュー状態の文字列表現（デバッグ用）
        /// </summary>
        /// <returns>キュー状態情報</returns>
        public override string ToString()
        {
            var status = isProcessing ? "処理中" : "停止中";
            return $"QueueBasedController [{status}] DifyQueue={DifyQueueSize}, EventQueue={EventQueueSize}";
        }
    }
}