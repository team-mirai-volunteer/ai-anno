using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Application.UseCases;
using AiTuber.Services.Dify.Domain.Entities;
using AiTuber.Services.Dify.Infrastructure.Http;

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

        /// <summary>
        /// 設定情報を取得
        /// </summary>
        /// <returns>Dify設定</returns>
        DifyConfiguration GetConfiguration();

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>接続成功フラグ</returns>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}