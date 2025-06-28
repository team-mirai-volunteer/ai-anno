using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using AiTuber.Services.Dify.Presentation.Controllers;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Domain.Entities;
using AiTuber.Services.Dify.Infrastructure.Http;

#nullable enable

namespace AiTuber.Tests.Dify.Presentation
{
    /// <summary>
    /// DifyController のユニットテスト
    /// Presentation層 Clean Architecture準拠
    /// DifyEditorWindow接続用コントローラー
    /// </summary>
    [TestFixture]
    public class DifyControllerTests
    {
        private DifyController _controller;
        private MockProcessQueryUseCase _mockUseCase;

        [SetUp]
        public void SetUp()
        {
            _mockUseCase = new MockProcessQueryUseCase();
            _controller = new DifyController(_mockUseCase);
        }

        #region Constructor Tests

        // 削除: 偽テスト - コンストラクタの正常動作は他テストで十分検証される

        [Test]
        public void DifyController作成_nullUseCase_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DifyController(null));
        }

        #endregion

        #region SendQuery Tests

        [UnityTest]
        public IEnumerator SendQuery_有効なクエリ_成功レスポンス()
        {
            // Arrange
            var query = "こんにちは";
            var user = "test-user";
            _mockUseCase.SetupSuccessResponse("こんにちは！", "conv-123", "msg-456");

            // Act
            var task = _controller.SendQueryAsync(query, user);
            yield return WaitForTask(task);
            var response = task.Result;

            // Assert
            Assert.IsTrue(response.IsSuccess);
            Assert.AreEqual("こんにちは！", response.TextResponse);
            Assert.AreEqual("conv-123", response.ConversationId);
        }

        [UnityTest]
        public IEnumerator SendQuery_空のクエリ_エラーレスポンス()
        {
            // Arrange
            var query = "";
            var user = "test-user";

            // Act
            var task = _controller.SendQueryAsync(query, user);
            yield return WaitForTask(task);
            var response = task.Result;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsFalse(response.IsSuccess);
            Assert.IsNotNull(response.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator SendQuery_nullユーザー_デフォルトユーザー使用()
        {
            // Arrange
            var query = "テストクエリ";
            string? user = null;
            _mockUseCase.SetupSuccessResponse("レスポンス", "conv-123", "msg-456");

            // Act
            var task = _controller.SendQueryAsync(query, user);
            yield return WaitForTask(task);
            var response = task.Result;

            // Assert
            Assert.IsTrue(response.IsSuccess);
            Assert.AreEqual("default-user", _mockUseCase.LastRequest?.User);
        }

        #endregion

        #region Streaming Tests

        [UnityTest]
        public IEnumerator SendQueryStreaming_イベントコールバック_正しく通知()
        {
            // Arrange
            var query = "ストリーミングテスト";
            var user = "test-user";
            var receivedEvents = new List<DifyStreamEvent>();
            
            _mockUseCase.SetupStreamingEvents(new[]
            {
                DifyStreamEvent.CreateMessageEvent("テスト", "conv-123", "msg-456"),
                DifyStreamEvent.CreateEndEvent("conv-123", "msg-456")
            });

            // Act
            var task = _controller.SendQueryStreamingAsync(
                query, 
                user,
                evt => receivedEvents.Add(evt)
            );
            yield return WaitForTask(task);

            // Assert
            Assert.AreEqual(2, receivedEvents.Count);
            Assert.IsTrue(receivedEvents[0].IsMessageEvent);
            Assert.IsTrue(receivedEvents[1].IsEndEvent);
        }

        [UnityTest]
        public IEnumerator SendQueryStreaming_キャンセル_正しく処理()
        {
            // Arrange
            var query = "キャンセルテスト";
            var user = "test-user";
            var cts = new CancellationTokenSource();
            _mockUseCase.SetupCancellation();

            // Act
            cts.CancelAfter(10); // より早くキャンセル
            var task = _controller.SendQueryStreamingAsync(query, user, null, cts.Token);
            
            // Wait with timeout
            var startTime = UnityEngine.Time.time;
            while (!task.IsCompleted && UnityEngine.Time.time - startTime < 1.0f)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.IsCompleted, "Task should complete within timeout");
            if (task.IsCanceled)
            {
                Assert.IsTrue(true, "Task was canceled as expected");
            }
            else if (task.IsFaulted && task.Exception?.InnerException is OperationCanceledException)
            {
                Assert.IsTrue(true, "Task faulted with OperationCanceledException as expected");
            }
            else
            {
                Assert.IsTrue(!task.Result.IsSuccess, "Task should not be successful when canceled");
            }
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void GetConfiguration_設定情報を返す()
        {
            // Act
            var config = _controller.GetConfiguration();

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("test-api-key", config.ApiKey);
            Assert.AreEqual("https://api.dify.ai/test", config.ApiUrl);
        }

        [UnityTest]
        public IEnumerator TestConnection_接続成功_Trueを返す()
        {
            // Arrange
            _mockUseCase.SetupConnectionSuccess();

            // Act
            var task = _controller.TestConnectionAsync();
            yield return WaitForTask(task);
            var result = task.Result;

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Helper Methods

        private IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ?? new Exception("Task failed");
            }
        }

        private IEnumerator WaitForTask<T>(Task<T> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ?? new Exception("Task failed");
            }
        }

        #endregion
    }

    #region Mock Classes

    /// <summary>
    /// ProcessQueryUseCase のモック実装
    /// </summary>
    public class MockProcessQueryUseCase : IProcessQueryUseCase
    {
        private QueryResponse _response = QueryResponse.CreateError("Not configured", 0);
        private DifyStreamEvent[]? _streamingEvents;
        private bool _shouldCancel = false;
        private bool _connectionSuccess = true;

        public DifyRequest? LastRequest { get; private set; }

        public void SetupSuccessResponse(string text, string conversationId, string messageId)
        {
            _response = QueryResponse.CreateSuccess(text, conversationId, messageId, 100);
        }

        public void SetupErrorResponse(string error)
        {
            _response = QueryResponse.CreateError(error, 0);
        }

        public void SetupStreamingEvents(DifyStreamEvent[] events)
        {
            _streamingEvents = events;
        }

        public void SetupCancellation()
        {
            _shouldCancel = true;
        }

        public void SetupConnectionSuccess()
        {
            _connectionSuccess = true;
        }

        public void SetupConnectionFailure()
        {
            _connectionSuccess = false;
        }

        public async Task<QueryResponse> ExecuteAsync(
            DifyRequest request,
            Action<DifyStreamEvent>? onEventReceived = null,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            if (_shouldCancel)
            {
                await Task.Delay(50, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_streamingEvents != null && onEventReceived != null)
            {
                foreach (var evt in _streamingEvents)
                {
                    await Task.Delay(10, cancellationToken);
                    onEventReceived(evt);
                }
            }

            return _response;
        }

        public DifyConfiguration GetConfiguration()
        {
            return new DifyConfiguration("test-api-key", "https://api.dify.ai/test", true);
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return _connectionSuccess;
        }
    }

    #endregion
}