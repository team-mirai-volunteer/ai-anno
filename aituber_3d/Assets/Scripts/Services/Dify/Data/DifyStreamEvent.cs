using System;

namespace AiTuber.Services.Dify.Data
{
    /// <summary>
    /// Dify Server-Sent Events (SSE) イベントデータクラス
    /// ストリーミングレスポンスのパース結果格納用
    /// </summary>
    [System.Serializable]
    public class DifyStreamEvent
    {
        /// <summary>
        /// イベント種別 ("message", "tts_message", "message_end", "workflow_started", "error" など)
        /// </summary>
        public string @event { get; set; }
        
        /// <summary>
        /// 会話ID
        /// </summary>
        public string conversation_id { get; set; }
        
        /// <summary>
        /// メッセージID
        /// </summary>
        public string message_id { get; set; }
        
        /// <summary>
        /// テキストメッセージ内容（部分的なストリーミングデータ）
        /// </summary>
        public string answer { get; set; }
        
        /// <summary>
        /// Base64エンコードされた音声データ（MP3形式）
        /// </summary>
        public string audio { get; set; }
        
        /// <summary>
        /// イベント作成時刻（Unix timestamp）
        /// </summary>
        public long created_at { get; set; }
        
        /// <summary>
        /// タスクID
        /// </summary>
        public string task_id { get; set; }
        
        /// <summary>
        /// ワークフロー実行ID
        /// </summary>
        public string workflow_run_id { get; set; }
        
        /// <summary>
        /// エラー詳細情報
        /// </summary>
        public string error { get; set; }
        
        /// <summary>
        /// イベントID（messageイベントなどで使用）
        /// </summary>
        public string id { get; set; }
        
        // イベント種別判定プロパティ
        
        /// <summary>
        /// テキストメッセージイベントかどうか
        /// </summary>
        public bool IsTextMessage => @event == "message";
        
        /// <summary>
        /// TTS音声メッセージイベントかどうか
        /// </summary>
        public bool IsTTSMessage => @event == "tts_message";
        
        /// <summary>
        /// メッセージ終了イベントかどうか
        /// </summary>
        public bool IsMessageEnd => @event == "message_end";
        
        /// <summary>
        /// ワークフロー開始イベントかどうか
        /// </summary>
        public bool IsWorkflowStarted => @event == "workflow_started";
        
        /// <summary>
        /// エラーイベントかどうか
        /// </summary>
        public bool IsError => @event == "error";
        
        /// <summary>
        /// 有効なテキストメッセージを持っているかどうか
        /// </summary>
        public bool HasValidTextMessage => IsTextMessage && !string.IsNullOrEmpty(answer);
        
        /// <summary>
        /// 有効な音声データを持っているかどうか
        /// </summary>
        public bool HasValidAudioData => IsTTSMessage && !string.IsNullOrEmpty(audio);
        
        /// <summary>
        /// 作成時刻をDateTimeに変換
        /// </summary>
        public DateTime CreatedDateTime => DateTimeOffset.FromUnixTimeSeconds(created_at).DateTime;
        
        /// <summary>
        /// 有効なデータを持っているかどうか
        /// </summary>
        public bool HasValidData => 
            !string.IsNullOrEmpty(@event) || 
            !string.IsNullOrEmpty(answer) || 
            !string.IsNullOrEmpty(audio) ||
            !string.IsNullOrEmpty(conversation_id) ||
            !string.IsNullOrEmpty(message_id);
    }
}