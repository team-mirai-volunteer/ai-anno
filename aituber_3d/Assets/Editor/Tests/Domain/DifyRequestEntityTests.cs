using NUnit.Framework;
using System;
using AiTuber.Services.Dify.Domain.Entities;

namespace AiTuber.Tests.Dify.Domain
{
    /// <summary>
    /// DifyRequest エンティティのユニットテスト
    /// Pure C# Domain Entity、Clean Architecture準拠
    /// TDD Red-Green-Refactor実装
    /// </summary>
    [TestFixture]
    public class DifyRequestEntityTests
    {
        #region Constructor Tests

        [Test]
        public void DifyRequest作成_有効なパラメータ_正常にインスタンス作成()
        {
            // Arrange
            var query = "こんにちは";
            var user = "test-user";

            // Act
            var request = new DifyRequest(query, user);

            // Assert
            Assert.AreEqual(query, request.Query);
            Assert.AreEqual(user, request.User);
            Assert.AreEqual("streaming", request.ResponseMode);
            Assert.IsEmpty(request.ConversationId);
        }

        [Test]
        public void DifyRequest作成_会話ID指定_正常にインスタンス作成()
        {
            // Arrange
            var query = "続きの質問";
            var user = "test-user";
            var conversationId = "conv-123";

            // Act
            var request = new DifyRequest(query, user, conversationId);

            // Assert
            Assert.AreEqual(conversationId, request.ConversationId);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void DifyRequest作成_無効なクエリ_ArgumentException(string invalidQuery)
        {
            // Arrange
            var user = "test-user";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new DifyRequest(invalidQuery, user));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void DifyRequest作成_無効なユーザー_ArgumentException(string invalidUser)
        {
            // Arrange
            var query = "テスト質問";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new DifyRequest(query, invalidUser));
        }

        #endregion

        #region Validation Tests

        [Test]
        public void IsValid_有効なリクエスト_Trueを返す()
        {
            // Arrange
            var request = new DifyRequest("テスト質問", "test-user");

            // Act
            var result = request.IsValid();

            // Assert
            Assert.IsTrue(result);
        }
        #endregion

        #region Equality Tests

        [Test]
        public void Equals_同一内容のリクエスト_Trueを返す()
        {
            // Arrange
            var request1 = new DifyRequest("質問", "user1", "conv-1");
            var request2 = new DifyRequest("質問", "user1", "conv-1");

            // Act & Assert
            Assert.AreEqual(request1, request2);
            Assert.AreEqual(request1.GetHashCode(), request2.GetHashCode());
        }

        [Test]
        public void Equals_異なる内容のリクエスト_Falseを返す()
        {
            // Arrange
            var request1 = new DifyRequest("質問1", "user1", "conv-1");
            var request2 = new DifyRequest("質問2", "user1", "conv-1");

            // Act & Assert
            Assert.AreNotEqual(request1, request2);
        }

        #endregion
    }
}