using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using AiTuber.Services.Dify.Infrastructure.Http;
using Newtonsoft.Json;

#nullable enable

namespace AiTuber.Services.Dify.Mock
{
    /// <summary>
    /// Mock HTTPクライアント実装
    /// Mock例外領域 - Clean Architecture例外として配置
    /// SSERecordings完全再現によるIHttpClient実装
    /// </summary>
    public class MockHttpClient : IHttpClient
    {
        private readonly SSERecordingReader _recordingReader;
        private readonly SSERecordingSimulator _simulator;

        /// <summary>
        /// MockHttpClientを作成
        /// </summary>
        /// <param name="recordingReader">録画データ読み込み</param>
        /// <param name="simulator">タイミング再現シミュレーター</param>
        /// <exception cref="ArgumentNullException">必須パラメータがnullの場合</exception>
        public MockHttpClient(SSERecordingReader recordingReader, SSERecordingSimulator simulator)
        {
            _recordingReader = recordingReader ?? throw new ArgumentNullException(nameof(recordingReader));
            _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        }

        /// <summary>
        /// ストリーミングリクエストを送信（Mock実装）
        /// SSERecordings完全再現による1,179イベント配信
        /// </summary>
        /// <param name="request">HTTPリクエスト</param>
        /// <param name="onDataReceived">データ受信時のコールバック（Push型）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>HTTPレスポンス</returns>
        /// <exception cref="ArgumentNullException">リクエストがnullの場合</exception>
        public async Task<HttpResponse> SendStreamingRequestAsync(
            HttpRequest request, 
            Action<string>? onDataReceived, 
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // キャンセル即座確認
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 録画イベント取得
                var events = _recordingReader.GetAllEvents();
                var responseBuilder = new StringBuilder();
                var baseTime = DateTimeOffset.UtcNow;

                // 1,179イベント完全再現ループ
                foreach (var recordingEvent in events)
                {
                    // キャンセル確認
                    cancellationToken.ThrowIfCancellationRequested();

                    // タイミング再現待機 (EditMode対応でTask.Delay使用)
                    await WaitForEventTimingTaskAsync(
                        baseTime, 
                        recordingEvent.Timestamp, 
                        cancellationToken);

                    // SSE形式データ生成
                    var sseData = CreateSSEData(recordingEvent);
                    
                    // Push型コールバック実行
                    if (onDataReceived != null)
                    {
                        var sseMessage = $"data: {sseData}\n\n";
                        onDataReceived(sseMessage);
                    }
                    
                    // レスポンス蓄積
                    responseBuilder.AppendLine($"data: {sseData}");
                }

                // 終了マーカー送信
                if (onDataReceived != null)
                {
                    onDataReceived("data: [DONE]\n\n");
                }
                responseBuilder.AppendLine("data: [DONE]");

                return new HttpResponse(
                    true,
                    "",
                    responseBuilder.ToString());
            }
            catch (OperationCanceledException)
            {
                throw; // キャンセル例外は再スロー
            }
            catch (Exception ex)
            {
                return new HttpResponse(
                    false,
                    $"Mock streaming failed: {ex.Message}",
                    "");
            }
        }

        /// <summary>
        /// 接続テスト（Mock実装）
        /// 常にtrueを返す（Mock環境では接続成功扱い）
        /// </summary>
        /// <param name="url">テスト対象URL</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>常にtrue</returns>
        public async Task<bool> TestConnectionAsync(
            string url, 
            CancellationToken cancellationToken = default)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            // Mock環境では軽量な遅延のみ
            await UniTask.Delay(TimeSpan.FromMilliseconds(50), cancellationToken: cancellationToken);
            
            return true; // Mock環境では常に接続成功
        }

        /// <summary>
        /// SSE形式データを生成
        /// Dify SSE JSON形式準拠
        /// </summary>
        /// <param name="recordingEvent">録画イベントデータ</param>
        /// <returns>SSE JSON文字列</returns>
        private string CreateSSEData(SSERecordingEvent recordingEvent)
        {
            // Dify SSE JSON形式に変換
            var sseObject = new
            {
                @event = recordingEvent.EventType,
                conversation_id = recordingEvent.ConversationId,
                message_id = recordingEvent.MessageId,
                answer = string.IsNullOrEmpty(recordingEvent.Answer) ? null : recordingEvent.Answer,
                audio = string.IsNullOrEmpty(recordingEvent.AudioData) ? null : recordingEvent.AudioData,
                task_id = string.IsNullOrEmpty(recordingEvent.TaskId) ? null : recordingEvent.TaskId,
                created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // JSON シリアライズ（null値は除外）
            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            return JsonConvert.SerializeObject(sseObject, jsonSettings);
        }

        /// <summary>
        /// EditMode対応のタイミング待機 (Task.Delay使用)
        /// </summary>
        /// <param name="baseTime">録画開始基準時刻</param>
        /// <param name="eventTimestamp">イベントタイムスタンプ（ミリ秒）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>待機タスク</returns>
        private async Task WaitForEventTimingTaskAsync(
            DateTimeOffset baseTime, 
            double eventTimestamp, 
            CancellationToken cancellationToken = default)
        {
            // 再生速度を考慮したタイミング計算
            var adjustedTimestamp = eventTimestamp / _simulator.GetPlaybackSpeed();
            var targetTime = baseTime.AddMilliseconds(adjustedTimestamp);
            var currentTime = DateTimeOffset.UtcNow;
            
            // 遅延時間計算
            var delay = targetTime - currentTime;
            
            // 既に時刻を過ぎている場合は待機しない
            if (delay <= TimeSpan.Zero)
                return;
            
            // Task.Delayによる非ブロッキング遅延 (EditMode対応)
            await Task.Delay(delay, cancellationToken);
        }
    }
}