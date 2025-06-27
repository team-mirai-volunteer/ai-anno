using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Domain.Entities;

#nullable enable

namespace AiTuber.Services.Dify.Application.Ports
{
    /// <summary>
    /// Difyストリーミング通信ポート
    /// Infrastructure層への依存性逆転用インターフェース
    /// Clean Architecture準拠
    /// </summary>
    public interface IDifyStreamingPort
    {
        /// <summary>
        /// ストリーミングクエリを実行
        /// </summary>
        /// <param name="request">Difyリクエスト</param>
        /// <param name="onEventReceived">ストリームイベント受信コールバック</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>クエリレスポンス</returns>
        Task<QueryResponse> ExecuteStreamingAsync(
            DifyRequest request,
            Action<DifyStreamEvent>? onEventReceived = null,
            CancellationToken cancellationToken = default);
    }
}