using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Application.Ports;
using AiTuber.Services.Dify.Domain.Entities;

namespace AiTuber.Tests.Dify.Application
{
    /// <summary>
    /// ProcessQueryUseCase のユニットテスト (Unity対応版)
    /// Pure C# Application Layer、Clean Architecture準拠
    /// TDD Red-Green-Refactor実装、Mock使用
    /// </summary>
    [TestFixture]
    public class ProcessQueryUseCaseTestsFixed
    {
        private ProcessQueryUseCase _useCase;
        private MockDifyStreamingPort _mockStreamingPort;
        private MockResponseProcessor _mockResponseProcessor;

        [SetUp]
        public void SetUp()
        {
            _mockStreamingPort = new MockDifyStreamingPort();
            _mockResponseProcessor = new MockResponseProcessor();
            _useCase = new ProcessQueryUseCase(_mockStreamingPort, _mockResponseProcessor);
        }

        #region Constructor Tests

        [Test]
        public void ProcessQueryUseCase作成_有効な依存関係_正常にインスタンス作成()
        {
            // Arrange & Act & Assert
            Assert.IsNotNull(_useCase);
        }

        [Test]
        public void ProcessQueryUseCase作成_NullStreamingPort_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProcessQueryUseCase(null, _mockResponseProcessor));
        }

        [Test]
        public void ProcessQueryUseCase作成_NullResponseProcessor_ArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProcessQueryUseCase(_mockStreamingPort, null));
        }

        #endregion

        #region ExecuteAsync Tests (Unity版)

        [UnityTest]
        public IEnumerator クエリ実行_有効なリクエスト_成功レスポンスを返す()
        {
            // Arrange
            var request = new DifyRequest("こんにちは", "test-user");
            var expectedResponse = new QueryResponse(
                isSuccess: true,
                textResponse: "こんにちは！",
                conversationId: "conv-123",
                messageId: "msg-456",
                processingTimeMs: 500
            );

            _mockStreamingPort.SetupSuccess(expectedResponse);

            // Act
            QueryResponse result = null;
            yield return PerformAsyncOperation(
                () => _useCase.ExecuteAsync(request, cancellationToken: CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("こんにちは！", result.TextResponse);
            Assert.AreEqual("conv-123", result.ConversationId);
            Assert.AreEqual("msg-456", result.MessageId);
            Assert.AreEqual(1, _mockStreamingPort.CallCount);
        }

        [UnityTest]
        public IEnumerator クエリ実行_NullRequest_ArgumentNullException()
        {
            // Act & Assert
            yield return PerformAsyncOperationExpectingException<ArgumentNullException>(
                () => _useCase.ExecuteAsync(null, cancellationToken: CancellationToken.None));
        }

        [UnityTest]
        public IEnumerator クエリ実行_ストリーミングエラー_失敗レスポンスを返す()
        {
            // Arrange
            var request = new DifyRequest("エラーテスト", "test-user");
            _mockStreamingPort.SetupError("Connection failed");

            // Act
            QueryResponse result = null;
            yield return PerformAsyncOperation(
                () => _useCase.ExecuteAsync(request, cancellationToken: CancellationToken.None),
                r => result = r);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Connection failed", result.ErrorMessage);
            Assert.AreEqual(0, result.ProcessingTimeMs);
        }

        #endregion

        #region Validation Tests

        [Test]
        public void ValidateRequest_有効なリクエスト_例外なし()
        {
            // Arrange
            var request = new DifyRequest("テスト", "user");

            // Act & Assert
            Assert.DoesNotThrow(() => _useCase.ValidateRequest(request));
        }

        [Test]
        public void ValidateRequest_無効なリクエスト_ArgumentException()
        {
            // このテストはDifyRequestコンストラクタでの例外を確認
            Assert.Throws<ArgumentException>(() => new DifyRequest("", "user"));
        }

        #endregion

        #region Unity Test Helper Methods

        /// <summary>
        /// 非同期操作をUnityTest用IEnumeratorで実行
        /// </summary>
        private IEnumerator PerformAsyncOperation<T>(System.Func<Task<T>> asyncOperation, System.Action<T> onResult)
        {
            var task = asyncOperation();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ?? new System.Exception("Unknown async error");
            }

            onResult(task.Result);
        }

        /// <summary>
        /// 例外が期待される非同期操作をUnityTest用で実行
        /// </summary>
        private IEnumerator PerformAsyncOperationExpectingException<TException>(System.Func<Task> asyncOperation)
            where TException : System.Exception
        {
            var task = asyncOperation();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var baseException = task.Exception?.GetBaseException();
                if (baseException is TException)
                {
                    // Expected exception
                    yield break;
                }
                throw baseException ?? new System.Exception("Unexpected exception type");
            }

            Assert.Fail($"Expected {typeof(TException).Name} was not thrown");
        }

        #endregion
    }

    #region Mock Classes

    /// <summary>
    /// IDifyStreamingPort のモック実装
    /// </summary>
    public class MockDifyStreamingPort : IDifyStreamingPort
    {
        public int CallCount { get; private set; }
        public bool ShouldThrowError { get; set; }
        public string ErrorMessage { get; set; } = "Mock error";
        public QueryResponse SuccessResponse { get; set; }
        public DifyStreamEvent[] StreamingEvents { get; set; } = System.Array.Empty<DifyStreamEvent>();

        public void SetupSuccess(QueryResponse response)
        {
            ShouldThrowError = false;
            SuccessResponse = response;
        }

        public void SetupError(string errorMessage)
        {
            ShouldThrowError = true;
            ErrorMessage = errorMessage;
        }

        public void SetupStreamingEvents(DifyStreamEvent[] events)
        {
            StreamingEvents = events;
        }

        public async Task<QueryResponse> ExecuteStreamingAsync(
            DifyRequest request, 
            System.Action<DifyStreamEvent> onEventReceived = null, 
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldThrowError)
            {
                return new QueryResponse(
                    isSuccess: false,
                    textResponse: "",
                    conversationId: "",
                    messageId: "",
                    processingTimeMs: 0,
                    errorMessage: ErrorMessage
                );
            }

            // ストリーミングイベントをシミュレート
            await Task.Delay(10, cancellationToken);
            foreach (var evt in StreamingEvents)
            {
                onEventReceived?.Invoke(evt);
            }

            return SuccessResponse ?? new QueryResponse(
                isSuccess: true,
                textResponse: $"Mock response for: {request.Query}",
                conversationId: "mock-conv",
                messageId: "mock-msg",
                processingTimeMs: 100
            );
        }
    }

    /// <summary>
    /// IResponseProcessor のモック実装
    /// </summary>
    public class MockResponseProcessor : IResponseProcessor
    {
        public int AudioProcessCallCount { get; private set; }

        public void ProcessAudioEvent(DifyStreamEvent audioEvent)
        {
            AudioProcessCallCount++;
        }

        public void ProcessTextEvent(DifyStreamEvent textEvent)
        {
            // Mock implementation
        }
    }

    #endregion
}