using AiTuber.Services.Dify.Domain.Services;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Entities
{
    /// <summary>
    /// Dify Server-Sent Events ストリームイベントエンティティ
    /// リファクタリング済み - 責任分離によるClean Architecture準拠
    /// </summary>
    public class DifyStreamEvent
    {
        /// <summary>
        /// イベントコアデータ
        /// </summary>
        public DifyStreamEventCore Core { get; }

        /// <summary>
        /// DifyStreamEventを作成
        /// </summary>
        /// <param name="core">イベントコアデータ</param>
        internal DifyStreamEvent(DifyStreamEventCore core)
        {
            Core = core ?? throw new System.ArgumentNullException(nameof(core));
        }

        /// <summary>
        /// イベント種別（後方互換性）
        /// </summary>
        public string EventType => Core.EventType;

        /// <summary>
        /// 会話ID（後方互換性）
        /// </summary>
        public string ConversationId => Core.ConversationId;

        /// <summary>
        /// メッセージID（後方互換性）
        /// </summary>
        public string? MessageId => Core.MessageId;

        /// <summary>
        /// テキストメッセージ内容（後方互換性）
        /// </summary>
        public string? Answer => Core.Answer;

        /// <summary>
        /// Base64エンコードされた音声データ（後方互換性）
        /// </summary>
        public string? Audio => Core.Audio;

        /// <summary>
        /// イベント作成時刻（後方互換性）
        /// </summary>
        public long CreatedAt => Core.CreatedAt;

        /// <summary>
        /// タスクID（後方互換性）
        /// </summary>
        public string? TaskId => Core.TaskId;

        /// <summary>
        /// ワークフロー実行ID（後方互換性）
        /// </summary>
        public string? WorkflowRunId => Core.WorkflowRunId;

        /// <summary>
        /// メッセージイベントかどうかを判定（後方互換性）
        /// </summary>
        public bool IsMessageEvent => DifyStreamEventValidator.IsMessageEvent(this);

        /// <summary>
        /// 音声イベントかどうかを判定（後方互換性）
        /// </summary>
        public bool IsAudioEvent => DifyStreamEventValidator.IsAudioEvent(this);

        /// <summary>
        /// 終了イベントかどうかを判定（後方互換性）
        /// </summary>
        public bool IsEndEvent => DifyStreamEventValidator.IsEndEvent(this);

        /// <summary>
        /// 有効な音声データを持っているか判定（後方互換性）
        /// </summary>
        public bool HasValidAudio => DifyStreamEventValidator.HasValidAudio(this);

        /// <summary>
        /// イベントの妥当性を検証（後方互換性）
        /// </summary>
        /// <returns>有効な場合true</returns>
        public bool IsValid() => DifyStreamEventValidator.IsValid(this);

        #region Factory Methods (後方互換性)

        /// <summary>
        /// メッセージイベントを作成（後方互換性）
        /// </summary>
        public static DifyStreamEvent CreateMessageEvent(string answer, string conversationId, string messageId)
            => DifyStreamEventFactory.CreateMessageEvent(answer, conversationId, messageId);

        /// <summary>
        /// 音声イベントを作成（後方互換性）
        /// </summary>
        public static DifyStreamEvent CreateAudioEvent(string audioData, string conversationId, string messageId)
            => DifyStreamEventFactory.CreateAudioEvent(audioData, conversationId, messageId);

        /// <summary>
        /// 音声イベントを作成（MessageID省略版、後方互換性）
        /// </summary>
        public static DifyStreamEvent CreateAudioEvent(string audioData, string conversationId)
            => DifyStreamEventFactory.CreateAudioEvent(audioData, conversationId);

        /// <summary>
        /// 終了イベントを作成（後方互換性）
        /// </summary>
        public static DifyStreamEvent CreateEndEvent(string conversationId, string? messageId = null)
            => DifyStreamEventFactory.CreateEndEvent(conversationId, messageId);

        /// <summary>
        /// カスタムイベントを作成（後方互換性）
        /// </summary>
        public static DifyStreamEvent CreateCustomEvent(
            string eventType,
            string conversationId,
            string? messageId = null,
            string? taskId = null,
            string? workflowRunId = null)
            => DifyStreamEventFactory.CreateCustomEvent(eventType, conversationId, messageId, taskId, workflowRunId);

        /// <summary>
        /// JSON文字列からDifyStreamEventを作成（後方互換性）
        /// </summary>
        public static DifyStreamEvent? ParseJsonToDifyStreamEvent(string jsonData)
            => DifyStreamEventFactory.ParseFromJson(jsonData);

        #endregion
    }
}