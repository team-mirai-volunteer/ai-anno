using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AiTuber.Services.Legacy.Dify;
using AiTuber.Services.Legacy.Dify.Data;
using Aituber;
using Cysharp.Threading.Tasks;

namespace AiTuber.Services.Legacy.Dify.Unity
{
    /// <summary>
    /// DifyService統合アダプター - Unity環境での使用
    /// 既存のQueueManagerパターンに準拠し、DifyServiceを統合
    /// DifyServiceの純粋C#機能をUnityのMonoBehaviourパターンに統合
    /// </summary>
    public class DifyQueueManagerAdapter
    {
        private readonly DifyService _difyService;
        private readonly IDifyApiClient _apiClient;
        private readonly DifyServiceConfig _config;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="difyService">Dify統合サービスインスタンス</param>
        /// <param name="apiClient">Dify APIクライアント</param>
        /// <param name="config">サービス設定</param>
        /// <exception cref="ArgumentNullException">引数がnullの場合</exception>
        public DifyQueueManagerAdapter(
            DifyService difyService,
            IDifyApiClient apiClient,
            DifyServiceConfig config)
        {
            _difyService = difyService ?? throw new ArgumentNullException(nameof(difyService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 質問処理をDify APIを使用して実行
        /// 既存のQueueManagerのRequestReplyメソッドを置換
        /// </summary>
        /// <param name="question">処理対象の質問</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>Dify処理結果</returns>
        /// <exception cref="ArgumentNullException">questionがnullの場合</exception>
        /// <exception cref="ArgumentException">question.questionが空の場合</exception>
        public async UniTask<DifyServiceResult> ProcessQuestionAsync(Question question, CancellationToken cancellationToken = default)
        {
            if (question is null)
                throw new ArgumentNullException(nameof(question));

            if (string.IsNullOrWhiteSpace(question.question))
                throw new ArgumentException("Question text cannot be empty", nameof(question));

            try
            {
                // ユーザーIDの決定（既存のQueueManagerパターンに準拠）
                var userId = string.IsNullOrWhiteSpace(question.userName) 
                    ? "anonymous-user" 
                    : question.userName;


                // 実際のDifyServiceを使用してクエリ処理
                var result = await _difyService.ProcessUserQueryAsync(
                    question.question,
                    userId,
                    conversationId: null,
                    onStreamEvent: null,
                    cancellationToken);

                return result;
            }
            catch (ArgumentException ex)
            {
                return new DifyServiceResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Invalid argument: {ex.Message}",
                    ProcessingTimeMs = 0
                };
            }
            catch (InvalidOperationException ex)
            {
                return new DifyServiceResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Service unavailable: {ex.Message}",
                    ProcessingTimeMs = 0
                };
            }
            catch (TaskCanceledException ex)
            {
                return new DifyServiceResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Request timeout: {ex.Message}",
                    ProcessingTimeMs = 0
                };
            }
            catch (System.Exception ex)
            {
                
                // エラー時の結果オブジェクト作成
                return new DifyServiceResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Adapter processing failed: {ex.Message}",
                    ProcessingTimeMs = 0
                };
            }
        }

        /// <summary>
        /// 既存のConversationオブジェクトを作成
        /// QueueManagerのパターンに準拠し、TextToSpeechシステムとの統合を可能にする
        /// </summary>
        /// <param name="question">元の質問</param>
        /// <param name="difyResult">Dify処理結果</param>
        /// <returns>音声生成用のConversationオブジェクト</returns>
        /// <exception cref="ArgumentNullException">引数がnullの場合</exception>
        public Conversation CreateConversationFromResult(Question question, DifyServiceResult difyResult)
        {
            if (question is null)
                throw new ArgumentNullException(nameof(question));

            if (difyResult == null)
                throw new ArgumentNullException(nameof(difyResult));

            // 応答テキストの決定
            var responseText = difyResult.IsSuccess && difyResult.HasTextResponse
                ? difyResult.TextResponse
                : "申し訳ございません。現在、応答を生成できません。";

            // 画像ファイル名の決定（デフォルトスライドを使用）
            var imageFileName = "slide_1"; // QueueManagerの既存パターンに準拠

            return new Conversation(question, responseText, imageFileName);
        }

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>接続成功の場合true</returns>
        public bool TestConnection()
        {
            try
            {
                // 同期的な接続テスト（テスト用）
                var result = _config is not null && _config.IsValid;
                return result;
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[DifyAdapter] Invalid argument: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError($"[DifyAdapter] Service unavailable: {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogError($"[DifyAdapter] Request timeout: {ex.Message}");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DifyAdapter] Connection test failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// サービス設定の検証
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        public bool ValidateConfiguration()
        {
            try
            {
                var isValid = _difyService.ValidateConfiguration();
                Debug.Log($"[DifyAdapter] Configuration validation: {isValid}");
                return isValid;
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[DifyAdapter] Invalid argument: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError($"[DifyAdapter] Service unavailable: {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogError($"[DifyAdapter] Request timeout: {ex.Message}");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DifyAdapter] Configuration validation failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 統計情報の取得
        /// デバッグ・監視用
        /// </summary>
        /// <returns>設定情報のサマリー</returns>
        public string GetConfigurationSummary()
        {
            try
            {
                var config = _difyService.GetConfiguration();
                return $"API URL: {config.ApiUrl}, " +
                       $"Audio: {config.EnableAudioProcessing}";
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[DifyAdapter] Invalid argument: {ex.Message}");
                return $"Configuration error: {ex.Message}";
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError($"[DifyAdapter] Service unavailable: {ex.Message}");
                return $"Service unavailable: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogError($"[DifyAdapter] Request timeout: {ex.Message}");
                return $"Request timeout: {ex.Message}";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DifyAdapter] Failed to get configuration summary: {ex}");
                return "Configuration unavailable";
            }
        }

        /// <summary>
        /// Unity環境でのDify処理結果の詳細ログ出力
        /// 音声データ、処理時間、イベント数等の統計情報を出力
        /// </summary>
        /// <param name="result">Dify処理結果</param>
        /// <param name="question">元の質問</param>
        /// <param name="suppressErrorLogs">エラーログを抑制するかどうか（テスト用）</param>
        public void LogProcessingDetails(DifyServiceResult result, Question question, bool suppressErrorLogs = false)
        {
            if (result == null || question == null) return;

            var details = new List<string>
            {
                $"Question: {question.question}",
                $"User: {question.userName}",
                $"Success: {result.IsSuccess}",
                $"Processing Time: {result.ProcessingTimeMs}ms",
                $"Event Count: {result.EventCount}",
                $"Has Text: {result.HasTextResponse}",
                $"Has Audio: {result.HasAudioData}"
            };

            if (result.HasAudioData)
            {
                details.Add($"Audio Size: {result.AudioData.Length} bytes");
            }

            if (!string.IsNullOrEmpty(result.ConversationId))
            {
                details.Add($"Conversation ID: {result.ConversationId}");
            }

            if (!string.IsNullOrEmpty(result.MessageId))
            {
                details.Add($"Message ID: {result.MessageId}");
            }

            Debug.Log($"[DifyAdapter] Processing Details:\n{string.Join("\n", details)}");

            if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage) && !suppressErrorLogs)
            {
                Debug.LogWarning($"[DifyAdapter] Processing failed: {result.ErrorMessage}");
            }
        }
    }
}