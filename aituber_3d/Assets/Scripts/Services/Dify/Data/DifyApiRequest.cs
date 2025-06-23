using System;
using System.Collections.Generic;

namespace AiTuber.Services.Dify.Data
{
    /// <summary>
    /// Dify Chat Messages APIリクエスト用データクラス
    /// Pure C#実装でUnity非依存、テスト容易
    /// </summary>
    [System.Serializable]
    public class DifyApiRequest
    {
        /// <summary>
        /// 入力パラメータ（通常は空のDictionary）
        /// </summary>
        public Dictionary<string, object> inputs { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// ユーザーからの質問・コメント内容
        /// </summary>
        public string query { get; set; }
        
        /// <summary>
        /// レスポンスモード (常に "streaming")
        /// </summary>
        public string response_mode { get; set; } = "streaming";
        
        /// <summary>
        /// 会話継続用ID（空文字列で新規会話）
        /// </summary>
        public string conversation_id { get; set; } = "";
        
        /// <summary>
        /// ユーザー識別子
        /// </summary>
        public string user { get; set; }
        
        /// <summary>
        /// 添付ファイル配列（現在未使用）
        /// </summary>
        public object[] files { get; set; } = new object[0];
        
        /// <summary>
        /// リクエストデータの基本バリデーション
        /// </summary>
        /// <returns>必須フィールドが正しく設定されている場合true</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(query) && 
                   !string.IsNullOrWhiteSpace(user) &&
                   response_mode == "streaming";
        }
        
        
        /// <summary>
        /// 新規会話かどうかを判定
        /// </summary>
        public bool IsNewConversation => string.IsNullOrEmpty(conversation_id);
    }
}