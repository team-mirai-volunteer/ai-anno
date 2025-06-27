using NUnit.Framework;
using AiTuber.Services.Legacy.Dify.Data;
using System;

namespace AiTuber.Tests.Legacy.Dify
{
    /// <summary>
    /// Dify データクラスのユニットテスト
    /// TDD実装、Pure C#なのでエディタプレイ不要
    /// </summary>
    [TestFixture]
    public class DifyDataTests
    {
        #region DifyApiRequest Tests
        
        [Test]
        public void APIリクエスト作成_デフォルト値_正常設定テスト()
        {
            // Arrange & Act
            var request = new DifyApiRequest();
            
            // Assert
            Assert.AreEqual("streaming", request.response_mode);
            Assert.AreEqual("", request.conversation_id);
            Assert.IsNotNull(request.inputs);
            Assert.IsNotNull(request.files);
            Assert.AreEqual(0, request.files.Length);
        }
        
        [Test]
        public void APIリクエスト検証_有効データ_成功結果テスト()
        {
            // Arrange
            var request = new DifyApiRequest 
            { 
                query = "こんにちは", 
                user = "test-user" 
            };
            
            // Act & Assert
            var result = request.IsValid();
            Assert.IsTrue(result);
        }
        
        [TestCase("", "user")]
        [TestCase("query", "")]
        [TestCase("", "")]
        [TestCase(null, "user")]
        [TestCase("query", null)]
        public void APIリクエスト検証_必須フィールド不足_失敗結果テスト(string query, string user)
        {
            // Arrange
            var request = new DifyApiRequest { query = query, user = user };
            
            // Act & Assert
            var result = request.IsValid();
            Assert.IsFalse(result);
        }
        
        [TestCase("streaming")]
        public void APIリクエスト検証_有効レスポンスモード_成功結果テスト(string responseMode)
        {
            // Arrange
            var request = new DifyApiRequest 
            { 
                query = "test", 
                user = "test-user",
                response_mode = responseMode
            };
            
            // Act & Assert
            var result = request.IsValid();
            Assert.IsTrue(result);
        }
        
        [Test]
        public void APIリクエスト検証_無効レスポンスモード_失敗結果テスト()
        {
            // Arrange
            var request = new DifyApiRequest 
            { 
                query = "test", 
                user = "test-user",
                response_mode = "invalid"
            };
            
            // Act & Assert
            var result = request.IsValid();
            Assert.IsFalse(result);
        }
        
        
        [Test]
        public void APIリクエスト判定_新規会話_真値結果テスト()
        {
            // Arrange
            var request = new DifyApiRequest { conversation_id = "" };
            
            // Act & Assert
            var result = request.IsNewConversation;
            Assert.IsTrue(result);
        }
        
        [Test]
        public void APIリクエスト判定_既存会話_偽値結果テスト()
        {
            // Arrange
            var request = new DifyApiRequest { conversation_id = "existing-id" };
            
            // Act & Assert
            var result = request.IsNewConversation;
            Assert.IsFalse(result);
        }
        
        #endregion
        
        #region DifyStreamEvent Tests
        
        [Test]
        public void ストリームイベント判定_テキストメッセージ_真値結果テスト()
        {
            // Arrange
            var streamEvent = new DifyStreamEvent { @event = "message" };
            
            // Act & Assert
            var result = streamEvent.IsTextMessage;
            Assert.IsTrue(result);
        }
        
        [Test]
        public void ストリームイベント判定_TTSメッセージ_真値結果テスト()
        {
            // Arrange
            var streamEvent = new DifyStreamEvent { @event = "tts_message" };
            
            // Act & Assert
            var result = streamEvent.IsTTSMessage;
            Assert.IsTrue(result);
        }
        
        [Test]
        public void ストリームイベント判定_メッセージ終了_真値結果テスト()
        {
            // Arrange
            var streamEvent = new DifyStreamEvent { @event = "message_end" };
            
            // Act & Assert
            var result = streamEvent.IsMessageEnd;
            Assert.IsTrue(result);
        }
        
        [Test]
        public void ストリームイベント判定_有効テキスト_真値結果テスト()
        {
            // Arrange
            var streamEvent = new DifyStreamEvent 
            { 
                @event = "message",
                answer = "こんにちは"
            };
            
            // Act & Assert
            var result = streamEvent.HasValidTextMessage;
            Assert.IsTrue(result);
        }
        
        [Test]
        public void ストリームイベント判定_空回答_偽値結果テスト()
        {
            // Arrange
            var streamEvent = new DifyStreamEvent 
            { 
                @event = "message",
                answer = ""
            };
            
            // Act & Assert
            var result = streamEvent.HasValidTextMessage;
            Assert.IsFalse(result);
        }
        
        [Test]
        public void ストリームイベント判定_有効音声データ_真値結果テスト()
        {
            // Arrange
            var streamEvent = new DifyStreamEvent 
            { 
                @event = "tts_message",
                audio = "base64audiodata"
            };
            
            // Act & Assert
            var result = streamEvent.HasValidAudioData;
            Assert.IsTrue(result);
        }
        
        [Test]
        public void ストリームイベント変換_タイムスタンプ_日時結果テスト()
        {
            // Arrange
            var timestamp = 1609459200; // 2021-01-01 00:00:00 UTC
            var streamEvent = new DifyStreamEvent { created_at = timestamp };
            var expected = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            // Act & Assert
            var result = streamEvent.CreatedDateTime;
            Assert.AreEqual(expected, result);
        }
        
        #endregion
        
        #region DifyProcessingResult Tests
        
        [Test]
        public void 処理結果作成_デフォルト値_正常設定テスト()
        {
            // Arrange & Act
            var result = new DifyProcessingResult();
            
            // Assert
            Assert.IsNotNull(result.AudioChunks);
            Assert.AreEqual(0, result.AudioChunkCount);
            Assert.AreEqual(0, result.TotalAudioBytes);
            Assert.IsFalse(result.HasTextResponse);
            Assert.IsFalse(result.HasAudioData);
        }
        
        [Test]
        public void 処理結果判定_テキスト有り_真値結果テスト()
        {
            // Arrange
            var result = new DifyProcessingResult { TextResponse = "Hello" };
            
            // Act & Assert
            var hasTextResponse = result.HasTextResponse;
            Assert.IsTrue(hasTextResponse);
        }
        
        [Test]
        public void 処理結果判定_音声データ有り_真値結果テスト()
        {
            // Arrange
            var result = new DifyProcessingResult();
            result.AudioChunks.Add(new byte[] { 1, 2, 3 });
            
            // Act & Assert
            var hasAudioData = result.HasAudioData;
            var audioChunkCount = result.AudioChunkCount;
            Assert.IsTrue(hasAudioData);
            Assert.AreEqual(1, audioChunkCount);
        }
        
        [Test]
        public void 処理結果計算_総音声バイト数_正確数値テスト()
        {
            // Arrange
            var result = new DifyProcessingResult();
            result.AudioChunks.Add(new byte[] { 1, 2, 3 }); // 3 bytes
            result.AudioChunks.Add(new byte[] { 4, 5 });    // 2 bytes
            
            // Act & Assert
            var totalBytes = result.TotalAudioBytes;
            Assert.AreEqual(5, totalBytes);
        }
        
        [Test]
        public void 処理結果計算_nullチャンク含む_正確数値テスト()
        {
            // Arrange
            var result = new DifyProcessingResult();
            result.AudioChunks.Add(new byte[] { 1, 2, 3 });
            result.AudioChunks.Add(null);
            
            // Act & Assert
            var totalBytes = result.TotalAudioBytes;
            Assert.AreEqual(3, totalBytes);
        }
        
        #endregion
    }
}