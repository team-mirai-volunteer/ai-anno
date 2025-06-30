using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Linq;

public class MainUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _topText = null;
    [SerializeField] private RawImage _questionerIcon = null;
    [SerializeField] private TMP_Text _questionerName = null;
    [SerializeField] private TMP_Text _totalAnswerCountText = null;
    [SerializeField] private TMP_Text _queuedQuestionCountText = null;
    [SerializeField] private RawImage[] _queuedIcons = null;
    [SerializeField] private TMP_Text _questionText = null;
    [SerializeField] private TMP_Text _answerText = null;
    [SerializeField] private RawImage _slideImage = null;

    public void SetQuestionerIconUrl(string url)
    {
        LoadImage(url, _questionerIcon);
    }
    public void SetQuestionerName(string name)
    {
        _questionerName.text = name;
    }
    public void SetTotalAnswerCount(int count)
    {
        _totalAnswerCountText.text = count.ToString("N0");
    }
    public void SetQueuedQuestionCount(int count)
    {
        _queuedQuestionCountText.text = count.ToString("N0");
    }
    public void SetQuestionText(string text)
    {
        _questionText.text = text;
    }
    public void SetAnswerText(string text)
    {
        _answerText.text = text;
    }
    public void SetSlideImageUrl(string url)
    {
        LoadImage(url, _slideImage);
    }
    private async void LoadImage(string url, RawImage targetImage)
    {
        if (string.IsNullOrEmpty(url))
        {
            targetImage.texture = null;
            return;
        }

        var texture = await LoadImageAsync(url);
        if (texture != null)
        {
            targetImage.texture = texture;
        }
    }

    private Dictionary<string, Texture2D> _imageCache = new();

    private void Start()
    {
        Test();
    }

    private void Test()
    {
        var iconUrl = "https://yt3.googleusercontent.com/ZXlu3tgzsXVrVURXwFFhZlHTd8tzAfGTElWGIZqv7gA-0kWb_yL3YzNEbPtGNK-6HbpLPxUV=s88-c-k-c0x00ffffff-no-rj";
        SetQuestionerIconUrl(iconUrl);
        SetQuestionerName("ほにゃ");
        SetTotalAnswerCount(456789);
        SetQueuedQuestionCount(123);
        SetQuestionText(string.Concat(Enumerable.Repeat("これはテストの質問です。", 16)));
        SetAnswerText(string.Concat(Enumerable.Repeat("これはテストの回答です。", 16)));
        var slideUrl = "https://yt3.googleusercontent.com/vlfKkLZHqZzpoGKLCzUtqqhe6U6THBSkgXfOI8Z0dgqL1ZPX7qCwyQmux2_1zeXhN20QOXfpnCY=w1707-fcrop64=1,00005a57ffffa5a8-k-c0xffffffff-no-nd-rj";
        SetSlideImageUrl(slideUrl);
    }

    private async Task<Texture2D> LoadImageAsync(string url)
    {
        if (_imageCache.TryGetValue(url, out Texture2D cachedTexture))
        {
            return cachedTexture; // キャッシュから取得
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            var operation = request.SendWebRequest();

            // await で非同期完了を待つ
            while (!operation.isDone)
            {
                await Task.Yield(); // メインスレッドをブロックしないように待機
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"画像取得失敗: {request.error}");
                return null;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            _imageCache[url] = texture;

            return texture;
        }
    }
}
