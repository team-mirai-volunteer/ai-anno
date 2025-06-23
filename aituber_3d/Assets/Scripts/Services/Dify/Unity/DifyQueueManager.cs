using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using AiTuber.Services.Dify;
using AiTuber.Services.Dify.Data;
using Aituber;

namespace AiTuber.Services.Dify.Unity
{
    /// <summary>
    /// Dify統合キューマネージャー - MonoBehaviourラッパー
    /// 既存のQueueManagerに代わるDify統合版
    /// Pure C# DifyServiceをUnity環境で実行するためのアダプター
    /// </summary>
    public class DifyQueueManager : MonoBehaviour
    {
        [Header("Dify Configuration")]
        [SerializeField] 
        private DifyConfigAsset _difyConfigAsset;

        [Header("Unity Component References")]
        [SerializeField] 
        private TextToSpeech _textToSpeech;
        
        [SerializeField] 
        private QueueIndicator _queueIndicator;
        
        [SerializeField] 
        private NewCommentIndicatorQueue _newCommentIndicatorQueue;

        [Header("Processing Settings")]
        [SerializeField] 
        private int _maxConcurrentRequests = 2;
        
        [SerializeField] 
        private float _processingIntervalSeconds = 1.0f;

        [SerializeField]
        private bool _enableDebugLogging = true;

        // Dify統合コンポーネント
        private DifyService _difyService;
        private DifyApiClient _apiClient;
        private DifyQueueManagerAdapter _adapter;
        private DifyServiceConfig _config;

        // 処理キュー
        private Queue<Question> _inputQueue = new Queue<Question>();
        private Queue<Question> _processingQueue = new Queue<Question>();
        
        // 制御フラグ
        private bool _isProcessing = false;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _queueLock = new object();

        // 統計情報
        private int _totalProcessedQuestions = 0;
        private int _successfulResponses = 0;
        private int _failedResponses = 0;

        #region Unity Lifecycle

        private void Awake()
        {
            ValidateComponents();
            InitializeDifyService();
        }

        private void Start()
        {
            StartProcessing();
        }

        private void OnDestroy()
        {
            StopProcessing();
        }

        private void OnValidate()
        {
            // Inspector値変更時の検証
            _maxConcurrentRequests = Mathf.Clamp(_maxConcurrentRequests, 1, 10);
            _processingIntervalSeconds = Mathf.Clamp(_processingIntervalSeconds, 0.1f, 10.0f);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 必要なコンポーネントの検証
        /// </summary>
        private void ValidateComponents()
        {
            if (_difyConfigAsset == null)
            {
                Debug.LogError("[DifyQueueManager] DifyConfigAsset is not assigned!");
                enabled = false;
                return;
            }

            if (_textToSpeech == null)
            {
                _textToSpeech = FindObjectOfType<TextToSpeech>();
                if (_textToSpeech == null)
                {
                    Debug.LogError("[DifyQueueManager] TextToSpeech component not found!");
                    enabled = false;
                    return;
                }
            }

            if (_queueIndicator == null)
            {
                _queueIndicator = FindObjectOfType<QueueIndicator>();
                if (_queueIndicator == null)
                {
                    Debug.LogWarning("[DifyQueueManager] QueueIndicator not found - queue visualization disabled");
                }
            }

            if (_newCommentIndicatorQueue == null)
            {
                _newCommentIndicatorQueue = FindObjectOfType<NewCommentIndicatorQueue>();
                if (_newCommentIndicatorQueue == null)
                {
                    Debug.LogWarning("[DifyQueueManager] NewCommentIndicatorQueue not found - comment indication disabled");
                }
            }
        }

        /// <summary>
        /// Difyサービスの初期化
        /// </summary>
        private void InitializeDifyService()
        {
            try
            {
                // 設定の作成
                _config = new DifyServiceConfig
                {
                    ApiKey = _difyConfigAsset.ApiKey,
                    ApiUrl = _difyConfigAsset.ApiUrl,
                    EnableAudioProcessing = _difyConfigAsset.EnableAudioProcessing
                };

                // 設定の検証
                if (!_config.IsValid)
                {
                    Debug.LogError($"[DifyQueueManager] Invalid Dify configuration: {GetConfigurationSummary()}");
                    enabled = false;
                    return;
                }

                // APIクライアントの作成
                _apiClient = new DifyApiClient();

                // Difyサービスの作成
                _difyService = new DifyService(_apiClient, _config);

                // アダプターの作成
                _adapter = new DifyQueueManagerAdapter(_difyService, _apiClient, _config);

                if (_enableDebugLogging)
                {
                    Debug.Log($"[DifyQueueManager] Initialized successfully: {_adapter.GetConfigurationSummary()}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyQueueManager] Initialization failed: {ex}");
                enabled = false;
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// 質問をキューに追加
        /// 既存のQueueManager.AddTextToQueueと互換性のあるインターフェース
        /// </summary>
        /// <param name="question">追加する質問</param>
        public void AddQuestionToQueue(Question question)
        {
            if (question == null)
            {
                Debug.LogWarning("[DifyQueueManager] Attempted to add null question to queue");
                return;
            }

            if (string.IsNullOrWhiteSpace(question.question))
            {
                Debug.LogWarning("[DifyQueueManager] Attempted to add empty question to queue");
                return;
            }

            lock (_queueLock)
            {
                _inputQueue.Enqueue(question);
                
                if (_enableDebugLogging)
                {
                }
            }

            // UI更新
            UpdateQueueIndicator(question);
        }

        /// <summary>
        /// 処理の開始
        /// </summary>
        public void StartProcessing()
        {
            if (_isProcessing)
            {
                Debug.LogWarning("[DifyQueueManager] Processing is already running");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _isProcessing = true;

            // コルーチン処理開始
            StartCoroutine(ProcessQueueCoroutine());

            if (_enableDebugLogging)
            {
                Debug.Log("[DifyQueueManager] Started processing queue");
            }
        }

        /// <summary>
        /// 処理の停止
        /// </summary>
        public void StopProcessing()
        {
            if (!_isProcessing)
            {
                return;
            }

            _isProcessing = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            if (_enableDebugLogging)
            {
                Debug.Log("[DifyQueueManager] Stopped processing queue");
            }
        }

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <returns>接続成功の場合true</returns>
        public bool TestConnection()
        {
            if (_adapter == null)
            {
                Debug.LogError("[DifyQueueManager] Adapter not initialized");
                return false;
            }

            try
            {
                var result = _adapter.TestConnection();
                Debug.Log($"[DifyQueueManager] Connection test result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyQueueManager] Connection test failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 統計情報の取得
        /// </summary>
        /// <returns>処理統計</returns>
        public string GetProcessingStatistics()
        {
            lock (_queueLock)
            {
                return $"Queue: {_inputQueue.Count}, " +
                       $"Processing: {_processingQueue.Count}, " +
                       $"Total: {_totalProcessedQuestions}, " +
                       $"Success: {_successfulResponses}, " +
                       $"Failed: {_failedResponses}";
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// キュー処理のメインループ（コルーチン版）
        /// </summary>
        private IEnumerator ProcessQueueCoroutine()
        {
            while (_isProcessing)
            {
                bool hasError = false;
                
                try
                {
                    ProcessNextQuestion();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DifyQueueManager] Error in processing loop: {ex}");
                    hasError = true;
                }
                
                if (hasError)
                {
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    yield return new WaitForSeconds(_processingIntervalSeconds);
                }
            }
        }

        /// <summary>
        /// 次の質問を処理（同期版）
        /// </summary>
        private void ProcessNextQuestion()
        {
            Question nextQuestion = null;

            // キューから質問を取得
            lock (_queueLock)
            {
                if (_inputQueue.Count > 0 && 
                    _processingQueue.Count < _maxConcurrentRequests &&
                    _textToSpeech.HasCapacityToEnqueue())
                {
                    nextQuestion = _inputQueue.Dequeue();
                    _processingQueue.Enqueue(nextQuestion);
                }
            }

            if (nextQuestion == null)
            {
                return;
            }

            try
            {
                // Difyアダプターで質問を処理（同期的に実行）
                var result = _adapter.ProcessQuestionAsync(nextQuestion).GetAwaiter().GetResult();

                // 統計情報更新
                UpdateStatistics(result);

                // デバッグログ出力
                if (_enableDebugLogging)
                {
                    _adapter.LogProcessingDetails(result, nextQuestion);
                }

                // 音声生成システムに送信
                SendToTextToSpeech(nextQuestion, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyQueueManager] Failed to process question '{nextQuestion.question}': {ex}");
                _failedResponses++;
            }
            finally
            {
                // 処理キューから削除
                lock (_queueLock)
                {
                    var tempQueue = new Queue<Question>();
                    while (_processingQueue.Count > 0)
                    {
                        var q = _processingQueue.Dequeue();
                        if (q.id != nextQuestion.id)
                        {
                            tempQueue.Enqueue(q);
                        }
                    }
                    _processingQueue = tempQueue;
                }

                _totalProcessedQuestions++;
            }
        }

        /// <summary>
        /// TextToSpeechシステムに結果を送信
        /// </summary>
        /// <param name="question">元の質問</param>
        /// <param name="result">Dify処理結果</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private void SendToTextToSpeech(Question question, DifyServiceResult result)
        {
            try
            {
                // ConversationオブジェクトをDifyアダプターで作成
                var conversation = _adapter.CreateConversationFromResult(question, result);

                // TextToSpeechキューに追加
                _textToSpeech.EnqueueConversation(conversation);

                if (_enableDebugLogging)
                {
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyQueueManager] Failed to send to TextToSpeech: {ex}");
            }
        }

        /// <summary>
        /// 統計情報の更新
        /// </summary>
        /// <param name="result">Dify処理結果</param>
        private void UpdateStatistics(DifyServiceResult result)
        {
            if (result.IsSuccess)
            {
                _successfulResponses++;
            }
            else
            {
                _failedResponses++;
            }
        }

        /// <summary>
        /// キューインジケーターの更新
        /// </summary>
        /// <param name="question">追加された質問</param>
        private void UpdateQueueIndicator(Question question)
        {
            try
            {
                // QueueIndicatorの更新
                _queueIndicator?.AppendInIndicator(question);

                // NewCommentIndicatorQueueの更新
                _newCommentIndicatorQueue?.questions.Enqueue(question);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyQueueManager] Failed to update queue indicator: {ex}");
            }
        }

        /// <summary>
        /// 設定サマリーの取得
        /// </summary>
        /// <returns>設定情報の文字列</returns>
        private string GetConfigurationSummary()
        {
            if (_config == null)
            {
                return "Configuration not initialized";
            }

            return $"API: {_config.ApiUrl}, " +
                   $"Audio: {_config.EnableAudioProcessing}";
        }

        #endregion

        #region Unity Inspector Methods

        /// <summary>
        /// Inspectorからの接続テスト実行
        /// </summary>
        [ContextMenu("Test Connection")]
        private void TestConnectionFromInspector()
        {
            if (_adapter == null)
            {
                Debug.LogError("[DifyQueueManager] Service not initialized");
                return;
            }

            var result = TestConnection();
            Debug.Log($"[DifyQueueManager] Inspector test result: {result}");
        }

        /// <summary>
        /// Inspectorからの統計情報表示
        /// </summary>
        [ContextMenu("Show Statistics")]
        private void ShowStatisticsFromInspector()
        {
            Debug.Log($"[DifyQueueManager] Statistics: {GetProcessingStatistics()}");
        }

        /// <summary>
        /// Inspectorからの設定検証
        /// </summary>
        [ContextMenu("Validate Configuration")]
        private void ValidateConfigurationFromInspector()
        {
            if (_adapter == null)
            {
                Debug.LogError("[DifyQueueManager] Service not initialized");
                return;
            }

            var isValid = _adapter.ValidateConfiguration();
            Debug.Log($"[DifyQueueManager] Configuration valid: {isValid}");
        }

        #endregion
    }
}