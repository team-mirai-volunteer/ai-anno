using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace AiTuber.Services.Dify.Application.UseCases
{
    /// <summary>
    /// クエリレスポンス結果
    /// Application層のデータ転送オブジェクト
    /// Legacy DifyProcessingResultからリファクタリング済み
    /// </summary>
    public class QueryResponse
    {
        /// <summary>
        /// 処理成功フラグ
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// テキストレスポンス
        /// </summary>
        public string TextResponse { get; private set; }

        /// <summary>
        /// 会話ID
        /// </summary>
        public string ConversationId { get; private set; }

        /// <summary>
        /// メッセージID
        /// </summary>
        public string MessageId { get; private set; }

        /// <summary>
        /// 処理時間（ミリ秒）
        /// </summary>
        public long ProcessingTimeMs { get; private set; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// 音声データ（複数チャンク対応）
        /// </summary>
        public IReadOnlyList<byte[]> AudioChunks { get; private set; }

        /// <summary>
        /// 音声データを持っているかどうか
        /// </summary>
        public bool HasAudioData => AudioChunks.Count > 0;

        /// <summary>
        /// QueryResponseを作成
        /// </summary>
        /// <param name="isSuccess">処理成功フラグ</param>
        /// <param name="textResponse">テキストレスポンス</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <param name="processingTimeMs">処理時間</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <param name="audioChunks">音声データ</param>
        public QueryResponse(
            bool isSuccess,
            string textResponse,
            string conversationId,
            string messageId,
            long processingTimeMs,
            string? errorMessage = null,
            IReadOnlyList<byte[]>? audioChunks = null)
        {
            IsSuccess = isSuccess;
            TextResponse = textResponse ?? "";
            ConversationId = conversationId ?? "";
            MessageId = messageId ?? "";
            ProcessingTimeMs = processingTimeMs;
            ErrorMessage = errorMessage;
            AudioChunks = audioChunks ?? Array.Empty<byte[]>();
        }

        /// <summary>
        /// 成功レスポンスを作成
        /// </summary>
        /// <param name="textResponse">テキストレスポンス</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <param name="processingTimeMs">処理時間</param>
        /// <param name="audioChunks">音声データ</param>
        /// <returns>成功レスポンス</returns>
        public static QueryResponse CreateSuccess(
            string textResponse,
            string conversationId,
            string messageId,
            long processingTimeMs = 0,
            IReadOnlyList<byte[]>? audioChunks = null)
        {
            return new QueryResponse(
                isSuccess: true,
                textResponse: textResponse,
                conversationId: conversationId,
                messageId: messageId,
                processingTimeMs: processingTimeMs,
                audioChunks: audioChunks
            );
        }

        /// <summary>
        /// エラーレスポンスを作成
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <param name="processingTimeMs">処理時間</param>
        /// <returns>エラーレスポンス</returns>
        public static QueryResponse CreateError(
            string errorMessage,
            long processingTimeMs = 0)
        {
            return new QueryResponse(
                isSuccess: false,
                textResponse: "",
                conversationId: "",
                messageId: "",
                processingTimeMs: processingTimeMs,
                errorMessage: errorMessage
            );
        }
    }
}