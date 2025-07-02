using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Linq;
using System;

namespace AiTuber
{
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
        [SerializeField] private GameObject _settingsUI = null;
        [SerializeField] private float _longPressDuration = 2.0f;

        private string _questionerIconUrl = null;
        private string[] _queuedIconUrls = null;
        private string _slideImageUrl = null;

        public int QueueCount => _queuedIcons.Length;

        private bool _isPointerDown = false;
        private float _pointerDownTime = 0f;
        private void Update()
        {
            if (_isPointerDown)
            {
                _pointerDownTime += Time.unscaledDeltaTime;
                if (_pointerDownTime >= _longPressDuration)
                {
                    _isPointerDown = false;
                    _pointerDownTime = 0f;
                    if (_settingsUI != null)
                    {
                        _settingsUI.SetActive(true);
                    }
                }
            }
        }

        public void OnSettingsAreaPointerDown()
        {
            _isPointerDown = true;
            _pointerDownTime = 0f;
        }

        public void OnSettingsAreaPointerUp()
        {
            _isPointerDown = false;
            _pointerDownTime = 0f;
        }


        public void SetQuestionerIconUrl(string url)
        {
            // Debug.Log($"MainUI.SetQuestionerIconUrl called with URL: {url}");
            _questionerIconUrl = url;
            LoadImage(url, texture =>
            {
                if (_questionerIconUrl == url)
                {
                    _questionerIcon.texture = texture;
                }
                else
                {
                    Debug.LogWarning($"Questioner icon URL mismatch: expected {_questionerIconUrl}, got {url}");
                }
            });
        }
        public void SetQuestionerName(string name)
        {
            // Debug.Log($"MainUI.SetQuestionerName called with Name: {name}");
            _questionerName.text = name;
        }
        public void SetTotalAnswerCount(int count)
        {
            _totalAnswerCountText.text = count.ToString("N0");
        }
        public void SetQueuedQuestionCount(int count)
        {
            // Debug.Log($"MainUI.SetQueuedQuestionCount called with count: {count}");
            _queuedQuestionCountText.text = count.ToString("N0");
        }
        public void SetQueuedIcon(string url, int index)
        {
            // Debug.Log($"MainUI.SetQueuedIcon called with URL: {url}, Index: {index}");
            if (index < 0 || index >= _queuedIcons.Length)
            {
                Debug.LogWarning($"Index out of bounds for queued icons. Index: {index}, Length: {_queuedIcons.Length}");
                return;
            }
            _queuedIconUrls[index] = url;
            LoadImage(url, texture =>
            {
                if (_queuedIconUrls[index] == url)
                {
                    _queuedIcons[index].texture = texture;
                }
                else
                {
                    Debug.LogWarning($"Queued icon URL mismatch: expected {_queuedIconUrls[index]}, got {url}");
                }
            });
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
            _slideImageUrl = url;
            LoadImage(url, texture =>
            {
                if (texture != null)
                {
                    _slideImage.texture = texture;
                }
            });
        }
        private async void LoadImage(string url, Action<Texture2D> onCompleted)
        {
            if (string.IsNullOrEmpty(url))
            {
                onCompleted?.Invoke(null);
                return;
            }

            var texture = await LoadImageAsync(url);
            if (texture != null)
            {
                onCompleted?.Invoke(texture);
            }
            else
            {
                Debug.LogWarning($"Failed to load image from URL: {url}");
                onCompleted?.Invoke(null);
            }
        }

        private Dictionary<string, Texture2D> _imageCache = new();

        private void Awake()
        {
            _queuedIconUrls = new string[_queuedIcons.Length];
        }

        private void Start()
        {
            // 念の為起動時に設定 UI を非表示にしておく
            _settingsUI?.SetActive(false);
        }

        private void Test()
        {
            Debug.Log("MainUI.Test() called");
            var iconUrl = "https://yt3.googleusercontent.com/ZXlu3tgzsXVrVURXwFFhZlHTd8tzAfGTElWGIZqv7gA-0kWb_yL3YzNEbPtGNK-6HbpLPxUV=s88-c-k-c0x00ffffff-no-rj";
            SetQuestionerIconUrl(iconUrl);
            SetQuestionerName("ほにゃ");
            SetTotalAnswerCount(456789);
            SetQueuedQuestionCount(99);
            SetQueuedIcon(iconUrl, 0);
            SetQueuedIcon(iconUrl, 1);
            SetQueuedIcon(iconUrl, 2);
            SetQueuedIcon(iconUrl, 3);
            SetQuestionText(string.Concat(Enumerable.Repeat("これはテストの質問です。", 16)));
            SetAnswerText(string.Concat(Enumerable.Repeat("これはテストの回答です。", 16)));
            
            // ローカルでアクセス可能な確実な画像URL（GitHub成功例を使用）
            var slideUrl = "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png";
            Debug.Log($"Test: Setting slide URL to {slideUrl}");
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
}
