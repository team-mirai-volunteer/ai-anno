using AiTuber.Services.Dify.Domain.Entities;

#nullable enable

namespace AiTuber.Services.Dify.Application.Ports
{
    /// <summary>
    /// レスポンス処理サービスポート
    /// 音声・テキストイベントの処理を担当
    /// Clean Architecture準拠
    /// </summary>
    public interface IResponseProcessor
    {
        /// <summary>
        /// 音声イベントを処理
        /// </summary>
        /// <param name="audioEvent">音声ストリームイベント</param>
        void ProcessAudioEvent(DifyStreamEvent audioEvent);

        /// <summary>
        /// テキストイベントを処理
        /// </summary>
        /// <param name="textEvent">テキストストリームイベント</param>
        void ProcessTextEvent(DifyStreamEvent textEvent);
    }
}