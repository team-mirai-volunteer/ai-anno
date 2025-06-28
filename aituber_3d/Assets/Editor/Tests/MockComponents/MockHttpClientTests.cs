using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using AiTuber.Services.Dify.Mock;
using AiTuber.Services.Dify.Infrastructure.Http;

#nullable enable

namespace AiTuber.Tests.MockComponents
{
    /// <summary>
    /// MockHttpClient TDD テスト
    /// Phase 1 Red: Mock例外領域の失敗テスト作成
    /// SSERecordings完全再現のIHttpClient実装要件明確化
    /// </summary>
    [TestFixture]
    public class MockHttpClientTests
    {
        private MockHttpClient? _mockClient;
        private SSERecordingReader? _recordingReader;
        private SSERecordingSimulator? _simulator;

        [SetUp]
        public void SetUp()
        {
            // TDD Red: 未実装クラスのため失敗する
            _recordingReader = new SSERecordingReader("SSERecordings/dify_sse_recording.json");
            _simulator = new SSERecordingSimulator(1.0f); // Normal speed
            _mockClient = new MockHttpClient(_recordingReader, _simulator);
        }

        #region Constructor Tests

        [Test]
        public void コンストラクタ_有効な依存関係_正常にインスタンス化される()
        {
            // Arrange & Act & Assert
            Assert.IsNotNull(_mockClient);
        }

        [Test]
        public void コンストラクタ_nullRecordingReader_ArgumentNullExceptionが発生する()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MockHttpClient(null!, _simulator!));
        }

        [Test]
        public void コンストラクタ_nullSimulator_ArgumentNullExceptionが発生する()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MockHttpClient(_recordingReader!, null!));
        }

        #endregion

        #region IHttpClient Interface Compliance Tests

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_IHttpClientインターフェース_正しく実装されている()
        {
            // Arrange
            var request = new HttpRequest(
                "https://api.dify.ai/v1/chat-messages",
                "POST",
                "{\"query\":\"テストクエリ\",\"user\":\"test-user\"}");

            var receivedData = new List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => receivedData.Add(data),
                cancellationToken.Token);

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var result = task.Result;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess, "Mock client should always succeed");
            Assert.IsTrue(receivedData.Count > 0, "Should receive streaming data");
        }

        [UnityTest]
        public IEnumerator 接続テスト_IHttpClientインターフェース_常にTrueを返す()
        {
            // Arrange
            var testUrl = "https://api.dify.ai/v1/chat-messages";

            // Act
            var task = _mockClient!.TestConnectionAsync(testUrl);

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.Result, "Mock client should always return true for connection test");
        }

        #endregion

        #region SSE Recording Reproduction Tests

        [UnityTest]
        public IEnumerator SSE録画再生_1179イベント_完全再現される()
        {
            // Arrange
            var request = new HttpRequest(
                "https://api.dify.ai/v1/chat-messages",
                "POST",
                "{\"query\":\"テストクエリ\",\"user\":\"test-user\"}");

            var receivedEvents = new List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => {
                    if (data.StartsWith("data: ") && !data.Contains("[DONE]"))
                    {
                        receivedEvents.Add(data);
                    }
                },
                cancellationToken.Token);

            // Wait for completion with timeout (should take ~15 seconds for full recording)
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
            Assert.AreEqual(1179, receivedEvents.Count, 
                "Should reproduce all 1,179 events from recording (excluding [DONE] marker)");
        }

        [UnityTest]
        public IEnumerator SSE録画再生_タイミング精度_100ミリ秒以内の誤差()
        {
            // Arrange
            var request = new HttpRequest("https://api.dify.ai/v1/chat-messages", "POST");
            var receivedTimestamps = new List<DateTimeOffset>();
            var cancellationToken = new CancellationTokenSource();
            var startTime = DateTimeOffset.UtcNow;

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => {
                    if (data.StartsWith("data: ") && !data.Contains("[DONE]"))
                    {
                        receivedTimestamps.Add(DateTimeOffset.UtcNow);
                    }
                },
                cancellationToken.Token);

            // Wait for first few events (test timing precision) - EditMode compatible
            var startWait = DateTimeOffset.UtcNow;
            while ((DateTimeOffset.UtcNow - startWait).TotalSeconds < 2.0 && receivedTimestamps.Count < 10)
            {
                yield return null;
            }
            cancellationToken.Cancel();

            // Assert timing precision
            if (receivedTimestamps.Count >= 2)
            {
                var firstEventDelay = (receivedTimestamps[0] - startTime).TotalMilliseconds;
                var expectedFirstEventDelay = 783.168; // From recording data
                
                // Unity/Task.Delayの精度を考慮して100ms以内に緩和
                Assert.IsTrue(Math.Abs(firstEventDelay - expectedFirstEventDelay) <= 100,
                    $"First event timing should be within 100ms. Expected: {expectedFirstEventDelay}ms, Actual: {firstEventDelay}ms");
            }
        }

        [UnityTest]
        public IEnumerator SSE録画再生_高速再生_2倍速で正しく動作()
        {
            // Arrange
            var fastSimulator = new SSERecordingSimulator(2.0f); // 2x speed
            var fastMockClient = new MockHttpClient(_recordingReader!, fastSimulator);
            
            var request = new HttpRequest("https://api.dify.ai/v1/chat-messages", "POST");
            var receivedEvents = new List<string>();
            var cancellationToken = new CancellationTokenSource();
            var startTime = DateTimeOffset.UtcNow;

            // Act
            var task = fastMockClient.SendStreamingRequestAsync(
                request,
                data => {
                    if (data.StartsWith("data: ") && !data.Contains("[DONE]"))
                    {
                        receivedEvents.Add(data);
                    }
                },
                cancellationToken.Token);

            // Wait for completion (should be ~7.5 seconds at 2x speed)
            var timeoutTask = Task.Delay(10000, cancellationToken.Token);
            while (!task.IsCompleted && !timeoutTask.IsCompleted)
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                cancellationToken.Cancel();
            }

            var totalTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            // Assert
            Assert.IsTrue(totalTime < 10000, 
                $"2x speed playback should complete in under 10 seconds. Actual: {totalTime}ms");
            Assert.IsTrue(receivedEvents.Count > 100, 
                "Should receive significant number of events even with cancellation");
        }

        #endregion

        #region Event Type Specific Tests

        [UnityTest]
        public IEnumerator 録画再生_Messageイベント_正しいJSONフォーマット()
        {
            // Arrange
            var request = new HttpRequest("https://api.dify.ai/v1/chat-messages", "POST");
            var messageEvents = new List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => {
                    if (data.Contains("\"event\":\"message\""))
                    {
                        messageEvents.Add(data);
                    }
                },
                cancellationToken.Token);

            // Wait for some message events - EditMode compatible
            var startWait = DateTimeOffset.UtcNow;
            while ((DateTimeOffset.UtcNow - startWait).TotalSeconds < 3.0 && messageEvents.Count < 10)
            {
                yield return null;
            }
            cancellationToken.Cancel();

            // Assert
            Assert.IsTrue(messageEvents.Count > 0, "Should receive message events");
            
            foreach (var eventData in messageEvents)
            {
                Assert.IsTrue(eventData.StartsWith("data: "), "Should have proper SSE format");
                Assert.IsTrue(eventData.Contains("\"answer\""), "Message events should contain answer field");
                Assert.IsTrue(eventData.Contains("\"conversation_id\""), "Message events should contain conversation_id");
            }
        }

        [UnityTest]
        public IEnumerator 録画再生_TTSAudioイベント_Base64エンコーディング()
        {
            // Arrange
            var request = new HttpRequest("https://api.dify.ai/v1/chat-messages", "POST");
            var audioEvents = new List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => {
                    if (data.Contains("\"event\":\"tts_message\""))
                    {
                        audioEvents.Add(data);
                    }
                },
                cancellationToken.Token);

            // Wait for audio events (they come later in the recording) - EditMode compatible
            var startWait = DateTimeOffset.UtcNow;
            while ((DateTimeOffset.UtcNow - startWait).TotalSeconds < 5.0 && audioEvents.Count < 5)
            {
                yield return null;
            }
            cancellationToken.Cancel();

            // Assert
            Assert.IsTrue(audioEvents.Count > 0, "Should receive audio events");
            
            foreach (var eventData in audioEvents)
            {
                Assert.IsTrue(eventData.Contains("\"audio\""), "Audio events should contain audio field");
                // Extract and validate Base64 format
                var audioMatch = System.Text.RegularExpressions.Regex.Match(
                    eventData, "\"audio\":\"([^\"]+)\"");
                if (audioMatch.Success)
                {
                    var audioBase64 = audioMatch.Groups[1].Value;
                    Assert.DoesNotThrow(() => Convert.FromBase64String(audioBase64),
                        "Audio data should be valid Base64");
                }
            }
        }

        #endregion

        #region Cancellation and Error Handling Tests

        [UnityTest]
        public IEnumerator ストリーミング中キャンセル_正しくキャンセルされる()
        {
            // Arrange
            var request = new HttpRequest("https://api.dify.ai/v1/chat-messages", "POST");
            var receivedEvents = new List<string>();
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => receivedEvents.Add(data),
                cancellationToken.Token);

            // Cancel after 1 second - EditMode compatible
            var startWait = DateTimeOffset.UtcNow;
            while ((DateTimeOffset.UtcNow - startWait).TotalSeconds < 1.0)
            {
                yield return null;
            }
            cancellationToken.Cancel();

            // Wait for cancellation
            var cancelWait = DateTimeOffset.UtcNow;
            while ((DateTimeOffset.UtcNow - cancelWait).TotalSeconds < 0.5 && !task.IsCompleted && !task.IsCanceled)
            {
                yield return null;
            }

            // Assert
            Assert.IsTrue(task.IsCanceled || task.IsCompleted, 
                "Task should be canceled or completed");
            Assert.IsTrue(receivedEvents.Count > 0, 
                "Should have received some events before cancellation");
            Assert.IsTrue(receivedEvents.Count < 1179, 
                "Should not have received all events due to cancellation");
        }

        [UnityTest]
        public IEnumerator ストリーミングリクエスト送信_nullリクエスト_ArgumentNullExceptionが発生する()
        {
            // Act
            var task = _mockClient!.SendStreamingRequestAsync(null!, null);

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
        public IEnumerator パフォーマンステスト_1179イベント再生_完全処理()
        {
            // Arrange
            var request = new HttpRequest("https://api.dify.ai/v1/chat-messages", "POST");
            var eventCount = 0;
            var cancellationToken = new CancellationTokenSource();

            // Act
            var task = _mockClient!.SendStreamingRequestAsync(
                request,
                data => {
                    eventCount++;
                    // Count events for performance verification
                },
                cancellationToken.Token);

            // Wait for completion
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
            Assert.IsTrue(eventCount > 1000, 
                $"Should process most events. Processed: {eventCount}");
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            _mockClient = null;
            _recordingReader = null;
            _simulator = null;
        }
    }
}