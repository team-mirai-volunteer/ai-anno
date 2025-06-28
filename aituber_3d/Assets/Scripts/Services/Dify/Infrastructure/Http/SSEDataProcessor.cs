using System;
using System.Collections.Generic;
using System.Text;
using AiTuber.Services.Dify.Domain.Entities;
using Newtonsoft.Json;
using UnityEngine;

#nullable enable

namespace AiTuber.Services.Dify.Infrastructure.Http
{
    /// <summary>
    /// Server-Sent Events データ処理専用クラス
    /// Infrastructure Layer、Pure C#実装
    /// </summary>
    internal static class SSEDataProcessor
    {
        /// <summary>
        /// SSEストリーミングデータを処理
        /// </summary>
        /// <param name="sseData">SSEデータ</param>
        /// <param name="textResponse">テキストレスポンス蓄積用</param>
        /// <param name="audioChunks">音声チャンク蓄積用</param>
        /// <param name="onEventReceived">イベント受信コールバック</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        public static void ProcessStreamingData(
            string sseData,
            StringBuilder textResponse,
            List<byte[]> audioChunks,
            Action<DifyStreamEvent>? onEventReceived,
            ref string conversationId,
            ref string messageId)
        {
            if (string.IsNullOrEmpty(sseData) || !sseData.StartsWith("data: "))
                return;

            try
            {
                var jsonData = ExtractJsonData(sseData);
                if (jsonData == null) return;

                var eventData = DeserializeEventData(jsonData);
                if (eventData == null) return;

                UpdateConversationIds(eventData, ref conversationId, ref messageId);

                var domainEvent = CreateDomainEvent(eventData, textResponse, audioChunks);
                
                InvokeEventCallback(domainEvent, onEventReceived);
            }
            catch (JsonException ex)
            {
                Debug.LogWarning($"Failed to parse SSE JSON data: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Skipping invalid streaming data: {ex.Message}");
            }
        }

        /// <summary>
        /// SSEデータからJSON文字列を抽出
        /// </summary>
        /// <param name="sseData">SSEデータ</param>
        /// <returns>JSONデータ、無効な場合はnull</returns>
        private static string? ExtractJsonData(string sseData)
        {
            var jsonData = sseData.Substring(6); // Remove "data: " prefix
            return jsonData.Trim() == "[DONE]" ? null : jsonData;
        }

        /// <summary>
        /// JSONデータをDifyStreamEventDtoにデシリアライズ
        /// </summary>
        /// <param name="jsonData">JSONデータ</param>
        /// <returns>デシリアライズ結果、失敗時はnull</returns>
        private static DifyStreamEventDto? DeserializeEventData(string jsonData)
        {
            return JsonConvert.DeserializeObject<DifyStreamEventDto>(jsonData);
        }

        /// <summary>
        /// 会話IDとメッセージIDを更新
        /// </summary>
        /// <param name="eventData">イベントデータ</param>
        /// <param name="conversationId">会話ID（参照渡し）</param>
        /// <param name="messageId">メッセージID（参照渡し）</param>
        private static void UpdateConversationIds(
            DifyStreamEventDto eventData,
            ref string conversationId,
            ref string messageId)
        {
            if (!string.IsNullOrEmpty(eventData.ConversationId))
                conversationId = eventData.ConversationId;
            if (!string.IsNullOrEmpty(eventData.MessageId))
                messageId = eventData.MessageId;
        }

        /// <summary>
        /// DTOからDomainイベントを作成
        /// </summary>
        /// <param name="eventData">イベントDTO</param>
        /// <param name="textResponse">テキストレスポンス蓄積用</param>
        /// <param name="audioChunks">音声チャンク蓄積用</param>
        /// <returns>作成されたDomainイベント、作成できない場合はnull</returns>
        private static DifyStreamEvent? CreateDomainEvent(
            DifyStreamEventDto eventData,
            StringBuilder textResponse,
            List<byte[]> audioChunks)
        {
            return eventData.Event switch
            {
                "message" when !string.IsNullOrEmpty(eventData.Answer) && !string.IsNullOrEmpty(eventData.ConversationId) =>
                    CreateMessageEvent(eventData, textResponse),

                "tts_message" when !string.IsNullOrEmpty(eventData.Audio) && !string.IsNullOrEmpty(eventData.ConversationId) =>
                    CreateAudioEvent(eventData, audioChunks),

                _ => CreateCustomEvent(eventData)
            };
        }

        /// <summary>
        /// メッセージイベントを作成
        /// </summary>
        private static DifyStreamEvent CreateMessageEvent(DifyStreamEventDto eventData, StringBuilder textResponse)
        {
            textResponse.Append(eventData.Answer);
            return DifyStreamEvent.CreateMessageEvent(
                eventData.Answer!,
                eventData.ConversationId!,
                eventData.MessageId ?? "");
        }

        /// <summary>
        /// 音声イベントを作成
        /// </summary>
        private static DifyStreamEvent? CreateAudioEvent(DifyStreamEventDto eventData, List<byte[]> audioChunks)
        {
            try
            {
                var audioBytes = Convert.FromBase64String(eventData.Audio!);
                audioChunks.Add(audioBytes);
                return DifyStreamEvent.CreateAudioEvent(eventData.Audio!, eventData.ConversationId!);
            }
            catch (FormatException ex)
            {
                Debug.LogWarning($"Invalid Base64 audio data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// カスタムイベントを作成
        /// </summary>
        private static DifyStreamEvent? CreateCustomEvent(DifyStreamEventDto eventData)
        {
            if (string.IsNullOrEmpty(eventData.Event) || string.IsNullOrEmpty(eventData.ConversationId))
                return null;

            return DifyStreamEvent.CreateCustomEvent(
                eventData.Event,
                eventData.ConversationId,
                eventData.MessageId,
                eventData.TaskId,
                eventData.WorkflowRunId);
        }

        /// <summary>
        /// イベントコールバックを呼び出し
        /// </summary>
        /// <param name="domainEvent">Domainイベント</param>
        /// <param name="onEventReceived">コールバック</param>
        private static void InvokeEventCallback(DifyStreamEvent? domainEvent, Action<DifyStreamEvent>? onEventReceived)
        {
            if (domainEvent != null && domainEvent.IsValid())
            {
                onEventReceived?.Invoke(domainEvent);
            }
        }
    }

    /// <summary>
    /// Dify SSE JSON デシリアライゼーション用DTO
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