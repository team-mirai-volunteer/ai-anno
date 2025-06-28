using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Entities
{
    /// <summary>
    /// Dify Chat Messages APIリクエストエンティティ
    /// Pure C# Domain Entity、Clean Architecture準拠
    /// Legacy DifyApiRequestからリファクタリング済み
    /// </summary>
    public class DifyRequest
    {
        /// <summary>
        /// ユーザークエリ（必須）
        /// </summary>
        public string Query { get; }

        /// <summary>
        /// ユーザー識別子（必須）
        /// </summary>
        public string User { get; }

        /// <summary>
        /// 会話継続用ID（空文字列で新規会話）
        /// </summary>
        public string ConversationId { get; }

        /// <summary>
        /// レスポンスモード（常に "streaming"）
        /// </summary>
        public string ResponseMode { get; }

        /// <summary>
        /// 入力パラメータ（通常は空）
        /// </summary>
        public IReadOnlyDictionary<string, object> Inputs { get; }

        /// <summary>
        /// 添付ファイル（現在未使用）
        /// </summary>
        public IReadOnlyList<object> Files { get; }

        /// <summary>
        /// DifyRequestエンティティを作成
        /// </summary>
        /// <param name="query">ユーザークエリ</param>
        /// <param name="user">ユーザー識別子</param>
        /// <param name="conversationId">会話ID（オプション）</param>
        /// <exception cref="ArgumentException">無効なパラメータが指定された場合</exception>
        public DifyRequest(string query, string user, string conversationId = "")
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            
            if (string.IsNullOrWhiteSpace(user))
                throw new ArgumentException("User cannot be null or empty", nameof(user));

            Query = query.Trim();
            User = user.Trim();
            ConversationId = conversationId ?? "";
            ResponseMode = "streaming";
            Inputs = new Dictionary<string, object>();
            Files = Array.Empty<object>();
        }

        /// <summary>
        /// リクエストデータの基本バリデーション
        /// </summary>
        /// <returns>必須フィールドが正しく設定されている場合true</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Query) && 
                   !string.IsNullOrWhiteSpace(User) &&
                   !string.IsNullOrWhiteSpace(ResponseMode);
        }

        /// <summary>
        /// Legacy API互換形式に変換
        /// Infrastructure層での利用向け
        /// </summary>
        /// <returns>API送信用のデータ転送オブジェクト</returns>
        public DifyApiRequestDto ToApiRequest()
        {
            return new DifyApiRequestDto
            {
                Query = Query,
                User = User,
                ConversationId = ConversationId,
                ResponseMode = ResponseMode,
                Inputs = new Dictionary<string, object>(Inputs),
                Files = Files.ToArray()
            };
        }

        /// <summary>
        /// 等価性比較（record型の機能を模倣）
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is DifyRequest other &&
                   Query == other.Query &&
                   User == other.User &&
                   ConversationId == other.ConversationId &&
                   ResponseMode == other.ResponseMode;
        }

        /// <summary>
        /// ハッシュコード計算（record型の機能を模倣）
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Query, User, ConversationId, ResponseMode);
        }
    }

    /// <summary>
    /// API送信用データ転送オブジェクト
    /// Infrastructure層でのシリアライゼーション用
    /// </summary>
    public class DifyApiRequestDto
    {
        public string Query { get; set; } = "";
        public string User { get; set; } = "";
        public string ConversationId { get; set; } = "";
        public string ResponseMode { get; set; } = "streaming";
        public Dictionary<string, object> Inputs { get; set; } = new();
        public object[] Files { get; set; } = Array.Empty<object>();
    }
}