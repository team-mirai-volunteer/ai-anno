using System;
using System.Collections.Generic;
using System.IO;
using AiTuber.Services.Dify.Data;
using UnityEngine;

namespace AiTuber.Services.Dify.Infrastructure
{
    /// <summary>
    /// Server-Sent Events (SSE) パーサー
    /// Pure C# 静的クラス、Unity非依存でユニットテスト可能
    /// Dify ストリーミングレスポンスの解析処理
    /// </summary>
    public static class SSEParser
    {
        /// <summary>
        /// SSE データ終了マーカー
        /// </summary>
        public const string DONE_MARKER = "[DONE]";
        
        /// <summary>
        /// SSE データプレフィックス
        /// </summary>
        public const string DATA_PREFIX = "data: ";

        /// <summary>
        /// SSE ストリームから DifyStreamEvent のシーケンスをパース
        /// </summary>
        /// <param name="sseContent">SSE ストリーム全体（改行区切り）</param>
        /// <returns>パースされたイベントのシーケンス</returns>
        /// <exception cref="ArgumentNullException">sseContent が null の場合</exception>
        public static IEnumerable<DifyStreamEvent> ParseEvents(string sseContent)
        {
            if (sseContent is null)
                throw new ArgumentNullException(nameof(sseContent));

            // yield returnと互換性を保つため、直接行分割してパース
            var lines = sseContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.None);
            
            foreach (var line in lines)
            {
                var lineResult = ParseSingleLine(line);
                if (lineResult != null)
                {
                    if (lineResult.IsEndMarker)
                        yield break;
                    
                    if (lineResult.IsValid && lineResult.Event != null)
                        yield return lineResult.Event;
                }
            }
        }

        /// <summary>
        /// TextReader から DifyStreamEvent のシーケンスをパース
        /// ストリーミング処理用（リアルタイム解析）
        /// </summary>
        /// <param name="reader">SSE データを読み取る TextReader</param>
        /// <returns>パースされたイベントのシーケンス</returns>
        /// <exception cref="ArgumentNullException">reader が null の場合</exception>
        public static IEnumerable<DifyStreamEvent> ParseEventsFromReader(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var eventData = ParseSingleLine(line);
                if (eventData != null)
                {
                    if (eventData.IsEndMarker)
                        yield break;
                    
                    if (eventData.Event != null)
                        yield return eventData.Event;
                }
            }
        }

        /// <summary>
        /// 単一の SSE ライン解析
        /// </summary>
        /// <param name="line">SSE ライン（"data: {JSON}" 形式）</param>
        /// <returns>解析結果</returns>
        public static SSELineResult ParseSingleLine(string line)
        {
            // null や空文字列の場合はスキップとして処理
            if (line is null)
            {
                return new SSELineResult { IsSkipped = true };
            }

            // 空行・コメント行をスキップ
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(":"))
            {
                return new SSELineResult { IsSkipped = true };
            }

            // "data: " または "event: " プレフィックスチェック
            string content = null;
            
            if (line.StartsWith(DATA_PREFIX))
            {
                content = line.Substring(DATA_PREFIX.Length).Trim();
            }
            else if (line.StartsWith("event: "))
            {
                content = line.Substring("event: ".Length).Trim();
                
                // event: message_end を JSON として処理
                if (content == "message_end")
                {
                    content = "{\"event\":\"message_end\"}";
                }
            }
            else
            {
                return new SSELineResult { IsSkipped = true };
            }

            var jsonData = content;

            // 終了マーカーチェック
            if (jsonData == DONE_MARKER)
            {
                return new SSELineResult { IsEndMarker = true };
            }

            // JSON パース
            try
            {
                var streamEvent = ParseJsonToStreamEvent(jsonData);
                return new SSELineResult 
                { 
                    Event = streamEvent,
                    IsValid = streamEvent != null
                };
            }
            catch (Exception ex)
            {
                return new SSELineResult 
                { 
                    IsParseError = true,
                    ErrorMessage = $"JSON parse error: {ex.Message}",
                    RawData = jsonData
                };
            }
        }

        /// <summary>
        /// SSE ストリームの妥当性検証
        /// </summary>
        /// <param name="sseContent">検証対象のSSEコンテンツ</param>
        /// <returns>検証結果</returns>
        /// <exception cref="ArgumentNullException">sseContent が null の場合</exception>
        public static SSEValidationResult ValidateStream(string sseContent)
        {
            if (sseContent is null)
                throw new ArgumentNullException(nameof(sseContent));

            var result = new SSEValidationResult();
            var lines = sseContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var lineResult = ParseSingleLine(line);
                result.TotalLines++;

                if (lineResult.IsSkipped)
                {
                    result.SkippedLines++;
                }
                else if (lineResult.IsEndMarker)
                {
                    result.HasEndMarker = true;
                    result.ValidLines++;
                }
                else if (lineResult.IsValid)
                {
                    result.ValidLines++;
                    if (lineResult.Event.IsTextMessage)
                        result.TextEventCount++;
                    if (lineResult.Event.IsTTSMessage)
                        result.AudioEventCount++;
                    if (lineResult.Event.IsError)
                        result.ErrorEventCount++;
                }
                else if (lineResult.IsParseError)
                {
                    result.ParseErrorCount++;
                    result.ParseErrors.Add(lineResult.ErrorMessage);
                }
            }

            result.IsValid = result.ValidLines > 0 && result.ParseErrorCount == 0;
            return result;
        }

        /// <summary>
        /// イベント種別別の統計情報を取得
        /// </summary>
        /// <param name="events">解析対象のイベント配列</param>
        /// <returns>統計情報</returns>
        /// <exception cref="ArgumentNullException">events が null の場合</exception>
        public static SSEEventStatistics GetEventStatistics(IEnumerable<DifyStreamEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            var stats = new SSEEventStatistics();

            foreach (var evt in events)
            {
                stats.TotalEvents++;

                if (evt.IsTextMessage)
                {
                    stats.TextMessageCount++;
                    if (evt.HasValidTextMessage)
                        stats.ValidTextMessageCount++;
                }
                else if (evt.IsTTSMessage)
                {
                    stats.TTSMessageCount++;
                    if (evt.HasValidAudioData)
                        stats.ValidAudioMessageCount++;
                }
                else if (evt.IsMessageEnd)
                {
                    stats.MessageEndCount++;
                }
                else if (evt.IsWorkflowStarted)
                {
                    stats.WorkflowStartedCount++;
                }
                else if (evt.IsError)
                {
                    stats.ErrorEventCount++;
                }
                else
                {
                    stats.UnknownEventCount++;
                }

                // 最初・最後のタイムスタンプ記録
                if (evt.created_at > 0)
                {
                    if (stats.FirstEventTimestamp == 0 || evt.created_at < stats.FirstEventTimestamp)
                        stats.FirstEventTimestamp = evt.created_at;
                    
                    if (evt.created_at > stats.LastEventTimestamp)
                        stats.LastEventTimestamp = evt.created_at;
                }
            }

            return stats;
        }

        /// <summary>
        /// JSON文字列をDifyStreamEventにパース（Unity非依存）
        /// </summary>
        /// <param name="jsonData">JSON文字列</param>
        /// <returns>パースされたDifyStreamEvent</returns>
        private static DifyStreamEvent ParseJsonToStreamEvent(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
                return null;

            try
            {
                // 簡単な文字列パースでJSONから主要フィールドを抽出
                var result = new DifyStreamEvent();
                bool hasValidField = false;
                
                // "event"フィールドの抽出
                var eventMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"event\":\\s*\"([^\"]+)\"");
                if (eventMatch.Success)
                {
                    result.@event = eventMatch.Groups[1].Value;
                    hasValidField = true;
                }
                
                // "answer"フィールドの抽出（ユニコードエスケープ対応）
                var answerMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"answer\":\\s*\"([^\"]+)\"");
                if (answerMatch.Success)
                {
                    var answerValue = answerMatch.Groups[1].Value;
                    // ユニコードエスケープを復号化
                    answerValue = System.Text.RegularExpressions.Regex.Replace(answerValue, @"\\u([0-9a-fA-F]{4})", 
                        match => ((char)int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber)).ToString());
                    result.answer = answerValue;
                    hasValidField = true;
                }
                
                // "conversation_id"フィールドの抽出
                var convIdMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"conversation_id\":\\s*\"([^\"]+)\"");
                if (convIdMatch.Success)
                {
                    result.conversation_id = convIdMatch.Groups[1].Value;
                    hasValidField = true;
                }
                
                // "message_id"フィールドの抽出
                var msgIdMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"message_id\":\\s*\"([^\"]+)\"");
                if (msgIdMatch.Success)
                {
                    result.message_id = msgIdMatch.Groups[1].Value;
                    hasValidField = true;
                }
                
                // "id"フィールドの抽出
                var idMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"id\":\\s*\"([^\"]+)\"");
                if (idMatch.Success)
                {
                    result.id = idMatch.Groups[1].Value;
                    hasValidField = true;
                }
                
                // "audio"フィールドの抽出
                var audioMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"audio\":\\s*\"([^\"]+)\"");
                if (audioMatch.Success)
                {
                    result.audio = audioMatch.Groups[1].Value;
                    hasValidField = true;
                }
                
                // "created_at"フィールドの抽出
                var createdAtMatch = System.Text.RegularExpressions.Regex.Match(jsonData, "\"created_at\":\\s*(\\d+)");
                if (createdAtMatch.Success && int.TryParse(createdAtMatch.Groups[1].Value, out var timestamp))
                {
                    result.created_at = timestamp;
                    hasValidField = true;
                }
                
                // 有効なフィールドが1つも見つからなかった場合はnullを返す
                return hasValidField ? result : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SSEParser] JSON parsing failed: {ex.Message}");
                return null;
            }
        }

    }

    /// <summary>
    /// SSE ライン解析結果
    /// </summary>
    public class SSELineResult
    {
        /// <summary>
        /// 解析されたイベント（正常時のみ）
        /// </summary>
        public DifyStreamEvent Event { get; set; }

        /// <summary>
        /// 解析が成功したかどうか
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// スキップされたライン（空行・コメント等）
        /// </summary>
        public bool IsSkipped { get; set; }

        /// <summary>
        /// 終了マーカー "[DONE]" かどうか
        /// </summary>
        public bool IsEndMarker { get; set; }

        /// <summary>
        /// JSON パースエラーが発生したかどうか
        /// </summary>
        public bool IsParseError { get; set; }

        /// <summary>
        /// エラーメッセージ（パースエラー時）
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 生のJSONデータ（デバッグ用）
        /// </summary>
        public string RawData { get; set; }
    }

    /// <summary>
    /// SSE ストリーム検証結果
    /// </summary>
    public class SSEValidationResult
    {
        /// <summary>
        /// 総ライン数
        /// </summary>
        public int TotalLines { get; set; }

        /// <summary>
        /// 有効なライン数
        /// </summary>
        public int ValidLines { get; set; }

        /// <summary>
        /// スキップされたライン数
        /// </summary>
        public int SkippedLines { get; set; }

        /// <summary>
        /// パースエラー数
        /// </summary>
        public int ParseErrorCount { get; set; }

        /// <summary>
        /// パースエラーメッセージ配列
        /// </summary>
        public List<string> ParseErrors { get; set; } = new List<string>();

        /// <summary>
        /// テキストイベント数
        /// </summary>
        public int TextEventCount { get; set; }

        /// <summary>
        /// 音声イベント数
        /// </summary>
        public int AudioEventCount { get; set; }

        /// <summary>
        /// エラーイベント数
        /// </summary>
        public int ErrorEventCount { get; set; }

        /// <summary>
        /// 終了マーカーが存在するかどうか
        /// </summary>
        public bool HasEndMarker { get; set; }

        /// <summary>
        /// 全体的に妥当なストリームかどうか
        /// </summary>
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// SSE イベント統計情報
    /// </summary>
    public class SSEEventStatistics
    {
        /// <summary>
        /// 総イベント数
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// テキストメッセージイベント数
        /// </summary>
        public int TextMessageCount { get; set; }

        /// <summary>
        /// 有効なテキストメッセージ数
        /// </summary>
        public int ValidTextMessageCount { get; set; }

        /// <summary>
        /// TTS音声メッセージイベント数
        /// </summary>
        public int TTSMessageCount { get; set; }

        /// <summary>
        /// 有効な音声メッセージ数
        /// </summary>
        public int ValidAudioMessageCount { get; set; }

        /// <summary>
        /// メッセージ終了イベント数
        /// </summary>
        public int MessageEndCount { get; set; }

        /// <summary>
        /// ワークフロー開始イベント数
        /// </summary>
        public int WorkflowStartedCount { get; set; }

        /// <summary>
        /// エラーイベント数
        /// </summary>
        public int ErrorEventCount { get; set; }

        /// <summary>
        /// 不明なイベント数
        /// </summary>
        public int UnknownEventCount { get; set; }

        /// <summary>
        /// 最初のイベントタイムスタンプ
        /// </summary>
        public long FirstEventTimestamp { get; set; }

        /// <summary>
        /// 最後のイベントタイムスタンプ
        /// </summary>
        public long LastEventTimestamp { get; set; }

        /// <summary>
        /// イベント期間（秒）
        /// </summary>
        public double EventDurationSeconds => 
            LastEventTimestamp > FirstEventTimestamp 
                ? LastEventTimestamp - FirstEventTimestamp 
                : 0;
    }
}