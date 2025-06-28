using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

#nullable enable

namespace AiTuber.Services.Dify.Mock
{
    /// <summary>
    /// SSE録画データ読み込みクラス
    /// Mock例外領域 - Clean Architecture例外として配置
    /// SSERecordings完全再現のためのデータ読み込み
    /// </summary>
    public class SSERecordingReader
    {
        private readonly SSERecordingData _recordingData;
        private readonly string _filePath;

        /// <summary>
        /// SSERecordingReaderを作成
        /// </summary>
        /// <param name="filePath">録画ファイルパス</param>
        /// <exception cref="ArgumentNullException">ファイルパスがnullの場合</exception>
        /// <exception cref="ArgumentException">ファイルパスが空の場合</exception>
        /// <exception cref="FileNotFoundException">ファイルが存在しない場合</exception>
        public SSERecordingReader(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            _filePath = filePath;
            
            // Unity AssetsのStreamingAssetsまたは相対パス解決
            var fullPath = ResolveFilePath(filePath);
            
            UnityEngine.Debug.Log($"[SSERecordingReader] Resolving path: {filePath} -> {fullPath}");
            
            if (!File.Exists(fullPath))
            {
                UnityEngine.Debug.LogError($"[SSERecordingReader] File not found: {fullPath}");
                throw new FileNotFoundException($"SSE recording file not found: {fullPath}");
            }

            // JSON録画データ読み込み
            var jsonContent = File.ReadAllText(fullPath);
            _recordingData = JsonConvert.DeserializeObject<SSERecordingData>(jsonContent) 
                ?? throw new InvalidOperationException("Failed to parse SSE recording data");
                
            UnityEngine.Debug.Log($"[SSERecordingReader] Loaded {_recordingData.EventCount} events, duration: {_recordingData.TotalDurationMs}ms");
        }

        /// <summary>
        /// イベント総数を取得
        /// </summary>
        /// <returns>イベント数</returns>
        public int GetEventCount()
        {
            return _recordingData.EventCount;
        }

        /// <summary>
        /// 総再生時間を取得
        /// </summary>
        /// <returns>再生時間（ミリ秒）</returns>
        public double GetTotalDurationMs()
        {
            return _recordingData.TotalDurationMs;
        }

        /// <summary>
        /// 全イベントを時系列順で取得
        /// </summary>
        /// <returns>全イベントリスト</returns>
        public List<SSERecordingEvent> GetAllEvents()
        {
            return _recordingData.Events.OrderBy(e => e.Timestamp).ToList();
        }

        /// <summary>
        /// 特定イベントタイプでフィルタリング
        /// </summary>
        /// <param name="eventType">イベントタイプ</param>
        /// <returns>該当イベントリスト</returns>
        public List<SSERecordingEvent> GetEventsByType(string eventType)
        {
            return _recordingData.Events
                .Where(e => e.EventType == eventType)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }

        /// <summary>
        /// ファイルパス解決（Unity対応）
        /// </summary>
        /// <param name="relativePath">相対パス</param>
        /// <returns>絶対パス</returns>
        private string ResolveFilePath(string relativePath)
        {
            // 絶対パスの場合はそのまま返す
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            // Unity StreamingAssets対応
            var streamingAssetsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, relativePath);
            if (File.Exists(streamingAssetsPath))
                return streamingAssetsPath;

            // プロジェクトルート相対パス
            var projectRootPath = Path.Combine(UnityEngine.Application.dataPath, "..", relativePath);
            var fullProjectPath = Path.GetFullPath(projectRootPath);
            if (File.Exists(fullProjectPath))
                return fullProjectPath;

            // そのまま返す（エラーは後で発生）
            return relativePath;
        }
    }

    /// <summary>
    /// SSE録画データ構造
    /// JSON形式の録画ファイル構造に対応
    /// </summary>
    [Serializable]
    public class SSERecordingData
    {
        [JsonProperty("recordedAt")]
        public string RecordedAt { get; set; } = "";

        [JsonProperty("eventCount")]
        public int EventCount { get; set; }

        [JsonProperty("totalDurationMs")]
        public double TotalDurationMs { get; set; }

        [JsonProperty("events")]
        public List<SSERecordingEvent> Events { get; set; } = new List<SSERecordingEvent>();
    }

    /// <summary>
    /// SSE録画イベントデータ
    /// Dify SSE形式に対応
    /// </summary>
    [Serializable]
    public class SSERecordingEvent
    {
        [JsonProperty("timestamp")]
        public double Timestamp { get; set; }

        [JsonProperty("eventType")]
        public string EventType { get; set; } = "";

        [JsonProperty("answer")]
        public string Answer { get; set; } = "";

        [JsonProperty("audio")]
        public string AudioData { get; set; } = "";

        [JsonProperty("conversationId")]
        public string ConversationId { get; set; } = "";

        [JsonProperty("messageId")]
        public string MessageId { get; set; } = "";

        [JsonProperty("taskId")]
        public string TaskId { get; set; } = "";
    }
}