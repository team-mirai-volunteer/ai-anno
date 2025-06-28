using System;
using System.Collections.Generic;

#nullable enable

namespace AiTuber.Services.Dify.Domain.Entities
{
    /// <summary>
    /// Dify Chat Messages APIリクエストエンティティ
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
        public readonly string ResponseMode = "streaming";

        /// <summary>
        /// 入力パラメータ（通常は空）
        /// </summary>
        public IReadOnlyDictionary<string, object> Inputs { get; }

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
            Inputs = new Dictionary<string, object>();
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
    }
}