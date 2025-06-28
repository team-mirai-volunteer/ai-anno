using System;
using AiTuber.Services.Dify.Domain.Entities;
using Newtonsoft.Json;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Services
{
    /// <summary>
    /// DifyStreamEvent作成専用Factory
    /// Domain Service、Pure C#実装
    /// </summary>
    public static class DifyStreamEventFactory
    {
        /// <summary>
        /// メッセージイベントを作成
        /// </summary>
        /// <param name="answer">テキストメッセージ</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <returns>メッセージイベント</returns>
        /// <exception cref="ArgumentException">必須パラメータが無効な場合</exception>
        public static DifyStreamEvent CreateMessageEvent(string answer, string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(answer))
                throw new ArgumentException("Answer cannot be null or empty", nameof(answer));
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("MessageId cannot be null or empty", nameof(messageId));

            var core = new DifyStreamEventCore(
                eventType: "message",
                conversationId: conversationId,
                messageId: messageId,
                answer: answer);

            return new DifyStreamEvent(core);
        }

        /// <summary>
        /// 音声イベントを作成
        /// </summary>
        /// <param name="audioData">Base64音声データ</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <returns>音声イベント</returns>
        /// <exception cref="ArgumentException">必須パラメータが無効な場合</exception>
        public static DifyStreamEvent CreateAudioEvent(string audioData, string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(audioData))
                throw new ArgumentException("AudioData cannot be null or empty", nameof(audioData));
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

            var core = new DifyStreamEventCore(
                eventType: "tts_message",
                conversationId: conversationId,
                messageId: messageId,
                audio: audioData);

            return new DifyStreamEvent(core);
        }

        /// <summary>
        /// 音声イベントを作成（MessageID省略版）
        /// </summary>
        /// <param name="audioData">Base64音声データ</param>
        /// <param name="conversationId">会話ID</param>
        /// <returns>音声イベント</returns>
        /// <exception cref="ArgumentException">必須パラメータが無効な場合</exception>
        public static DifyStreamEvent CreateAudioEvent(string audioData, string conversationId)
        {
            return CreateAudioEvent(audioData, conversationId, null);
        }

        /// <summary>
        /// 終了イベントを作成
        /// </summary>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <returns>終了イベント</returns>
        /// <exception cref="ArgumentException">必須パラメータが無効な場合</exception>
        public static DifyStreamEvent CreateEndEvent(string conversationId, string? messageId = null)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

            var core = new DifyStreamEventCore(
                eventType: "message_end",
                conversationId: conversationId,
                messageId: messageId);

            return new DifyStreamEvent(core);
        }

        /// <summary>
        /// カスタムイベントを作成
        /// </summary>
        /// <param name="eventType">イベント種別</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <param name="taskId">タスクID</param>
        /// <param name="workflowRunId">ワークフロー実行ID</param>
        /// <returns>カスタムイベント</returns>
        /// <exception cref="ArgumentException">必須パラメータが無効な場合</exception>
        public static DifyStreamEvent CreateCustomEvent(
            string eventType,
            string conversationId,
            string? messageId = null,
            string? taskId = null,
            string? workflowRunId = null)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("EventType cannot be null or empty", nameof(eventType));
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

            var core = new DifyStreamEventCore(
                eventType: eventType,
                conversationId: conversationId,
                messageId: messageId,
                taskId: taskId,
                workflowRunId: workflowRunId);

            return new DifyStreamEvent(core);
        }

        /// <summary>
        /// JSON文字列からDifyStreamEventを作成
        /// </summary>
        /// <param name="jsonData">JSONデータ</param>
        /// <returns>パース結果のDifyStreamEvent、失敗時はnull</returns>
        public static DifyStreamEvent? ParseFromJson(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
                return null;

            try
            {
                var eventData = JsonConvert.DeserializeObject<DifyStreamEventDto>(jsonData);
                if (eventData == null || string.IsNullOrEmpty(eventData.Event))
                    return null;

                var core = new DifyStreamEventCore(
                    eventType: eventData.Event,
                    conversationId: eventData.ConversationId ?? "",
                    messageId: eventData.MessageId,
                    answer: eventData.Answer,
                    audio: eventData.Audio,
                    createdAt: eventData.CreatedAt ?? 0,
                    taskId: eventData.TaskId,
                    workflowRunId: eventData.WorkflowRunId);

                return new DifyStreamEvent(core);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// JSON デシリアライゼーション用DTO
    /// </summary>
    internal class DifyStreamEventDto
    {
        [JsonProperty("event")]
        public string Event { get; set; } = "";

        [JsonProperty("conversation_id")]
        public string? ConversationId { get; set; }

        [JsonProperty("message_id")]
        public string? MessageId { get; set; }

        [JsonProperty("answer")]
        public string? Answer { get; set; }

        [JsonProperty("audio")]
        public string? Audio { get; set; }

        [JsonProperty("created_at")]
        public long? CreatedAt { get; set; }

        [JsonProperty("task_id")]
        public string? TaskId { get; set; }

        [JsonProperty("workflow_run_id")]
        public string? WorkflowRunId { get; set; }
    }
}