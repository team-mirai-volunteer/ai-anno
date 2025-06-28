using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Domain.Entities;
using AiTuber.Services.Dify.Infrastructure.Http;
using UnityEngine;

#nullable enable

namespace AiTuber.Services.Dify.Presentation.Controllers
{
    /// <summary>
    /// Dify サービスコントローラー
    /// Presentation層 Clean Architecture準拠
    /// DifyEditorWindow（テスト用）やその他UIから利用
    /// </summary>
    public class DifyController
    {
        private readonly IProcessQueryUseCase _processQueryUseCase;

        /// <summary>
        /// DifyController を作成
        /// </summary>
        /// <param name="processQueryUseCase">クエリ処理ユースケース</param>
        /// <exception cref="ArgumentNullException">必須パラメータがnullの場合</exception>
        public DifyController(IProcessQueryUseCase processQueryUseCase)
        {
            _processQueryUseCase = processQueryUseCase ?? throw new ArgumentNullException(nameof(processQueryUseCase));
        }

        /// <summary>
        /// クエリを送信（非ストリーミング）
        /// </summary>
        /// <param name="query">送信するクエリ</param>
        /// <param name="user">ユーザー識別子</param>
        /// <param name="conversationId">会話ID（継続会話の場合）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>クエリレスポンス</returns>
        public async Task<DifyResponse> SendQueryAsync(
            string query,
            string? user = null,
            string? conversationId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 入力検証
                if (string.IsNullOrWhiteSpace(query))
                {
                    return DifyResponse.CreateError("Query cannot be empty");
                }

                // リクエスト作成
                var request = new DifyRequest(
                    query,
                    user ?? "default-user",
                    conversationId ?? "");

                // ユースケース実行
                var response = await _processQueryUseCase.ExecuteAsync(
                    request,
                    null,
                    cancellationToken);

                // レスポンス変換
                return DifyResponse.CreateSuccess(
                    response.TextResponse,
                    response.ConversationId,
                    response.MessageId,
                    (int)response.ProcessingTimeMs);
            }
            catch (OperationCanceledException)
            {
                return DifyResponse.CreateError("Operation was cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyController] SendQuery failed: {ex.Message}");
                return DifyResponse.CreateError($"Query failed: {ex.Message}");
            }
        }

        /// <summary>
        /// クエリを送信（ストリーミング）
        /// </summary>
        /// <param name="query">送信するクエリ</param>
        /// <param name="user">ユーザー識別子</param>
        /// <param name="onEventReceived">イベント受信コールバック</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <param name="conversationId">会話ID（継続会話の場合）</param>
        /// <returns>最終レスポンス</returns>
        public async Task<DifyResponse> SendQueryStreamingAsync(
            string query,
            string? user = null,
            Action<DifyStreamEvent>? onEventReceived = null,
            CancellationToken cancellationToken = default,
            string? conversationId = null)
        {
            try
            {
                // 入力検証
                if (string.IsNullOrWhiteSpace(query))
                {
                    return DifyResponse.CreateError("Query cannot be empty");
                }

                // リクエスト作成
                var request = new DifyRequest(
                    query,
                    user ?? "default-user",
                    conversationId ?? "");

                // ユースケース実行
                var response = await _processQueryUseCase.ExecuteAsync(
                    request,
                    onEventReceived,
                    cancellationToken);

                // レスポンス変換
                return DifyResponse.CreateSuccess(
                    response.TextResponse,
                    response.ConversationId,
                    response.MessageId,
                    (int)response.ProcessingTimeMs);
            }
            catch (OperationCanceledException)
            {
                return DifyResponse.CreateError("Operation was cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyController] SendQueryStreaming failed: {ex.Message}");
                return DifyResponse.CreateError($"Streaming query failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定情報を取得
        /// </summary>
        /// <returns>Dify設定</returns>
        public DifyConfiguration GetConfiguration()
        {
            return _processQueryUseCase.GetConfiguration();
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
                return await _processQueryUseCase.TestConnectionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifyController] Connection test failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Dify レスポンスDTO（Presentation層用）
    /// </summary>
    public class DifyResponse
    {
        /// <summary>
        /// 成功フラグ
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// テキストレスポンス
        /// </summary>
        public string TextResponse { get; }

        /// <summary>
        /// 会話ID
        /// </summary>
        public string ConversationId { get; }

        /// <summary>
        /// メッセージID
        /// </summary>
        public string MessageId { get; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// 処理時間（ミリ秒）
        /// </summary>
        public int ProcessingTimeMs { get; }

        private DifyResponse(
            bool isSuccess,
            string textResponse,
            string conversationId,
            string messageId,
            string? errorMessage,
            int processingTimeMs)
        {
            IsSuccess = isSuccess;
            TextResponse = textResponse;
            ConversationId = conversationId;
            MessageId = messageId;
            ErrorMessage = errorMessage;
            ProcessingTimeMs = processingTimeMs;
        }

        /// <summary>
        /// 成功レスポンスを作成
        /// </summary>
        public static DifyResponse CreateSuccess(
            string textResponse,
            string conversationId,
            string messageId,
            int processingTimeMs)
        {
            return new DifyResponse(
                true,
                textResponse,
                conversationId,
                messageId,
                null,
                processingTimeMs);
        }

        /// <summary>
        /// エラーレスポンスを作成
        /// </summary>
        public static DifyResponse CreateError(string errorMessage)
        {
            return new DifyResponse(
                false,
                "",
                "",
                "",
                errorMessage,
                0);
        }
    }
}