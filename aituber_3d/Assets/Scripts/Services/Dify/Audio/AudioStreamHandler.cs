using System;
using System.Collections.Generic;

namespace AiTuber.Services.Dify.Audio
{
    /// <summary>
    /// 音声ストリームデータ処理用Pure C#静的クラス
    /// Base64音声データの処理、音声チャンク管理、メモリ効率化
    /// Unity非依存でユニットテスト可能
    /// </summary>
    public static class AudioStreamHandler
    {
        /// <summary>
        /// 音声処理結果
        /// </summary>
        public class AudioProcessingResult
        {
            /// <summary>
            /// 処理成功フラグ
            /// </summary>
            public bool IsSuccess { get; set; }
            
            /// <summary>
            /// 音声バイナリデータ
            /// </summary>
            public byte[] AudioData { get; set; }
            
            /// <summary>
            /// エラーメッセージ
            /// </summary>
            public string ErrorMessage { get; set; }
            
            /// <summary>
            /// 音声データサイズ（バイト）
            /// </summary>
            public int DataSizeBytes => AudioData?.Length ?? 0;
            
            /// <summary>
            /// 有効な音声データを持つかどうか
            /// </summary>
            public bool HasValidAudioData => IsSuccess && AudioData != null && AudioData.Length > 0;
        }

        /// <summary>
        /// Base64エンコードされた音声文字列を検証（簡易版）
        /// </summary>
        /// <param name="base64Audio">Base64音声文字列</param>
        /// <returns>有効であればtrue、無効であればfalse</returns>
        public static bool IsValidBase64Audio(string base64Audio)
        {
            // 安全な基本チェックのみ
            return !string.IsNullOrWhiteSpace(base64Audio) && 
                   base64Audio.Length > 0 && 
                   base64Audio.Length % 4 == 0;
        }

        /// <summary>
        /// Base64音声データをバイナリに変換
        /// </summary>
        /// <param name="base64Audio">Base64エンコードされた音声データ</param>
        /// <returns>音声処理結果</returns>
        public static AudioProcessingResult DecodeBase64Audio(string base64Audio)
        {
            var result = new AudioProcessingResult();

            if (string.IsNullOrWhiteSpace(base64Audio))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Base64 audio string is null or empty";
                return result;
            }

            try
            {
                // 事前検証
                if (!IsValidBase64Audio(base64Audio))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Invalid Base64 format";
                    return result;
                }

                // Base64デコード実行
                result.AudioData = Convert.FromBase64String(base64Audio);
                result.IsSuccess = true;

                return result;
            }
            catch (FormatException ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Base64 decode error: {ex.Message}";
                return result;
            }
            catch (OutOfMemoryException ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Memory allocation error: {ex.Message}";
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 複数の音声チャンクを結合
        /// </summary>
        /// <param name="audioChunks">音声チャンク配列</param>
        /// <returns>結合された音声データ</returns>
        public static AudioProcessingResult ConcatenateAudioChunks(List<byte[]> audioChunks)
        {
            var result = new AudioProcessingResult();

            if (audioChunks == null || audioChunks.Count == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Audio chunks list is null or empty";
                return result;
            }

            try
            {
                // 総サイズ計算
                int totalSize = 0;
                foreach (var chunk in audioChunks)
                {
                    if (chunk != null)
                        totalSize += chunk.Length;
                }

                if (totalSize == 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No valid audio data in chunks";
                    return result;
                }

                // 音声データ結合
                result.AudioData = new byte[totalSize];
                int offset = 0;

                foreach (var chunk in audioChunks)
                {
                    if (chunk != null && chunk.Length > 0)
                    {
                        Array.Copy(chunk, 0, result.AudioData, offset, chunk.Length);
                        offset += chunk.Length;
                    }
                }

                result.IsSuccess = true;
                return result;
            }
            catch (OutOfMemoryException ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Memory allocation error during concatenation: {ex.Message}";
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Unexpected error during concatenation: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 音声チャンクから統計情報を取得
        /// </summary>
        /// <param name="audioChunks">音声チャンク配列</param>
        /// <returns>統計情報</returns>
        public static AudioChunkStatistics GetAudioChunkStatistics(List<byte[]> audioChunks)
        {
            var stats = new AudioChunkStatistics();

            if (audioChunks == null || audioChunks.Count == 0)
                return stats;

            stats.TotalChunks = audioChunks.Count;

            foreach (var chunk in audioChunks)
            {
                if (chunk != null && chunk.Length > 0)
                {
                    stats.ValidChunks++;
                    stats.TotalBytes += chunk.Length;

                    if (chunk.Length > stats.LargestChunkBytes)
                        stats.LargestChunkBytes = chunk.Length;

                    if (stats.SmallestChunkBytes == 0 || chunk.Length < stats.SmallestChunkBytes)
                        stats.SmallestChunkBytes = chunk.Length;
                }
                else
                {
                    stats.NullOrEmptyChunks++;
                }
            }

            if (stats.ValidChunks > 0)
                stats.AverageChunkBytes = (double)stats.TotalBytes / stats.ValidChunks;

            return stats;
        }

    }

    /// <summary>
    /// 音声チャンク統計情報
    /// </summary>
    public class AudioChunkStatistics
    {
        /// <summary>
        /// 総チャンク数
        /// </summary>
        public int TotalChunks { get; set; }

        /// <summary>
        /// 有効チャンク数
        /// </summary>
        public int ValidChunks { get; set; }

        /// <summary>
        /// 無効（null/空）チャンク数
        /// </summary>
        public int NullOrEmptyChunks { get; set; }

        /// <summary>
        /// 総バイト数
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 平均チャンクサイズ（バイト）
        /// </summary>
        public double AverageChunkBytes { get; set; }

        /// <summary>
        /// 最大チャンクサイズ（バイト）
        /// </summary>
        public int LargestChunkBytes { get; set; }

        /// <summary>
        /// 最小チャンクサイズ（バイト）
        /// </summary>
        public int SmallestChunkBytes { get; set; }

        /// <summary>
        /// 有効性チェック
        /// </summary>
        public bool HasValidData => ValidChunks > 0 && TotalBytes > 0;
    }
}