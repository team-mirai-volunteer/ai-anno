using System;
using System.Text;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Entities
{
    /// <summary>
    /// Dify Server-Sent Events ストリームイベントエンティティ
    /// Pure C# Domain Entity、Clean Architecture準拠
    /// Legacy DifyStreamEventからリファクタリング済み
    /// </summary>
    public class DifyStreamEvent
    {
        /// <summary>
        /// イベント種別
        /// </summary>
        public string EventType { get; private set; }

        /// <summary>
        /// 会話ID
        /// </summary>
        public string ConversationId { get; private set; }

        /// <summary>
        /// メッセージID（オプション）
        /// </summary>
        public string? MessageId { get; private set; }

        /// <summary>
        /// テキストメッセージ内容（部分的なストリーミングデータ）
        /// </summary>
        public string? Answer { get; private set; }

        /// <summary>
        /// Base64エンコードされた音声データ
        /// </summary>
        public string? Audio { get; private set; }

        /// <summary>
        /// イベント作成時刻（Unix timestamp）
        /// </summary>
        public long CreatedAt { get; private set; }

        /// <summary>
        /// タスクID（オプション）
        /// </summary>
        public string? TaskId { get; private set; }

        /// <summary>
        /// ワークフロー実行ID（オプション）
        /// </summary>
        public string? WorkflowRunId { get; private set; }

        /// <summary>
        /// プライベートコンストラクタ（Factory Methodパターン）
        /// </summary>
        private DifyStreamEvent(
            string eventType,
            string conversationId,
            string? messageId = null,
            string? answer = null,
            string? audio = null,
            long createdAt = 0,
            string? taskId = null,
            string? workflowRunId = null)
        {
            EventType = eventType;
            ConversationId = conversationId;
            MessageId = messageId;
            Answer = answer;
            Audio = audio;
            CreatedAt = createdAt == 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : createdAt;
            TaskId = taskId;
            WorkflowRunId = workflowRunId;
        }

        /// <summary>
        /// メッセージイベントを作成
        /// </summary>
        /// <param name="answer">メッセージ内容</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <returns>メッセージイベント</returns>
        /// <exception cref="ArgumentException">無効なパラメータが指定された場合</exception>
        public static DifyStreamEvent CreateMessageEvent(
            string answer, 
            string conversationId, 
            string messageId)
        {
            if (string.IsNullOrWhiteSpace(answer))
                throw new ArgumentException("Answer cannot be null or empty", nameof(answer));
            
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));
            
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("MessageId cannot be null or empty", nameof(messageId));

            return new DifyStreamEvent(
                eventType: "message",
                conversationId: conversationId,
                messageId: messageId,
                answer: answer.Trim()
            );
        }

        /// <summary>
        /// 音声イベントを作成
        /// </summary>
        /// <param name="audioData">Base64エンコードされた音声データ</param>
        /// <param name="conversationId">会話ID</param>
        /// <returns>音声イベント</returns>
        /// <exception cref="ArgumentException">無効なパラメータが指定された場合</exception>
        public static DifyStreamEvent CreateAudioEvent(string audioData, string conversationId)
        {
            if (string.IsNullOrWhiteSpace(audioData))
                throw new ArgumentException("AudioData cannot be null or empty", nameof(audioData));
            
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

            return new DifyStreamEvent(
                eventType: "tts_message",
                conversationId: conversationId,
                audio: audioData
            );
        }

        /// <summary>
        /// 終了イベントを作成
        /// </summary>
        /// <param name="conversationId">会話ID</param>
        /// <param name="messageId">メッセージID</param>
        /// <returns>終了イベント</returns>
        public static DifyStreamEvent CreateEndEvent(string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

            return new DifyStreamEvent(
                eventType: "message_end",
                conversationId: conversationId,
                messageId: messageId
            );
        }

        /// <summary>
        /// メッセージイベントかどうか
        /// </summary>
        public bool IsMessageEvent => EventType == "message";

        /// <summary>
        /// 音声イベントかどうか
        /// </summary>
        public bool IsAudioEvent => EventType == "tts_message";

        /// <summary>
        /// 終了イベントかどうか
        /// </summary>
        public bool IsEndEvent => EventType == "message_end";

        /// <summary>
        /// 有効な音声データを持っているかどうか
        /// </summary>
        public bool HasValidAudio => !string.IsNullOrWhiteSpace(Audio) && IsValidBase64(Audio);

        /// <summary>
        /// ストリームイベントの基本バリデーション
        /// </summary>
        /// <returns>必須フィールドが正しく設定されている場合true</returns>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(EventType) || string.IsNullOrWhiteSpace(ConversationId))
                return false;

            return EventType switch
            {
                "message" => !string.IsNullOrWhiteSpace(Answer) && !string.IsNullOrWhiteSpace(MessageId),
                "tts_message" => !string.IsNullOrWhiteSpace(Audio),
                "message_end" => !string.IsNullOrWhiteSpace(MessageId),
                _ => true // 他のイベントタイプも許可
            };
        }

        /// <summary>
        /// Base64文字列の妥当性チェック
        /// </summary>
        /// <param name="base64String">チェック対象の文字列</param>
        /// <returns>有効なBase64文字列の場合true</returns>
        private static bool IsValidBase64(string base64String)
        {
            if (string.IsNullOrWhiteSpace(base64String))
                return false;

            try
            {
                Convert.FromBase64String(base64String);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Legacy API互換形式に変換
        /// Infrastructure層での利用向け
        /// </summary>
        /// <returns>Legacy形式のデータ転送オブジェクト</returns>
        public DifyStreamEventDto ToDto()
        {
            return new DifyStreamEventDto
            {
                Event = EventType,
                ConversationId = ConversationId,
                MessageId = MessageId,
                Answer = Answer,
                Audio = Audio,
                CreatedAt = CreatedAt,
                TaskId = TaskId,
                WorkflowRunId = WorkflowRunId
            };
        }

        /// <summary>
        /// 等価性比較（record型の機能を模倣）
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is DifyStreamEvent other &&
                   EventType == other.EventType &&
                   ConversationId == other.ConversationId &&
                   MessageId == other.MessageId &&
                   Answer == other.Answer &&
                   Audio == other.Audio;
        }

        /// <summary>
        /// ハッシュコード計算（record型の機能を模倣）
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(EventType, ConversationId, MessageId, Answer, Audio);
        }
    }

    /// <summary>
    /// Legacy API互換用データ転送オブジェクト
    /// Infrastructure層でのシリアライゼーション用
    /// </summary>
    public class DifyStreamEventDto
    {
        public string Event { get; set; } = "";
        public string ConversationId { get; set; } = "";
        public string? MessageId { get; set; }
        public string? Answer { get; set; }
        public string? Audio { get; set; }
        public long CreatedAt { get; set; }
        public string? TaskId { get; set; }
        public string? WorkflowRunId { get; set; }
    }
}