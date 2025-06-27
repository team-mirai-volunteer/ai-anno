using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Application.Ports;
using AiTuber.Services.Dify.Domain.Entities;

namespace AiTuber.Tests.Dify.Application
{
    /// <summary>
    /// ProcessQueryUseCase のユニットテスト
    /// Pure C# Application Layer、Clean Architecture準拠
    /// TDD Red-Green-Refactor実装、Mock使用
    /// </summary>
    [TestFixture]
    public class ProcessQueryUseCaseTests
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

        #region ExecuteAsync Tests

        [Test]
        public async Task クエリ実行_有効なリクエスト_成功レスポンスを返す()
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
            var result = await _useCase.ExecuteAsync(request, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("こんにちは！", result.TextResponse);
            Assert.AreEqual("conv-123", result.ConversationId);
            Assert.AreEqual("msg-456", result.MessageId);
            Assert.AreEqual(1, _mockStreamingPort.CallCount);
        }

        [Test]
        public async Task クエリ実行_ストリーミング成功_イベント通知される()
        {
            // Arrange
            var request = new DifyRequest("テスト質問", "test-user");
            var streamEvents = new[]
            {
                DifyStreamEvent.CreateMessageEvent("部分", "conv-123", "msg-456"),
                DifyStreamEvent.CreateMessageEvent("応答", "conv-123", "msg-456"),
                DifyStreamEvent.CreateEndEvent("conv-123", "msg-456")
            };

            _mockStreamingPort.SetupStreamingEvents(streamEvents);

            var receivedEvents = new System.Collections.Generic.List<DifyStreamEvent>();

            // Act
            var result = await _useCase.ExecuteAsync(
                request, 
                onEventReceived: evt => receivedEvents.Add(evt),
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, receivedEvents.Count);
            Assert.IsTrue(receivedEvents[0].IsMessageEvent);
            Assert.IsTrue(receivedEvents[2].IsEndEvent);
        }

        [Test]
        public async Task クエリ実行_音声データ含む_音声処理される()
        {
            // Arrange
            var request = new DifyRequest("音声テスト", "test-user");
            var audioData = Convert.ToBase64String(new byte[] { 0xFF, 0xF3, 0x01 }); // MP3 header
            var audioEvent = DifyStreamEvent.CreateAudioEvent(audioData, "conv-123");

            _mockStreamingPort.SetupStreamingEvents(new[] { audioEvent });

            // Act
            var result = await _useCase.ExecuteAsync(request, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasAudioData);
            Assert.AreEqual(1, _mockResponseProcessor.AudioProcessCallCount);
        }

        [Test]
        public async Task クエリ実行_NullRequest_ArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _useCase.ExecuteAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task クエリ実行_無効なRequest_ArgumentException()
        {
            // Arrange
            var invalidRequest = new DifyRequest("", "test-user"); // 空のクエリ

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _useCase.ExecuteAsync(invalidRequest, CancellationToken.None));
        }

        [Test]
        public async Task クエリ実行_ストリーミングエラー_失敗レスポンスを返す()
        {
            // Arrange
            var request = new DifyRequest("エラーテスト", "test-user");
            _mockStreamingPort.SetupError("Connection failed");

            // Act
            var result = await _useCase.ExecuteAsync(request, CancellationToken.None);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Connection failed", result.ErrorMessage);
            Assert.AreEqual(0, result.ProcessingTimeMs);
        }

        [Test]
        public async Task クエリ実行_キャンセルトークン_OperationCancelledException()
        {
            // Arrange
            var request = new DifyRequest("キャンセルテスト", "test-user");
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _useCase.ExecuteAsync(request, cts.Token));
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
            // Arrange
            var invalidRequest = new DifyRequest("", "user");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _useCase.ValidateRequest(invalidRequest));
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
        public DifyStreamEvent[] StreamingEvents { get; set; } = Array.Empty<DifyStreamEvent>();

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
            Action<DifyStreamEvent> onEventReceived = null, 
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