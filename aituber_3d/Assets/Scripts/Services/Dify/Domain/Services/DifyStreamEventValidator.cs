using AiTuber.Services.Dify.Domain.Entities;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Services
{
    /// <summary>
    /// DifyStreamEvent検証専用サービス
    /// Domain Service、Pure C#実装
    /// </summary>
    public static class DifyStreamEventValidator
    {
        /// <summary>
        /// メッセージイベントかどうかを判定
        /// </summary>
        /// <param name="streamEvent">検証対象イベント</param>
        /// <returns>メッセージイベントの場合true</returns>
        public static bool IsMessageEvent(DifyStreamEvent streamEvent)
        {
            if (streamEvent?.Core == null) return false;
            
            return streamEvent.Core.EventType == "message" &&
                   !string.IsNullOrEmpty(streamEvent.Core.Answer) &&
                   !string.IsNullOrEmpty(streamEvent.Core.ConversationId);
        }

        /// <summary>
        /// 音声イベントかどうかを判定
        /// </summary>
        /// <param name="streamEvent">検証対象イベント</param>
        /// <returns>音声イベントの場合true</returns>
        public static bool IsAudioEvent(DifyStreamEvent streamEvent)
        {
            if (streamEvent?.Core == null) return false;
            
            return streamEvent.Core.EventType == "tts_message" &&
                   !string.IsNullOrEmpty(streamEvent.Core.Audio) &&
                   !string.IsNullOrEmpty(streamEvent.Core.ConversationId);
        }

        /// <summary>
        /// イベントの基本的な妥当性を検証
        /// </summary>
        /// <param name="streamEvent">検証対象イベント</param>
        /// <returns>有効な場合true</returns>
        public static bool IsValid(DifyStreamEvent streamEvent)
        {
            if (streamEvent?.Core == null) return false;
            
            // 必須フィールドチェック
            if (string.IsNullOrWhiteSpace(streamEvent.Core.EventType)) return false;
            if (string.IsNullOrWhiteSpace(streamEvent.Core.ConversationId)) return false;
            
            // CreatedAtの妥当性チェック
            if (streamEvent.Core.CreatedAt <= 0) return false;
            
            return true;
        }

        /// <summary>
        /// 終了イベントかどうかを判定
        /// </summary>
        /// <param name="streamEvent">検証対象イベント</param>
        /// <returns>終了イベントの場合true</returns>
        public static bool IsEndEvent(DifyStreamEvent streamEvent)
        {
            if (streamEvent?.Core == null) return false;
            
            return streamEvent.Core.EventType switch
            {
                "workflow_finished" => true,
                "message_end" => true,
                "error" => true,
                _ => false
            };
        }

        /// <summary>
        /// 有効な音声データを持っているかを判定
        /// </summary>
        /// <param name="streamEvent">検証対象イベント</param>
        /// <returns>有効な音声データがある場合true</returns>
        public static bool HasValidAudio(DifyStreamEvent streamEvent)
        {
            if (streamEvent?.Core == null) return false;
            if (string.IsNullOrWhiteSpace(streamEvent.Core.Audio)) return false;
            
            // Base64形式の妥当性チェック
            try
            {
                System.Convert.FromBase64String(streamEvent.Core.Audio);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// イベント種別の妥当性を検証
        /// </summary>
        /// <param name="eventType">検証対象イベント種別</param>
        /// <returns>有効な場合true</returns>
        public static bool IsValidEventType(string? eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType)) return false;
            
            return eventType switch
            {
                "message" => true,
                "tts_message" => true,
                "workflow_started" => true,
                "workflow_finished" => true,
                "node_started" => true,
                "node_finished" => true,
                "message_end" => true,
                "error" => true,
                _ => false
            };
        }
    }
}