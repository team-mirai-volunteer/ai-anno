using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Legacy.Dify.Data;
using UnityEngine;

namespace AiTuber.Services.Legacy.Dify
{

    /// <summary>
    /// Dify統合サービス結果
    /// </summary>
    public class DifyServiceResult
    {
        /// <summary>
        /// 処理成功フラグ
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// テキストレスポンス
        /// </summary>
        public string TextResponse { get; set; }

        /// <summary>
        /// 音声データ（Base64エンコード済み）
        /// </summary>
        public string AudioBase64 { get; set; }

        /// <summary>
        /// 音声バイナリデータ
        /// </summary>
        public byte[] AudioData { get; set; }

        /// <summary>
        /// 会話ID
        /// </summary>
        public string ConversationId { get; set; }

        /// <summary>
        /// メッセージID
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// 処理時間（ミリ秒）
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// 受信イベント数
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 有効な音声データを持つかどうか
        /// </summary>
        public bool HasAudioData => AudioData != null && AudioData.Length > 0;

        /// <summary>
        /// 有効なテキストレスポンスを持つかどうか
        /// </summary>
        public bool HasTextResponse => !string.IsNullOrWhiteSpace(TextResponse);
    }

    /// <summary>
    /// Dify統合サービス設定
    /// </summary>
    public class DifyServiceConfig
    {
        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; }
        
        /// <summary>
        /// API URL
        /// </summary>
        public string ApiUrl { get; set; }
        
        /// <summary>
        /// 音声処理を有効にするかどうか
        /// </summary>
        public bool EnableAudioProcessing { get; set; } = true;
        
        /// <summary>
        /// 設定が有効かどうか
        /// </summary>
        public bool IsValid => 
            !string.IsNullOrWhiteSpace(ApiKey) && 
            ApiKey.Length >= 8 && 
            IsValidUrl(ApiUrl);

        /// <summary>
        /// URL形式の検証
        /// </summary>
        /// <param name="url">検証対象URL</param>
        /// <returns>有効なHTTP/HTTPS URLの場合true</returns>
        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }

    /// <summary>
    /// Dify統合サービス - Pure C#オーケストレーター
    /// IDifyApiClient、SSEParserを統合し、高レベルなビジネスロジックを提供する
    /// Unity非依存でユニットテスト可能
    /// 音声処理はTTSAudioProcessorに委譲
    /// </summary>
    public class DifyService
    {
        private readonly IDifyApiClient _apiClient;
        private readonly DifyServiceConfig _config;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="apiClient">Dify API クライアント</param>
        /// <param name="config">サービス設定</param>
        /// <exception cref="ArgumentNullException">apiClient または config が null の場合</exception>
        /// <exception cref="ArgumentException">config が無効な場合</exception>
        public DifyService(IDifyApiClient apiClient, DifyServiceConfig config)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            if (!_config.IsValid)
                throw new ArgumentException("Invalid service configuration", nameof(config));
                
            // API クライアントの設定を同期
            _apiClient.ApiKey = _config.ApiKey;
            _apiClient.ApiUrl = _config.ApiUrl;
        }

        /// <summary>
        /// ユーザーの質問に対してDifyから回答を取得
        /// ストリーミングモードで処理
        /// </summary>
        /// <param name="userQuery">ユーザーの質問</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="conversationId">会話ID（継続会話の場合）</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>Dify統合サービス結果</returns>
        /// <exception cref="ArgumentException">userQuery が空または無効な場合</exception>
        /// <exception cref="InvalidOperationException">サービス設定が無効な場合</exception>
        public async Task<DifyServiceResult> ProcessUserQueryAsync(
            string userQuery,
            string userId,
            string conversationId = null,
            CancellationToken cancellationToken = default)
        {
            return await ProcessUserQueryAsync(userQuery, userId, conversationId, null, cancellationToken);
        }

        /// <summary>
        /// ユーザーの質問に対してDifyから回答を取得（リアルタイムコールバック付き）
        /// ストリーミングモードで処理
        /// </summary>
        /// <param name="userQuery">ユーザーの質問</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="conversationId">会話ID（継続会話の場合）</param>
        /// <param name="onStreamEvent">ストリーミングイベント受信時のコールバック</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>Dify統合サービス結果</returns>
        /// <exception cref="ArgumentException">userQuery が空または無効な場合</exception>
        /// <exception cref="InvalidOperationException">サービス設定が無効な場合</exception>
        public async Task<DifyServiceResult> ProcessUserQueryAsync(
            string userQuery,
            string userId,
            string conversationId = null,
            Action<DifyStreamEvent> onStreamEvent = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
                throw new ArgumentException("User query cannot be empty", nameof(userQuery));
                
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be empty", nameof(userId));

            var startTime = DateTime.UtcNow;
            var result = new DifyServiceResult();

            try
            {
                // リクエストデータ作成
                var request = new DifyApiRequest
                {
                    query = userQuery,
                    user = userId,
                    conversation_id = conversationId,
                    response_mode = "streaming"
                };

                // API処理実行（ストリーミングモード固定）
                var apiResult = await ProcessStreamingRequestAsync(request, onStreamEvent, cancellationToken);

                // 結果の統合処理
                await ProcessApiResultAsync(apiResult, result, cancellationToken);

                result.IsSuccess = apiResult.IsSuccess;
                result.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Service processing failed";
                result.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                                
                // 構造化ログ出力（結果には含めない）
                Debug.LogError($"DifyService.ProcessUserQueryAsync failed: {ex}");
                
                return result;
            }
        }

        /// <summary>
        /// ストリーミングリクエストの処理
        /// SSEイベントを受信し、音声・テキストデータを統合
        /// </summary>
        /// <param name="request">APIリクエスト</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>処理結果</returns>
        private async Task<DifyProcessingResult> ProcessStreamingRequestAsync(
            DifyApiRequest request,
            Action<DifyStreamEvent> onStreamEvent,
            CancellationToken cancellationToken)
        {
            var receivedEvents = new List<DifyStreamEvent>();

            // ストリーミングリクエスト実行
            var result = await _apiClient.SendStreamingRequestAsync(
                request,
                eventData => {
                    ProcessStreamEvent(eventData, receivedEvents);
                    // 外部コールバックがあれば実行
                    onStreamEvent?.Invoke(eventData);
                },
                cancellationToken);
            return result;
        }

        /// <summary>
        /// SSEイベントの処理
        /// テキストメッセージのみを処理（音声処理はTTSAudioProcessorに委譲）
        /// </summary>
        /// <param name="eventData">SSEイベントデータ</param>
        /// <param name="receivedEvents">受信イベントリスト</param>
        private void ProcessStreamEvent(
            DifyStreamEvent eventData,
            List<DifyStreamEvent> receivedEvents)
        {
            if (eventData == null || !eventData.HasValidData)
                return;

            receivedEvents.Add(eventData);
        }

        /// <summary>
        /// API結果の後処理
        /// テキストレスポンスと基本情報の設定（音声処理はTTSAudioProcessorに委譲）
        /// </summary>
        /// <param name="apiResult">API処理結果</param>
        /// <param name="serviceResult">サービス結果</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        private async Task ProcessApiResultAsync(
            DifyProcessingResult apiResult,
            DifyServiceResult serviceResult,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // 基本情報の設定
                serviceResult.TextResponse = apiResult.TextResponse;
                serviceResult.ConversationId = apiResult.ConversationId;
                serviceResult.MessageId = apiResult.MessageId;
                serviceResult.EventCount = apiResult.TotalEventCount;
                serviceResult.ErrorMessage = apiResult.ErrorMessage;

                // 音声データ設定（型安全性確保のため）
                // EnableAudioProcessingフラグを確認
                if (_config.EnableAudioProcessing && apiResult.AudioChunks != null && apiResult.AudioChunks.Count > 0)
                {
                    // 複数チャンクを結合してAudioDataに設定
                    var totalBytes = apiResult.TotalAudioBytes;
                    var combinedAudio = new byte[totalBytes];
                    int offset = 0;
                    foreach (var chunk in apiResult.AudioChunks)
                    {
                        if (chunk != null)
                        {
                            Array.Copy(chunk, 0, combinedAudio, offset, chunk.Length);
                            offset += chunk.Length;
                        }
                    }
                    serviceResult.AudioData = combinedAudio;
                    serviceResult.AudioBase64 = Convert.ToBase64String(combinedAudio);
                }
                // 音声データはリアルタイムでTTSAudioProcessorが処理し、AudioBufferに蓄積される
            }, cancellationToken);
        }

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>接続成功の場合true</returns>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _apiClient.TestConnectionAsync(cancellationToken);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// サービス設定の検証
        /// </summary>
        /// <returns>設定が有効な場合true</returns>
        public bool ValidateConfiguration()
        {
            return _config.IsValid && _apiClient.IsConfigurationValid();
        }

        /// <summary>
        /// 現在の設定情報を取得
        /// </summary>
        /// <returns>サービス設定のコピー</returns>
        public DifyServiceConfig GetConfiguration()
        {
            return new DifyServiceConfig
            {
                ApiKey = _config.ApiKey,
                ApiUrl = _config.ApiUrl,
                EnableAudioProcessing = _config.EnableAudioProcessing
            };
        }
    }
}