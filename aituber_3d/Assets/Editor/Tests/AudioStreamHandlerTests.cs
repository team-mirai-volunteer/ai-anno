using NUnit.Framework;
using System;
using System.Collections.Generic;
using AiTuber.Services.Dify.Audio;

namespace AiTuber.Tests.Dify.Audio
{
    /// <summary>
    /// AudioStreamHandler クラスのユニットテスト
    /// 同期的Pure C#メソッドのみテスト、Unity Test Runner安全な実装
    /// </summary>
    [TestFixture]
    public class AudioStreamHandlerTests
    {

        #region IsValidBase64Audio Tests

        [Test]
        public void 有効なBase64Audio検証_正常形式_Trueを返す()
        {
            // Arrange
            string validBase64 = "SGVsbG8gV29ybGQ="; // "Hello World" in base64
            
            // Act
            var result = AudioStreamHandler.IsValidBase64Audio(validBase64);
            
            // Assert
            Assert.IsTrue(result);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void 有効なBase64Audio検証_無効な入力_Falseを返す(string input)
        {
            // Act
            var result = AudioStreamHandler.IsValidBase64Audio(input);
            
            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void 有効なBase64Audio検証_無効な長さ_Falseを返す()
        {
            // Arrange - Base64 length must be multiple of 4
            string invalidLength = "SGVsbG8"; // Length 7, not multiple of 4
            
            // Act
            var result = AudioStreamHandler.IsValidBase64Audio(invalidLength);
            
            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region DecodeBase64Audio Tests

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void Base64Audio復号化_無効な入力_失敗結果を返す(string input)
        {
            // Act
            var result = AudioStreamHandler.DecodeBase64Audio(input);
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.HasValidAudioData);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void Base64Audio復号化_有効なBase64_成功結果を返す()
        {
            // Arrange
            string validBase64 = "SGVsbG8gV29ybGQ="; // "Hello World" in base64
            
            // Act
            var result = AudioStreamHandler.DecodeBase64Audio(validBase64);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasValidAudioData);
            Assert.IsNotNull(result.AudioData);
            Assert.Greater(result.AudioData.Length, 0);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Base64Audio復号化_無効なBase64形式_失敗結果を返す()
        {
            // Arrange
            string invalidBase64 = "NotValidBase64!!!"; // Invalid characters
            
            // Act
            var result = AudioStreamHandler.DecodeBase64Audio(invalidBase64);
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.HasValidAudioData);
            Assert.IsNotNull(result.ErrorMessage);
        }

        #endregion

        #region ConcatenateAudioChunks Tests

        [Test]
        public void オーディオチャンク結合_Null入力_失敗結果を返す()
        {
            // Act
            var result = AudioStreamHandler.ConcatenateAudioChunks(null);
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void オーディオチャンク結合_空リスト_失敗結果を返す()
        {
            // Arrange
            var emptyList = new List<byte[]>();
            
            // Act
            var result = AudioStreamHandler.ConcatenateAudioChunks(emptyList);
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void オーディオチャンク結合_有効なチャンク_成功結果を返す()
        {
            // Arrange
            var chunks = new List<byte[]>
            {
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5 },
                new byte[] { 6, 7, 8, 9 }
            };
            
            // Act
            var result = AudioStreamHandler.ConcatenateAudioChunks(chunks);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasValidAudioData);
            Assert.AreEqual(9, result.DataSizeBytes);
            
            // Check concatenated data
            var expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            CollectionAssert.AreEqual(expected, result.AudioData);
        }

        [Test]
        public void オーディオチャンク結合_混在チャンク_成功結果を返す()
        {
            // Arrange
            var chunks = new List<byte[]>
            {
                new byte[] { 1, 2 },
                null,
                new byte[] { 3, 4 },
                new byte[0], // Empty array
                new byte[] { 5 }
            };
            
            // Act
            var result = AudioStreamHandler.ConcatenateAudioChunks(chunks);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(5, result.DataSizeBytes);
            
            // Check concatenated data (nulls and empty arrays should be skipped)
            var expected = new byte[] { 1, 2, 3, 4, 5 };
            CollectionAssert.AreEqual(expected, result.AudioData);
        }

        #endregion

        #region GetAudioChunkStatistics Tests

        [Test]
        public void オーディオチャンク統計取得_Null入力_空統計を返す()
        {
            // Act
            var stats = AudioStreamHandler.GetAudioChunkStatistics(null);
            
            // Assert
            Assert.AreEqual(0, stats.TotalChunks);
            Assert.AreEqual(0, stats.ValidChunks);
            Assert.IsFalse(stats.HasValidData);
        }

        [Test]
        public void オーディオチャンク統計取得_有効なチャンク_正確な統計を返す()
        {
            // Arrange
            var chunks = new List<byte[]>
            {
                new byte[] { 1, 2, 3, 4 }, // 4 bytes
                new byte[] { 5, 6 },       // 2 bytes
                null,                       // null
                new byte[] { 7, 8, 9 },    // 3 bytes
                new byte[0]                 // empty
            };
            
            // Act
            var stats = AudioStreamHandler.GetAudioChunkStatistics(chunks);
            
            // Assert
            Assert.AreEqual(5, stats.TotalChunks);
            Assert.AreEqual(3, stats.ValidChunks);
            Assert.AreEqual(2, stats.NullOrEmptyChunks);
            Assert.AreEqual(9, stats.TotalBytes);
            Assert.AreEqual(3.0, stats.AverageChunkBytes);
            Assert.AreEqual(4, stats.LargestChunkBytes);
            Assert.AreEqual(2, stats.SmallestChunkBytes);
            Assert.IsTrue(stats.HasValidData);
        }

        #endregion

        #region AudioProcessingResult Tests

        [Test]
        public void オーディオ処理結果_プロパティ_正常に動作する()
        {
            // Arrange & Act
            var result = new AudioStreamHandler.AudioProcessingResult
            {
                IsSuccess = true,
                AudioData = new byte[] { 1, 2, 3, 4, 5 },
                ErrorMessage = null
            };
            // Assert
            Assert.AreEqual(5, result.DataSizeBytes);
            Assert.IsTrue(result.HasValidAudioData);
        }

        [Test]
        public void オーディオ処理結果_Nullオーディオデータ_ゼロサイズを返す()
        {
            // Arrange & Act
            var result = new AudioStreamHandler.AudioProcessingResult
            {
                IsSuccess = true,
                AudioData = null,
                ErrorMessage = null
            };
            // Assert
            Assert.AreEqual(0, result.DataSizeBytes);
            Assert.IsFalse(result.HasValidAudioData);
        }

        #endregion
    }
}
