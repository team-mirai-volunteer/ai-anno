using NUnit.Framework;
using System;
using AiTuber.Services.Legacy.Dify.Infrastructure;
using AiTuber.Services.Legacy.Dify.Data;

namespace AiTuber.Tests.Legacy.Dify.SSE
{
    /// <summary>
    /// SSEParser クラスのユニットテスト
    /// 同期的Pure C#メソッドのみテスト、Unity Test Runner安全な実装
    /// </summary>
    [TestFixture]
    public class SSEParserTests
    {

        #region ParseSSEStream Tests

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void SSEストリーム解析_無効入力_失敗結果返却テスト(string input)
        {
            // Act
            var events = SSEParser.ParseEvents(input ?? "");
            
            // Assert
            Assert.IsNotNull(events);
            var eventList = new System.Collections.Generic.List<DifyStreamEvent>(events);
            Assert.AreEqual(0, eventList.Count);
        }

        [Test]
        public void SSEストリーム解析_単一イベント_成功結果返却テスト()
        {
            // Arrange
            string sseData = "data: {\"event\":\"message\",\"answer\":\"Hello World\",\"conversation_id\":\"conv-123\"}\n\n";
            
            // Act
            var events = SSEParser.ParseEvents(sseData);
            var eventList = new System.Collections.Generic.List<DifyStreamEvent>(events);
            
            // Assert
            Assert.AreEqual(1, eventList.Count);
            
            var firstEvent = eventList[0];
            Assert.AreEqual("message", firstEvent.@event);
            Assert.AreEqual("Hello World", firstEvent.answer);
            Assert.AreEqual("conv-123", firstEvent.conversation_id);
        }

        [Test]
        public void SSEストリーム解析_複数イベント_全イベント返却テスト()
        {
            // Arrange
            string sseData = 
                "data: {\"event\":\"message\",\"answer\":\"Hello\"}\n\n" +
                "data: {\"event\":\"tts_message\",\"audio\":\"YWJjZA==\"}\n\n" +
                "data: {\"event\":\"message_end\"}\n\n";
            
            // Act
            var events = SSEParser.ParseEvents(sseData);
            var eventList = new System.Collections.Generic.List<DifyStreamEvent>(events);
            
            // Assert
            Assert.AreEqual(3, eventList.Count);
            
            Assert.AreEqual("message", eventList[0].@event);
            Assert.AreEqual("Hello", eventList[0].answer);
            
            Assert.AreEqual("tts_message", eventList[1].@event);
            Assert.AreEqual("YWJjZA==", eventList[1].audio);
            
            Assert.AreEqual("message_end", eventList[2].@event);
        }

        [Test]
        public void SSEストリーム解析_コメント付き_コメント無視テスト()
        {
            // Arrange
            string sseData = 
                ": This is a comment\n" +
                "data: {\"event\":\"message\",\"answer\":\"Hello\"}\n" +
                ": Another comment\n\n";
            
            // Act
            var events = SSEParser.ParseEvents(sseData);
            var eventList = new System.Collections.Generic.List<DifyStreamEvent>(events);
            
            // Assert
            Assert.AreEqual(1, eventList.Count);
            Assert.AreEqual("message", eventList[0].@event);
        }

        #endregion

        #region ParseSingleEvent Tests

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void 単一イベント解析_無効入力_null返却テスト(string input)
        {
            // Act
            var result = SSEParser.ParseSingleLine(input);
            
            // Assert
            Assert.IsTrue(result.IsSkipped || result.IsParseError);
        }

        [Test]
        public void 単一イベント解析_有効データライン_イベント返却テスト()
        {
            // Arrange
            string eventLine = "data: {\"event\":\"message\",\"answer\":\"Test Response\",\"message_id\":\"msg-456\"}";
            
            // Act
            var result = SSEParser.ParseSingleLine(eventLine);
            
            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Event);
            Assert.AreEqual("message", result.Event.@event);
            Assert.AreEqual("Test Response", result.Event.answer);
            Assert.AreEqual("msg-456", result.Event.message_id);
        }

        [Test]
        public void 単一イベント解析_音声イベント_音声データ返却テスト()
        {
            // Arrange
            string eventLine = "data: {\"event\":\"tts_message\",\"audio\":\"SGVsbG8gV29ybGQ=\",\"conversation_id\":\"conv-789\"}";
            
            // Act
            var result = SSEParser.ParseSingleLine(eventLine);
            
            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Event);
            Assert.AreEqual("tts_message", result.Event.@event);
            Assert.AreEqual("SGVsbG8gV29ybGQ=", result.Event.audio);
            Assert.AreEqual("conv-789", result.Event.conversation_id);
        }

        [Test]
        public void 単一イベント解析_完了メッセージ_null返却テスト()
        {
            // Arrange
            string eventLine = "data: [DONE]";
            
            // Act
            var result = SSEParser.ParseSingleLine(eventLine);
            
            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsEndMarker);
        }

        #endregion

        #region IsStreamEnd Tests

        [TestCase("")]
        [TestCase(null)]
        [TestCase("data: {\"event\":\"message\"}")]
        public void ストリーム終了判定_非終了イベント_false返却テスト(string input)
        {
            // Act
            var result = SSEParser.ParseSingleLine(input ?? "");
            
            // Assert
            Assert.IsFalse(result?.IsEndMarker ?? false);
        }

        [TestCase("data: [DONE]")]
        [TestCase("data: {\"event\":\"message_end\"}")]
        [TestCase("event: message_end")]
        public void ストリーム終了判定_終了イベント_true返却テスト(string input)
        {
            // Act
            var result = SSEParser.ParseSingleLine(input);
            
            // Assert
            Assert.IsTrue(result.IsEndMarker || (result.Event?.IsMessageEnd ?? false));
        }

        #endregion

        #region IsValidStreamEvent Tests

        [Test]
        public void 有効ストリームイベント判定_nullイベント_false返却テスト()
        {
            // Arrange
            DifyStreamEvent nullEvent = null;
            
            // Act
            var result = nullEvent?.HasValidData ?? false;
            
            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void 有効ストリームイベント判定_空イベント_false返却テスト()
        {
            // Arrange
            var emptyEvent = new DifyStreamEvent();
            
            // Act
            var result = emptyEvent.HasValidData;
            
            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void 有効ストリームイベント判定_有効メッセージイベント_true返却テスト()
        {
            // Arrange
            var validEvent = new DifyStreamEvent
            {
                @event = "message",
                answer = "Test response"
            };
            
            // Act
            var result = validEvent.HasValidData;
            
            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void 有効ストリームイベント判定_有効音声イベント_true返却テスト()
        {
            // Arrange
            var validEvent = new DifyStreamEvent
            {
                @event = "tts_message",
                audio = "base64audiodata"
            };
            
            // Act
            var result = validEvent.HasValidData;
            
            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void SSEストリーム解析_不正なJSON_適切な処理テスト()
        {
            // Arrange
            string sseData = "data: {invalid json here}\n\n";
            
            // Act
            var events = SSEParser.ParseEvents(sseData);
            var eventList = new System.Collections.Generic.List<DifyStreamEvent>(events);
            
            // Assert
            Assert.AreEqual(0, eventList.Count); // パースできたイベントは0個
        }

        #endregion
    }
}