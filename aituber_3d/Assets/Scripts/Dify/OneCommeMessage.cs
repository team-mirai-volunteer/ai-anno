#nullable enable
using System;

namespace AiTuber.Dify
{
    /// <summary>
    /// OneCommeメッセージ構造
    /// </summary>
    [Serializable]
    public class OneCommeMessage
    {
        public string? type { get; set; }
        public OneCommeMessageData? data { get; set; }
    }

    /// <summary>
    /// OneCommeメッセージデータ
    /// </summary>
    [Serializable]
    public class OneCommeMessageData
    {
        public OneCommeComment[]? comments { get; set; }
    }
}