using System;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Entities
{
    /// <summary>
    /// Dify Server-Sent Events ストリームイベントコアエンティティ
    /// Pure C# Domain Entity、最小責任実装
    /// </summary>
    public class DifyStreamEventCore
    {
        /// <summary>
        /// イベント種別
        /// </summary>
        public string EventType { get; }

        /// <summary>
        /// 会話ID
        /// </summary>
        public string ConversationId { get; }

        /// <summary>
        /// メッセージID（オプション）
        /// </summary>
        public string? MessageId { get; }

        /// <summary>
        /// テキストメッセージ内容（部分的なストリーミングデータ）
        /// </summary>
        public string? Answer { get; }

        /// <summary>
        /// Base64エンコードされた音声データ
        /// </summary>
        public string? Audio { get; }

        /// <summary>
        /// イベント作成時刻（Unix timestamp）
        /// </summary>
        public long CreatedAt { get; }

        /// <summary>
        /// タスクID（オプション）
        /// </summary>
        public string? TaskId { get; }

        /// <summary>
        /// ワークフロー実行ID（オプション）
        /// </summary>
        public string? WorkflowRunId { get; }

        /// <summary>
        /// DifyStreamEventCoreを作成
        /// </summary>
        /// <param name="eventType">イベント種別</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <param name="answer">テキストメッセージ</param>
        /// <param name="audio">音声データ</param>
        /// <param name="createdAt">作成時刻</param>
        /// <param name="taskId">タスクID</param>
        /// <param name="workflowRunId">ワークフロー実行ID</param>
        public DifyStreamEventCore(
            string eventType,
            string conversationId,
            string? messageId = null,
            string? answer = null,
            string? audio = null,
            long createdAt = 0,
            string? taskId = null,
            string? workflowRunId = null)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
            MessageId = messageId;
            Answer = answer;
            Audio = audio;
            CreatedAt = createdAt == 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : createdAt;
            TaskId = taskId;
            WorkflowRunId = workflowRunId;
        }
    }
}