using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.TestTools;
using AiTuber.Services.Dify.InterfaceAdapters.Translators;
using AiTuber.Services.Dify.Domain.Entities;

#nullable enable

namespace AiTuber.Tests.Dify.InterfaceAdapters
{
    /// <summary>
    /// SSETranslator のユニットテスト
    /// InterfaceAdapters層 Clean Architecture準拠
    /// Legacy SSEParserからのリファクタリング版
    /// </summary>
    [TestFixture]
    public class SSETranslatorTests
    {
        private SSETranslator _translator;

        [SetUp]
        public void SetUp()
        {
            _translator = new SSETranslator();
        }

        #region Constructor Tests

        [Test]
        public void SSETranslator作成_正常にインスタンス作成()
        {
            // Act & Assert
            Assert.IsNotNull(_translator);
        }

        #endregion

        #region ParseSingleLine Tests

        [Test]
        public void ParseSingleLine_有効なメッセージイベント_正しく解析()
        {
            // Arrange
            var sseLine = "data: {\"eventType\":\"message\",\"answer\":\"こんにちは\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Event);
            Assert.IsTrue(result.Event.IsMessageEvent);
            Assert.AreEqual("こんにちは", result.Event.Answer);
            Assert.AreEqual("conv-123", result.Event.ConversationId);
            Assert.AreEqual("msg-456", result.Event.MessageId);
        }

        [Test]
        public void ParseSingleLine_有効な音声イベント_正しく解析()
        {
            // Arrange
            var audioData = "//PExABatDnYAVnAADrrO3E88zvpN09N";
            var sseLine = $"data: {{\"eventType\":\"tts_message\",\"audio\":\"{audioData}\",\"conversationId\":\"conv-123\"}}";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Event);
            Assert.IsTrue(result.Event.IsAudioEvent);
            Assert.IsTrue(result.Event.HasValidAudio);
            Assert.AreEqual("conv-123", result.Event.ConversationId);
        }

        [Test]
        public void ParseSingleLine_終了イベント_正しく解析()
        {
            // Arrange
            var sseLine = "data: {\"eventType\":\"message_end\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Event);
            Assert.IsTrue(result.Event.IsEndEvent);
            Assert.AreEqual("conv-123", result.Event.ConversationId);
            Assert.AreEqual("msg-456", result.Event.MessageId);
        }

        [Test]
        public void ParseSingleLine_DONEマーカー_終了マーカーとして認識()
        {
            // Arrange
            var sseLine = "data: [DONE]";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsEndMarker);
            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Event);
        }

        [Test]
        public void ParseSingleLine_空行_スキップとして処理()
        {
            // Arrange
            var sseLine = "";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSkipped);
            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Event);
        }

        [Test]
        public void ParseSingleLine_コメント行_スキップとして処理()
        {
            // Arrange
            var sseLine = ": これはコメント行です";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSkipped);
            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Event);
        }

        [Test]
        public void ParseSingleLine_null入力_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _translator.ParseSingleLine(null));
        }

        [Test]
        public void ParseSingleLine_不正なJSON_パースエラーとして処理()
        {
            // Arrange
            var sseLine = "data: {invalid json}";

            // Act
            var result = _translator.ParseSingleLine(sseLine);

            // Debug output
            UnityEngine.Debug.Log($"IsParseError: {result.IsParseError}, IsValid: {result.IsValid}, ErrorMessage: {result.ErrorMessage}");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsParseError, $"Expected IsParseError=True but got {result.IsParseError}");
            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Event);
            Assert.IsNotNull(result.ErrorMessage);
        }

        #endregion

        #region ParseEvents Tests

        [Test]
        public void ParseEvents_複数有効イベント_すべて正しく解析()
        {
            // Arrange
            var sseContent = "data: {\"eventType\":\"message\",\"answer\":\"こんにちは\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n" +
                           "data: {\"eventType\":\"message_end\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n";

            // Act
            var events = _translator.ParseEvents(sseContent).ToList();

            // Assert
            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(events[0].IsMessageEvent);
            Assert.AreEqual("こんにちは", events[0].Answer);
            Assert.IsTrue(events[1].IsEndEvent);
        }

        [Test]
        public void ParseEvents_DONEマーカー含む_DONEで解析終了()
        {
            // Arrange
            var sseContent = "data: {\"eventType\":\"message\",\"answer\":\"こんにちは\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n" +
                           "data: [DONE]\n\n" +
                           "data: {\"eventType\":\"message_end\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n";

            // Act
            var events = _translator.ParseEvents(sseContent).ToList();

            // Assert
            Assert.AreEqual(1, events.Count); // DONEの後のイベントは処理されない
            Assert.IsTrue(events[0].IsMessageEvent);
        }

        [Test]
        public void ParseEvents_null入力_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _translator.ParseEvents(null).ToList());
        }

        [Test]
        public void ParseEvents_空文字列_空のシーケンス()
        {
            // Arrange
            var sseContent = "";

            // Act
            var events = _translator.ParseEvents(sseContent).ToList();

            // Assert
            Assert.AreEqual(0, events.Count);
        }

        #endregion

        #region ValidateStream Tests

        [Test]
        public void ValidateStream_有効なストリーム_正しい統計情報()
        {
            // Arrange
            var sseContent = "data: {\"eventType\":\"message\",\"answer\":\"こんにちは\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n" +
                           "data: {\"eventType\":\"tts_message\",\"audio\":\"dGVzdA==\",\"conversationId\":\"conv-123\"}\n\n" +
                           "data: {\"eventType\":\"message_end\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n" +
                           "data: [DONE]\n\n";

            // Act
            var result = _translator.ValidateStream(sseContent);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.HasEndMarker);
            Assert.AreEqual(1, result.TextEventCount);
            Assert.AreEqual(1, result.AudioEventCount);
            Assert.AreEqual(0, result.ParseErrorCount);
        }

        [Test]
        public void ValidateStream_パースエラー含む_エラー情報記録()
        {
            // Arrange
            var sseContent = "data: {\"eventType\":\"message\",\"answer\":\"こんにちは\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n" +
                           "data: {invalid json}\n\n" +
                           "data: {\"eventType\":\"message_end\",\"conversationId\":\"conv-123\",\"messageId\":\"msg-456\"}\n\n";

            // Act
            var result = _translator.ValidateStream(sseContent);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.ParseErrorCount);
            Assert.IsTrue(result.ParseErrors.Count > 0);
        }

        [Test]
        public void ValidateStream_null入力_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _translator.ValidateStream(null));
        }

        #endregion

        #region GetEventStatistics Tests

        [Test]
        public void GetEventStatistics_イベント配列_正しい統計情報()
        {
            // Arrange
            var events = new List<DifyStreamEvent>
            {
                DifyStreamEvent.CreateMessageEvent("こんにちは", "conv-123", "msg-456"),
                DifyStreamEvent.CreateAudioEvent("dGVzdA==", "conv-123"),
                DifyStreamEvent.CreateEndEvent("conv-123", "msg-456")
            };

            // Act
            var stats = _translator.GetEventStatistics(events);

            // Assert
            Assert.AreEqual(3, stats.TotalEvents);
            Assert.AreEqual(1, stats.TextMessageCount);
            Assert.AreEqual(1, stats.TTSMessageCount);
            Assert.AreEqual(1, stats.MessageEndCount);
        }

        [Test]
        public void GetEventStatistics_null入力_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _translator.GetEventStatistics(null));
        }

        #endregion
    }
}