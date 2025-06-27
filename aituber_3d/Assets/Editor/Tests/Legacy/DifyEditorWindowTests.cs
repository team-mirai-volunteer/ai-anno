using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using AiTuber.Editor.Dify;

namespace AiTuber.Tests.Legacy.Editor
{
    /// <summary>
    /// DifyEditorWindow基本機能テスト
    /// EditorPrefsを変更しない無害なテストのみ
    /// </summary>
    [TestFixture]
    public class DifyEditorWindowTests
    {
        #region Safe Tests Only

        [Test]
        public void DifyEditorSettings存在確認_クラスが正しく定義されている()
        {
            // 基本的な存在確認のみ（EditorPrefsを変更しない）
            Assert.IsNotNull(typeof(DifyEditorSettings));
        }

        [Test]
        public void 設定検証メソッド_基本動作確認()
        {
            // ValidateConfigurationメソッドが例外を投げないことを確認
            Assert.DoesNotThrow(() => DifyEditorSettings.ValidateConfiguration());
        }

        [Test]
        public void JSON文字列生成_例外なく動作()
        {
            // ToJsonStringメソッドが例外を投げないことを確認
            var result = DifyEditorSettings.ToJsonString();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("apiKey"));
        }

        [Test]
        public void 設定サマリー生成_例外なく動作()
        {
            // GetConfigurationSummaryメソッドが例外を投げないことを確認
            var result = DifyEditorSettings.GetConfigurationSummary();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("API Key"));
        }

        #endregion
    }
}