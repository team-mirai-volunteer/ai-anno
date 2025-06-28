using System;
using System.Threading;
using System.Threading.Tasks;
using AiTuber.Services.Dify.Domain.Entities;
using AiTuber.Services.Dify.Infrastructure.Http;

#nullable enable

namespace AiTuber.Services.Dify.Application.UseCases
{
    /// <summary>
    /// クエリ処理ユースケースインターフェース
    /// Application層 Clean Architecture準拠
    /// </summary>
    public interface IProcessQueryUseCase
    {
        /// <summary>
        /// クエリを実行
        /// </summary>
        /// <param name="request">Difyリクエスト</param>
        /// <param name="onEventReceived">イベント受信時のコールバック</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>クエリレスポンス</returns>
        Task<QueryResponse> ExecuteAsync(
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