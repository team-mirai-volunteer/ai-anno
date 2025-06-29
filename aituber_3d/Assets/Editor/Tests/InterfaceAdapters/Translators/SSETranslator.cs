using System;
using System.Collections.Generic;
using System.Linq;
using AiTuber.Services.Dify.Domain.Entities;

#nullable enable

namespace AiTuber.Services.Dify.InterfaceAdapters.Translators
{
    /// <summary>
    /// Server-Sent Events (SSE) データ変換クラス
    /// InterfaceAdapters層 Clean Architecture準拠
    /// Legacy SSEParserからのリファクタリング版
    /// </summary>
    public class SSETranslator
    {
        private const string DATA_PREFIX = "data: ";
        private const string DONE_MARKER = "[DONE]";

        /// <summary>
        /// SSETranslator を作成
        /// </summary>
        public SSETranslator()
        {
        }

        /// <summary>
        /// 単一のSSEラインを解析
        /// </summary>
        /// <param name="sseLine">SSEライン</param>
        /// <returns>解析結果</returns>
        /// <exception cref="ArgumentNullException">sseLine が null の場合</exception>
        public SSELineResult ParseSingleLine(string? sseLine)
        {
            if (sseLine is null)
                throw new ArgumentNullException(nameof(sseLine));

            // 空行・コメント行をスキップ
            if (string.IsNullOrWhiteSpace(sseLine) || sseLine.StartsWith(":"))
            {
                return new SSELineResult { IsSkipped = true };
            }

            // data: プレフィックスチェック
            if (!sseLine.StartsWith(DATA_PREFIX))
            {
                return new SSELineResult { IsSkipped = true };
            }

            var content = sseLine.Substring(DATA_PREFIX.Length).Trim();

            // DONEマーカーチェック
            if (content == DONE_MARKER)
            {
                return new SSELineResult { IsEndMarker = true };
            }

            // JSON解析
            try
            {
                var streamEvent = ParseJsonToDifyStreamEvent(content);
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
                    ErrorMessage = $"JSON parse error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// SSEストリームから全イベントを解析
        /// </summary>
        /// <param name="sseContent">SSEストリーム全体</param>
        /// <returns>解析されたイベントのシーケンス</returns>
        /// <exception cref="ArgumentNullException">sseContent が null の場合</exception>
        public IEnumerable<DifyStreamEvent> ParseEvents(string sseContent)
        {
            if (sseContent is null)
                throw new ArgumentNullException(nameof(sseContent));

            var lines = sseContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.None);
            
            foreach (var line in lines)
            {
                var lineResult = ParseSingleLine(line);
                
                if (lineResult.IsEndMarker)
                    yield break;
                
                if (lineResult.IsValid && lineResult.Event != null)
                    yield return lineResult.Event;
            }
        }

        /// <summary>
        /// SSEストリームの妥当性検証
        /// </summary>
        /// <param name="sseContent">検証対象のSSEコンテンツ</param>
        /// <returns>検証結果</returns>
        /// <exception cref="ArgumentNullException">sseContent が null の場合</exception>
        public SSEValidationResult ValidateStream(string sseContent)
        {
            if (sseContent is null)
                throw new ArgumentNullException(nameof(sseContent));

            var result = new SSEValidationResult();
            var lines = sseContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var lineResult = ParseSingleLine(line);

                if (lineResult.IsEndMarker)
                {
                    result.HasEndMarker = true;
                }
                else if (lineResult.IsValid && lineResult.Event != null)
                {
                    if (lineResult.Event.IsMessageEvent)
                        result.TextEventCount++;
                    if (lineResult.Event.IsAudioEvent)
                        result.AudioEventCount++;
                }
                else if (lineResult.IsParseError)
                {
                    result.ParseErrorCount++;
                    result.ParseErrors.Add(lineResult.ErrorMessage ?? "Unknown parse error");
                }
            }

            result.IsValid = result.ParseErrorCount == 0;
            return result;
        }

        /// <summary>
        /// イベント配列から統計情報を取得
        /// </summary>
        /// <param name="events">イベント配列</param>
        /// <returns>統計情報</returns>
        /// <exception cref="ArgumentNullException">events が null の場合</exception>
        public SSEEventStatistics GetEventStatistics(IEnumerable<DifyStreamEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            var stats = new SSEEventStatistics();

            foreach (var evt in events)
            {
                stats.TotalEvents++;

                if (evt.IsMessageEvent)
                    stats.TextMessageCount++;
                else if (evt.IsAudioEvent)
                    stats.TTSMessageCount++;
                else if (evt.IsEndEvent)
                    stats.MessageEndCount++;
            }

            return stats;
        }

        /// <summary>
        /// JSON文字列をDifyStreamEventに変換
        /// </summary>
        /// <param name="jsonData">JSON文字列</param>
        /// <returns>変換されたDifyStreamEvent</returns>
        private DifyStreamEvent? ParseJsonToDifyStreamEvent(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
                return null;

            // 明らかに不正なJSONの場合は例外をスロー
            if (!jsonData.Trim().StartsWith("{") || !jsonData.Trim().EndsWith("}"))
            {
                throw new FormatException("Invalid JSON format");
            }

            // 簡単な文字列パースでJSONから主要フィールドを抽出
            var eventType = ExtractJsonValue(jsonData, "eventType");
            var answer = ExtractJsonValue(jsonData, "answer");
            var audio = ExtractJsonValue(jsonData, "audio");
            var conversationId = ExtractJsonValue(jsonData, "conversationId");
            var messageId = ExtractJsonValue(jsonData, "messageId");

            // イベントタイプに基づいてDifyStreamEventを作成
            if (eventType == "message" && !string.IsNullOrEmpty(answer) && !string.IsNullOrEmpty(conversationId))
            {
                return DifyStreamEvent.CreateMessageEvent(answer, conversationId, messageId ?? "");
            }
            else if (eventType == "tts_message" && !string.IsNullOrEmpty(audio) && !string.IsNullOrEmpty(conversationId))
            {
                return DifyStreamEvent.CreateAudioEvent(audio, conversationId);
            }
            else if (eventType == "message_end" && !string.IsNullOrEmpty(conversationId))
            {
                return DifyStreamEvent.CreateEndEvent(conversationId, messageId ?? "");
            }

            // 有効なフィールドが見つからない場合はnullを返す
            if (string.IsNullOrEmpty(eventType) && string.IsNullOrEmpty(answer) && 
                string.IsNullOrEmpty(audio) && string.IsNullOrEmpty(conversationId))
            {
                throw new FormatException("No valid JSON fields found");
            }

            return null;
        }

        /// <summary>
        /// JSON文字列から指定されたキーの値を抽出
        /// </summary>
        /// <param name="json">JSON文字列</param>
        /// <param name="key">抽出するキー</param>
        /// <returns>抽出された値</returns>
        private string? ExtractJsonValue(string json, string key)
        {
            var pattern = $"\"{key}\":\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    /// <summary>
    /// SSEライン解析結果
    /// </summary>
    public class SSELineResult
    {
        /// <summary>
        /// 解析されたイベント
        /// </summary>
        public DifyStreamEvent? Event { get; set; }

        /// <summary>
        /// 解析が成功したかどうか
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// スキップされたライン
        /// </summary>
        public bool IsSkipped { get; set; }

        /// <summary>
        /// 終了マーカー [DONE] かどうか
        /// </summary>
        public bool IsEndMarker { get; set; }

        /// <summary>
        /// パースエラーが発生したかどうか
        /// </summary>
        public bool IsParseError { get; set; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// SSEストリーム検証結果
    /// </summary>
    public class SSEValidationResult
    {
        /// <summary>
        /// 全体的に妥当なストリームかどうか
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 終了マーカーが存在するかどうか
        /// </summary>
        public bool HasEndMarker { get; set; }

        /// <summary>
        /// テキストイベント数
        /// </summary>
        public int TextEventCount { get; set; }

        /// <summary>
        /// 音声イベント数
        /// </summary>
        public int AudioEventCount { get; set; }

        /// <summary>
        /// パースエラー数
        /// </summary>
        public int ParseErrorCount { get; set; }

        /// <summary>
        /// パースエラーメッセージ配列
        /// </summary>
        public List<string> ParseErrors { get; set; } = new List<string>();
    }

    /// <summary>
    /// SSEイベント統計情報
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
        /// TTS音声メッセージイベント数
        /// </summary>
        public int TTSMessageCount { get; set; }

        /// <summary>
        /// メッセージ終了イベント数
        /// </summary>
        public int MessageEndCount { get; set; }
    }
}