#nullable enable
using System;

namespace AiTuber.Dify
{
    /// <summary>
    /// OneCommeコメントデータ
    /// </summary>
    [Serializable]
    public class OneCommeComment
    {
        public string? id { get; set; }
        public OneCommeCommentData? data { get; set; }
    }

    /// <summary>
    /// OneCommeコメント詳細データ
    /// </summary>
    [Serializable]
    public class OneCommeCommentData
    {
        public string? comment { get; set; }
        public string? name { get; set; }
        public string? displayName { get; set; }
        public string? speechText { get; set; }
        public string? service { get; set; }
        public string? iconUrl { get; set; }
        public string? timestamp { get; set; }
    }
}