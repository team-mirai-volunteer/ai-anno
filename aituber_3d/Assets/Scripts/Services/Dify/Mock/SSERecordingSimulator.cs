using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

#nullable enable

namespace AiTuber.Services.Dify.Mock
{
    /// <summary>
    /// SSE録画タイミング再現シミュレーター
    /// Mock例外領域 - Clean Architecture例外として配置
    /// 録画時間通りの正確な再生タイミング制御
    /// </summary>
    public class SSERecordingSimulator
    {
        private readonly float _playbackSpeed;

        /// <summary>
        /// SSERecordingSimulatorを作成
        /// </summary>
        /// <param name="playbackSpeed">再生速度（1.0f=等倍、2.0f=2倍速）</param>
        /// <exception cref="ArgumentException">再生速度が不正な場合</exception>
        public SSERecordingSimulator(float playbackSpeed = 1.0f)
        {
            if (playbackSpeed <= 0)
                throw new ArgumentException("Playback speed must be positive", nameof(playbackSpeed));

            _playbackSpeed = playbackSpeed;
        }

        /// <summary>
        /// 録画イベントの再生タイミングを待機
        /// タイムスタンプ基準の正確な遅延実装
        /// </summary>
        /// <param name="baseTime">録画開始基準時刻</param>
        /// <param name="eventTimestamp">イベントタイムスタンプ（ミリ秒）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>待機タスク</returns>
        public async UniTask WaitForEventTimingAsync(
            DateTimeOffset baseTime, 
            double eventTimestamp, 
            CancellationToken cancellationToken = default)
        {
            // 再生速度を考慮したタイミング計算
            var adjustedTimestamp = eventTimestamp / _playbackSpeed;
            var targetTime = baseTime.AddMilliseconds(adjustedTimestamp);
            var currentTime = DateTimeOffset.UtcNow;
            
            // 遅延時間計算
            var delay = targetTime - currentTime;
            
            // 既に時刻を過ぎている場合は待機しない
            if (delay <= TimeSpan.Zero)
                return;
            
            // UniTaskによる非ブロッキング遅延
            await UniTask.Delay(delay, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// イベント間隔での待機
        /// 前回イベントからの経過時間基準
        /// </summary>
        /// <param name="previousTimestamp">前回イベントタイムスタンプ（ミリ秒）</param>
        /// <param name="currentTimestamp">現在イベントタイムスタンプ（ミリ秒）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>待機タスク</returns>
        public async UniTask WaitForEventIntervalAsync(
            double previousTimestamp, 
            double currentTimestamp, 
            CancellationToken cancellationToken = default)
        {
            // イベント間隔計算
            var interval = currentTimestamp - previousTimestamp;
            
            // 再生速度調整
            var adjustedInterval = interval / _playbackSpeed;
            
            // 負の間隔は無効
            if (adjustedInterval <= 0)
                return;
            
            // UniTaskによる待機
            await UniTask.Delay(TimeSpan.FromMilliseconds(adjustedInterval), cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 再生速度を取得
        /// </summary>
        /// <returns>再生速度</returns>
        public float GetPlaybackSpeed()
        {
            return _playbackSpeed;
        }

        /// <summary>
        /// 調整済み総再生時間を計算
        /// </summary>
        /// <param name="originalDurationMs">元の再生時間（ミリ秒）</param>
        /// <returns>調整済み再生時間（ミリ秒）</returns>
        public double GetAdjustedDurationMs(double originalDurationMs)
        {
            return originalDurationMs / _playbackSpeed;
        }

        /// <summary>
        /// タイムスタンプを調整済み時刻に変換
        /// </summary>
        /// <param name="baseTime">基準時刻</param>
        /// <param name="timestamp">タイムスタンプ（ミリ秒）</param>
        /// <returns>調整済み時刻</returns>
        public DateTimeOffset GetAdjustedTime(DateTimeOffset baseTime, double timestamp)
        {
            var adjustedTimestamp = timestamp / _playbackSpeed;
            return baseTime.AddMilliseconds(adjustedTimestamp);
        }
    }
}