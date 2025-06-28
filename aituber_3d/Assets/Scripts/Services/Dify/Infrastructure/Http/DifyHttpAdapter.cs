using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using AiTuber.Services.Dify.Domain.Entities;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Application.Ports;
using UnityEngine;
using Newtonsoft.Json;

#nullable enable

namespace AiTuber.Services.Dify.Infrastructure.Http
{
    /// <summary>
    /// Dify API HTTP通信アダプター
    /// Infrastructure層 Clean Architecture準拠
    /// Legacy DifyApiClientからリファクタリング済み
    /// Push型ストリーミング対応
    /// </summary>
    public class DifyHttpAdapter : IDifyStreamingPort
    {
        private readonly IHttpClient _httpClient;
        private readonly DifyConfiguration _configuration;

        /// <summary>
        /// DifyHttpAdapter を作成
        /// </summary>
        /// <param name="httpClient">HTTP通信クライアント</param>
        /// <param name="configuration">Dify設定</param>
        /// <exception cref="ArgumentNullException">必須パラメータがnullの場合</exception>
        /// <exception cref="ArgumentException">無効な設定の場合</exception>
        public DifyHttpAdapter(IHttpClient httpClient, DifyConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            if (!_configuration.IsValid())
                throw new ArgumentException("Invalid configuration provided", nameof(configuration));
        }

        /// <summary>
        /// ストリーミングクエリを実行
        /// </summary>
        /// <param name="request">Difyリクエスト</param>
        /// <param name="onEventReceived">イベント受信時のコールバック（Push型）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>クエリレスポンス</returns>
        /// <exception cref="ArgumentNullException">必須パラメータがnullの場合</exception>
        public async Task<QueryResponse> ExecuteStreamingAsync(
            DifyRequest request,
            Action<DifyStreamEvent>? onEventReceived = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Check cancellation immediately
            cancellationToken.ThrowIfCancellationRequested();

            var startTime = DateTimeOffset.UtcNow;

            try
            {
                var httpRequest = CreateHttpRequest(request);
                var textResponse = new StringBuilder();
                var audioChunks = new System.Collections.Generic.List<byte[]>();
                string conversationId = "";
                string messageId = "";

                var httpResponse = await _httpClient.SendStreamingRequestAsync(
                    httpRequest,
                    sseData => {
                        SSEDataProcessor.ProcessStreamingData(sseData, textResponse, audioChunks, onEventReceived, ref conversationId, ref messageId);
                    },
                    cancellationToken);

                var processingTime = (DateTimeOffset.UtcNow - startTime).Milliseconds;

                if (!httpResponse.IsSuccess)
                {
                    return QueryResponse.CreateError(httpResponse.ErrorMessage, processingTime);
                }

                return QueryResponse.CreateSuccess(
                    textResponse.ToString().Trim(),
                    conversationId,
                    messageId,
                    processingTime,
                    audioChunks.AsReadOnly());
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation exceptions
            }
            catch (Exception ex)
            {
                var processingTime = (DateTimeOffset.UtcNow - startTime).Milliseconds;
                return QueryResponse.CreateError($"Streaming execution failed: {ex.Message}", processingTime);
            }
        }

        /// <summary>
        /// 設定を取得
        /// </summary>
        /// <returns>Dify設定</returns>
        public DifyConfiguration GetConfiguration()
        {
            return _configuration;
        }

        /// <summary>
        /// 設定の妥当性を検証
        /// </summary>
        /// <returns>有効な設定の場合true</returns>
        public bool ValidateConfiguration()
        {
            return _configuration.IsValid();
        }

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>接続成功フラグ</returns>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _httpClient.TestConnectionAsync(_configuration.ApiUrl, cancellationToken);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HTTPリクエストを作成
        /// </summary>
        /// <param name="request">Difyリクエスト</param>
        /// <returns>HTTPリクエスト</returns>
        private HttpRequest CreateHttpRequest(DifyRequest request)
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                inputs = new { },
                query = request.Query,
                response_mode = "streaming",
                conversation_id = string.IsNullOrEmpty(request.ConversationId) ? "" : request.ConversationId,
                user = request.User,
                auto_generate_name = false
            });

            var headers = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Authorization", $"Bearer {_configuration.ApiKey}" },
                { "Content-Type", "application/json" },
                { "Accept", "text/event-stream" },
                { "Cache-Control", "no-cache" }
            };

            return new HttpRequest(_configuration.ApiUrl, "POST", requestBody, headers);
        }

    }
}