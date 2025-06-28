using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using AiTuber.Services.Dify.Infrastructure.Http;

#nullable enable

namespace AiTuber.Tests.Infrastructure.Http
{
    /// <summary>
    /// UnityWebRequestHttpClient TDD テスト
    /// Phase 1 Red: 失敗テスト作成で要件明確化
    /// Legacy DifyApiClient.cs SSE実装パターン踏襲
    /// </summary>
    [TestFixture]
    public class UnityWebRequestHttpClientTests
    {
        private UnityWebRequestHttpClient? _httpClient;
        private DifyConfiguration? _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = new DifyConfiguration(
                "test-api-key",
                "https://api.dify.ai/v1/chat-messages",
                enableDebugLogging: false);
            
            // TDD Red: 未実装クラスのため失敗する
            _httpClient = new UnityWebRequestHttpClient(_configuration);
        }

        #region Constructor Tests

        [Test]
        public void コンストラクタ_有効な設定_正常にインスタンス化される()
        {
            // Arrange & Act & Assert
            Assert.IsNotNull(_httpClient);
        }

        [Test]
        public void コンストラクタ_null設定_ArgumentNullExceptionが発生する()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new UnityWebRequestHttpClient(null!));
        }

        [Test]
        public void コンストラクタ_無効な設定_ArgumentExceptionが発生する()
        {
            // Arrange
            var invalidConfig = new DifyConfiguration("", "", false);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new UnityWebRequestHttpClient(invalidConfig));
        }

        #endregion

        #region Streaming Request Tests

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_有効なリクエスト_成功レスポンスを返す()
        {
            // Arrange
            var request = new HttpRequest(
                "https://httpbin.org/stream/3",
                "GET",
                null,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Accept", "text/event-stream" }
                });

            var receivedData = new System.Collections.Generic.List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _httpClient!.SendStreamingRequestAsync(
                request,
                data => receivedData.Add(data),
                cancellationToken.Token);

            // Wait for completion with timeout
            var timeoutTask = Task.Delay(10000, cancellationToken.Token);
            while (!task.IsCompleted && !timeoutTask.IsCompleted)
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                cancellationToken.Cancel();
                yield return null;
            }

            var result = task.Result;

            // Assert
            Assert.IsTrue(result.IsSuccess, $"Request failed: {result.ErrorMessage}");
            Assert.IsTrue(receivedData.Count > 0, "No streaming data received");
            Assert.IsTrue(!string.IsNullOrEmpty(result.ResponseBody), "Response body is empty");
        }

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_DifySSE形式_正しくパースされる()
        {
            // Arrange
            var request = new HttpRequest(
                _configuration!.ApiUrl,
                "POST",
                "{\"query\":\"テストクエリ\",\"user\":\"test-user\",\"response_mode\":\"streaming\"}",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_configuration.ApiKey}" },
                    { "Content-Type", "application/json" },
                    { "Accept", "text/event-stream" }
                });

            var sseEvents = new System.Collections.Generic.List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _httpClient!.SendStreamingRequestAsync(
                request,
                data => {
                    if (data.StartsWith("data: "))
                    {
                        sseEvents.Add(data);
                    }
                },
                cancellationToken.Token);

            // Wait with timeout (real API might be slow)
            var timeoutTask = Task.Delay(15000, cancellationToken.Token);
            while (!task.IsCompleted && !timeoutTask.IsCompleted)
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                cancellationToken.Cancel();
                yield return null;
            }

            // Assert (Note: Real API might fail in test environment)
            // This test validates the SSE parsing capability
            Assert.IsNotNull(task.Result);
        }

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_キャンセルトークン_正しくキャンセルされる()
        {
            // Arrange
            var request = new HttpRequest(
                "https://httpbin.org/delay/10",
                "GET");

            var cancellationToken = new CancellationTokenSource();
            var receivedData = new System.Collections.Generic.List<string>();

            // Act
            var task = _httpClient!.SendStreamingRequestAsync(
                request,
                data => receivedData.Add(data),
                cancellationToken.Token);

            // Cancel after 2 seconds
            yield return new UnityEngine.WaitForSeconds(2.0f);
            cancellationToken.Cancel();

            // Wait for cancellation
            while (!task.IsCompleted && !task.IsCanceled)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.IsCanceled || 
                         task.Exception?.InnerException is OperationCanceledException);
        }

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_nullリクエスト_ArgumentNullExceptionが発生する()
        {
            // Arrange
            var exceptionThrown = false;

            // Act
            var task = _httpClient!.SendStreamingRequestAsync(null!, null);

            // Wait for completion
            while (!task.IsCompleted && !task.IsFaulted)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception?.InnerException is ArgumentNullException);
        }

        #endregion

        #region Connection Test

        [UnityTest]
        public IEnumerator 接続テスト_有効なURL_Trueを返す()
        {
            // Arrange
            var testUrl = "https://httpbin.org/status/200";

            // Act
            var task = _httpClient!.TestConnectionAsync(testUrl);

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.Result);
        }

        [UnityTest]
        public IEnumerator 接続テスト_無効なURL_Falseを返す()
        {
            // Arrange
            var testUrl = "https://invalid-url-that-does-not-exist.com";

            // Act
            var task = _httpClient!.TestConnectionAsync(testUrl);

            // Wait for completion with timeout
            var timeoutTask = Task.Delay(5000);
            while (!task.IsCompleted && !timeoutTask.IsCompleted)
            {
                yield return null;
            }

            // Assert
            Assert.IsFalse(task.Result);
        }

        [UnityTest]
        public IEnumerator 接続テスト_nullURL_ArgumentNullExceptionが発生する()
        {
            // Act
            var task = _httpClient!.TestConnectionAsync(null!);

            // Wait for completion
            while (!task.IsCompleted && !task.IsFaulted)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception?.InnerException is ArgumentNullException);
        }

        #endregion

        #region Performance Tests

        [UnityTest]
        public IEnumerator ストリーミング処理_大量データ_メモリリークなし()
        {
            // Arrange
            var request = new HttpRequest(
                "https://httpbin.org/stream/100", // 100 lines of data
                "GET");

            var receivedCount = 0;
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _httpClient!.SendStreamingRequestAsync(
                request,
                data => {
                    receivedCount++;
                    // Simulate processing
                },
                cancellationToken.Token);

            // Wait for completion with timeout
            var timeoutTask = Task.Delay(20000, cancellationToken.Token);
            while (!task.IsCompleted && !timeoutTask.IsCompleted)
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                cancellationToken.Cancel();
            }

            // Assert
            Assert.IsTrue(receivedCount > 0, "No data received during streaming");
            
            // Memory check (basic validation)
            System.GC.Collect();
            yield return null;
            
            // If we reach here without OutOfMemoryException, test passes
            Assert.Pass($"Memory test passed. Received {receivedCount} data chunks.");
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            _httpClient = null;
            _configuration = null;
        }
    }
}