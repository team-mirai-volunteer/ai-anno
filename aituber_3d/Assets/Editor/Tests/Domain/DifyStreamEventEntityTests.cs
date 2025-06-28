using NUnit.Framework;
using System;
using AiTuber.Services.Dify.Domain.Entities;

namespace AiTuber.Tests.Dify.Domain
{
    /// <summary>
    /// DifyStreamEvent エンティティのユニットテスト
    /// Pure C# Domain Entity、Clean Architecture準拠
    /// TDD Red-Green-Refactor実装
    /// </summary>
    [TestFixture]
    public class DifyStreamEventEntityTests
    {
        #region Constructor Tests

        [Test]
        public void DifyStreamEvent作成_メッセージイベント_正常にインスタンス作成()
        {
            // Arrange
            var eventType = "message";
            var answer = "こんにちは";
            var conversationId = "conv-123";
            var messageId = "msg-456";

            // Act
            var streamEvent = DifyStreamEvent.CreateMessageEvent(
                answer, conversationId, messageId);

            // Assert
            Assert.AreEqual(eventType, streamEvent.EventType);
            Assert.AreEqual(answer, streamEvent.Answer);
            Assert.AreEqual(conversationId, streamEvent.ConversationId);
            Assert.AreEqual(messageId, streamEvent.MessageId);
        }

        [Test]
        public void DifyStreamEvent作成_音声イベント_正常にインスタンス作成()
        {
            // Arrange
            var audioData = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
            var conversationId = "conv-123";

            // Act
            var streamEvent = DifyStreamEvent.CreateAudioEvent(audioData, conversationId);

            // Assert
            Assert.AreEqual("tts_message", streamEvent.EventType);
            Assert.AreEqual(audioData, streamEvent.Audio);
            Assert.AreEqual(conversationId, streamEvent.ConversationId);
        }

        [Test]
        public void DifyStreamEvent作成_終了イベント_正常にインスタンス作成()
        {
            // Arrange
            var conversationId = "conv-123";
            var messageId = "msg-456";

            // Act
            var streamEvent = DifyStreamEvent.CreateEndEvent(conversationId, messageId);

            // Assert
            Assert.AreEqual("message_end", streamEvent.EventType);
            Assert.AreEqual(conversationId, streamEvent.ConversationId);
            Assert.AreEqual(messageId, streamEvent.MessageId);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void CreateMessageEvent_無効なAnswer_ArgumentException(string invalidAnswer)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DifyStreamEvent.CreateMessageEvent(invalidAnswer, "conv-123", "msg-456"));
        }

        #endregion

        #region Property Tests

        [Test]
        public void IsMessageEvent_メッセージイベント_Trueを返す()
        {
            // Arrange
            var streamEvent = DifyStreamEvent.CreateMessageEvent("テスト", "conv-123", "msg-456");

            // Act & Assert
            Assert.IsTrue(streamEvent.IsMessageEvent);
            Assert.IsFalse(streamEvent.IsAudioEvent);
            Assert.IsFalse(streamEvent.IsEndEvent);
        }

        [Test]
        public void IsAudioEvent_音声イベント_Trueを返す()
        {
            // Arrange
            var audioData = Convert.ToBase64String(new byte[] { 1, 2, 3 });
            var streamEvent = DifyStreamEvent.CreateAudioEvent(audioData, "conv-123");

            // Act & Assert
            Assert.IsTrue(streamEvent.IsAudioEvent);
            Assert.IsFalse(streamEvent.IsMessageEvent);
            Assert.IsFalse(streamEvent.IsEndEvent);
        }

        [Test]
        public void IsEndEvent_終了イベント_Trueを返す()
        {
            // Arrange
            var streamEvent = DifyStreamEvent.CreateEndEvent("conv-123", "msg-456");

            // Act & Assert
            Assert.IsTrue(streamEvent.IsEndEvent);
            Assert.IsFalse(streamEvent.IsMessageEvent);
            Assert.IsFalse(streamEvent.IsAudioEvent);
        }

        [Test]
        public void HasValidAudio_有効な音声データ_Trueを返す()
        {
            // Arrange
            var audioData = Convert.ToBase64String(new byte[] { 0xFF, 0xF3, 0x01 }); // MP3 header
            var streamEvent = DifyStreamEvent.CreateAudioEvent(audioData, "conv-123");

            // Act & Assert
            Assert.IsTrue(streamEvent.HasValidAudio);
        }

        [Test]
        public void HasValidAudio_無効な音声データ_Falseを返す()
        {
            // Arrange
            var streamEvent = DifyStreamEvent.CreateMessageEvent("テスト", "conv-123", "msg-456");

            // Act & Assert
            Assert.IsFalse(streamEvent.HasValidAudio);
        }

        #endregion

        #region Validation Tests

        [Test]
        public void IsValid_有効なメッセージイベント_Trueを返す()
        {
            // Arrange
            var streamEvent = DifyStreamEvent.CreateMessageEvent("テスト", "conv-123", "msg-456");

            // Act
            var result = streamEvent.IsValid();

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsValid_有効な音声イベント_Trueを返す()
        {
            // Arrange
            var audioData = Convert.ToBase64String(new byte[] { 1, 2, 3 });
            var streamEvent = DifyStreamEvent.CreateAudioEvent(audioData, "conv-123");

            // Act
            var result = streamEvent.IsValid();

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Equality Tests

        [Test]
        public void Equals_同一内容のイベント_Trueを返す()
        {
            // Arrange
            var event1 = DifyStreamEvent.CreateMessageEvent("テスト", "conv-123", "msg-456");
            var event2 = DifyStreamEvent.CreateMessageEvent("テスト", "conv-123", "msg-456");

            // Act & Assert
            Assert.AreEqual(event1, event2);
            Assert.AreEqual(event1.GetHashCode(), event2.GetHashCode());
        }

        [Test]
        public void Equals_異なる内容のイベント_Falseを返す()
        {
            // Arrange
            var event1 = DifyStreamEvent.CreateMessageEvent("テスト1", "conv-123", "msg-456");
            var event2 = DifyStreamEvent.CreateMessageEvent("テスト2", "conv-123", "msg-456");

            // Act & Assert
            Assert.AreNotEqual(event1, event2);
        }

        #endregion
    }
}