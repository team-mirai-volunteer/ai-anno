#nullable enable
using Cysharp.Threading.Tasks;

namespace AiTuber.Dify
{
    /// <summary>
    /// 汎用イベント処理の統一インターフェース
    /// </summary>
    public interface IEventNode
    {
        /// <summary>
        /// イベント処理が完了したか
        /// </summary>
        bool IsCompleted { get; }
        
        /// <summary>
        /// イベント処理が開始されたか
        /// </summary>
        bool IsStarted { get; }
        
        /// <summary>
        /// イベント実行
        /// </summary>
        /// <returns>完了まで待機するTask</returns>
        UniTask Execute();
    }
}