using System.Collections.Generic;

namespace AiTuber.Services.Legacy.Dify.Data
{
    /// <summary>
    /// Dify API処理結果を格納するデータクラス
    /// ビジネスロジック層からUI層への戻り値用
    /// </summary>
    public class DifyProcessingResult
    {
        /// <summary>
        /// 会話ID（次回リクエスト時の会話継続用）
        /// </summary>
        public string ConversationId { get; set; }
        
        /// <summary>
        /// メッセージID
        /// </summary>
        public string MessageId { get; set; }
        
        /// <summary>
        /// 生成されたテキストレスポンス（全体）
        /// </summary>
        public string TextResponse { get; set; }
        
        /// <summary>
        /// 音声データチャンク配列（Base64デコード済み）
        /// </summary>
        public List<byte[]> AudioChunks { get; set; } = new List<byte[]>();
        
        /// <summary>
        /// 処理成功フラグ
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// エラーメッセージ（IsSuccess=falseの場合）
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// 処理時間（デバッグ用）
        /// </summary>
        public double ProcessingTimeMs { get; set; }
        
        /// <summary>
        /// 受信イベント総数（デバッグ用）
        /// </summary>
        public int TotalEventCount { get; set; }
        
        /// <summary>
        /// 音声チャンク数
        /// </summary>
        public int AudioChunkCount => AudioChunks?.Count ?? 0;
        
        /// <summary>
        /// 音声データの総バイト数
        /// </summary>
        public int TotalAudioBytes
        {
            get
            {
                if (AudioChunks == null) return 0;
                int total = 0;
                foreach (var chunk in AudioChunks)
                {
                    total += chunk?.Length ?? 0;
                }
                return total;
            }
        }
        
        /// <summary>
        /// 有効なテキストレスポンスを持っているかどうか
        /// </summary>
        public bool HasTextResponse => !string.IsNullOrEmpty(TextResponse);
        
        /// <summary>
        /// 音声データを持っているかどうか
        /// </summary>
        public bool HasAudioData => AudioChunkCount > 0;
    }
}