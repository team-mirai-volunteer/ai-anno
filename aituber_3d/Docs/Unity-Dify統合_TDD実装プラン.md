# Unity-Dify統合 TDD実装プラン

## 🎯 **実装アプローチ**
**TDD + Pure C# 設計**: MonoBehaviourを最小限にしてエディタプレイなしでユニットテスト可能

## 📋 **アーキテクチャ設計**

### **レイヤー分離**
```
┌─────────────────────┐
│   Unity Adapter     │ ← MonoBehaviour (最小限)
│  DifyQueueManager   │
├─────────────────────┤
│   Service Layer     │ ← Pure C# (TDD対象)
│    DifyService      │
├─────────────────────┤
│   Infrastructure    │ ← Pure C# (TDD対象)
│ DifyApiClient       │
│ SSEParser           │
│ AudioStreamHandler  │
├─────────────────────┤
│   Data Layer        │ ← Pure C# (TDD対象)
│ DifyApiRequest      │
│ DifyStreamEvent     │
└─────────────────────┘
```

## 📋 **Phase 1: Pure C# データレイヤー** (TDD Ready)

### **データクラス設計**
```csharp
// Scripts/Services/Dify/Data/DifyApiRequest.cs
[System.Serializable]
public class DifyApiRequest
{
    public Dictionary<string, object> inputs { get; set; } = new Dictionary<string, object>();
    public string query { get; set; }
    public string response_mode { get; set; } = "streaming";
    public string conversation_id { get; set; } = "";
    public string user { get; set; }
    public object[] files { get; set; } = new object[0];
    
    // バリデーション機能
    public bool IsValid() => !string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(user);
}

// Scripts/Services/Dify/Data/DifyStreamEvent.cs
[System.Serializable]
public class DifyStreamEvent
{
    public string @event { get; set; }
    public string conversation_id { get; set; }
    public string message_id { get; set; }
    public string answer { get; set; }
    public string audio { get; set; }  // Base64 MP3データ
    public long created_at { get; set; }
    
    // イベント種別判定
    public bool IsTextMessage => @event == "message";
    public bool IsTTSMessage => @event == "tts_message";
    public bool IsMessageEnd => @event == "message_end";
}

// Scripts/Services/Dify/Data/DifyProcessingResult.cs
public class DifyProcessingResult
{
    public string ConversationId { get; set; }
    public string MessageId { get; set; }
    public string TextResponse { get; set; }
    public List<byte[]> AudioChunks { get; set; } = new List<byte[]>();
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
}
```

### **テストファイル**
```csharp
// Tests/Editor/DifyDataTests.cs
[TestFixture]
public class DifyApiRequestTests
{
    [Test]
    public void Constructor_DefaultValues_InitializesCorrectly()
    {
        var request = new DifyApiRequest();
        Assert.AreEqual("streaming", request.response_mode);
        Assert.AreEqual("", request.conversation_id);
        Assert.IsNotNull(request.inputs);
        Assert.IsNotNull(request.files);
    }
    
    [Test]
    public void IsValid_WithQueryAndUser_ReturnsTrue()
    {
        var request = new DifyApiRequest 
        { 
            query = "こんにちは", 
            user = "test-user" 
        };
        Assert.IsTrue(request.IsValid());
    }
    
    [TestCase("", "user")]
    [TestCase("query", "")]
    [TestCase("", "")]
    public void IsValid_MissingRequiredFields_ReturnsFalse(string query, string user)
    {
        var request = new DifyApiRequest { query = query, user = user };
        Assert.IsFalse(request.IsValid());
    }
}
```

---

## 📋 **Phase 2: Infrastructure レイヤー** (TDD Ready)

### **Interface定義**
```csharp
// Scripts/Services/Dify/Interfaces/IDifyApiClient.cs
public interface IDifyApiClient
{
    Task<string> SendChatMessageStreamAsync(DifyApiRequest request, CancellationToken cancellationToken = default);
    Task<DifyApiRequest> CreateRequestAsync(string query, string userId);
}

// Scripts/Services/Dify/Interfaces/IAudioStreamHandler.cs
public interface IAudioStreamHandler
{
    byte[] ProcessBase64Audio(string base64Audio);
    bool ValidateAudioFormat(byte[] audioData);
    bool IsValidMP3Header(byte[] audioData);
}
```

### **SSEParser実装**
```csharp
// Scripts/Services/Dify/Infrastructure/SSEParser.cs
public static class SSEParser
{
    private const string SSE_DATA_PREFIX = "data: ";
    
    public static IEnumerable<DifyStreamEvent> ParseSSEData(string rawSSEData)
    {
        if (string.IsNullOrEmpty(rawSSEData))
            yield break;
            
        var lines = rawSSEData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (!IsValidSSELine(line)) continue;
            
            var jsonData = ExtractDataFromSSELine(line);
            if (!string.IsNullOrEmpty(jsonData))
            {
                var streamEvent = JsonUtility.FromJson<DifyStreamEvent>(jsonData);
                if (streamEvent != null)
                    yield return streamEvent;
            }
        }
    }
    
    public static bool IsValidSSELine(string line)
    {
        return line != null && line.StartsWith(SSE_DATA_PREFIX);
    }
    
    public static string ExtractDataFromSSELine(string line)
    {
        return IsValidSSELine(line) ? line.Substring(SSE_DATA_PREFIX.Length).Trim() : string.Empty;
    }
}
```

### **HTTP Client実装**
```csharp
// Scripts/Services/Dify/Infrastructure/DifyApiClient.cs
public class DifyApiClient : IDifyApiClient
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string apiKey;
    
    public DifyApiClient(string baseUrl, string apiKey)
    {
        this.baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        
        this.httpClient = new HttpClient();
        this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    public async Task<string> SendChatMessageStreamAsync(DifyApiRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!request.IsValid()) throw new ArgumentException("Invalid request", nameof(request));
        
        var json = JsonUtility.ToJson(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await httpClient.PostAsync($"{baseUrl}/v1/chat-messages", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new DifyApiException($"API request failed: {ex.Message}", ex);
        }
    }
    
    public async Task<DifyApiRequest> CreateRequestAsync(string query, string userId)
    {
        return new DifyApiRequest
        {
            query = query,
            user = userId,
            response_mode = "streaming"
        };
    }
}

// Scripts/Services/Dify/Exceptions/DifyApiException.cs
public class DifyApiException : Exception
{
    public DifyApiException(string message) : base(message) { }
    public DifyApiException(string message, Exception innerException) : base(message, innerException) { }
}
```

---

## 📋 **Phase 3: Service レイヤー** (TDD Ready)

### **DifyService実装**
```csharp
// Scripts/Services/Dify/DifyService.cs
public class DifyService
{
    private readonly IDifyApiClient apiClient;
    private readonly IAudioStreamHandler audioHandler;
    
    public DifyService(IDifyApiClient apiClient, IAudioStreamHandler audioHandler)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.audioHandler = audioHandler ?? throw new ArgumentNullException(nameof(audioHandler));
    }
    
    public async Task<DifyProcessingResult> ProcessCommentAsync(string comment, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. リクエスト作成
            var request = await apiClient.CreateRequestAsync(comment, userId);
            
            // 2. API呼び出し
            var rawSSEData = await apiClient.SendChatMessageStreamAsync(request, cancellationToken);
            
            // 3. SSEパース
            var events = SSEParser.ParseSSEData(rawSSEData);
            
            // 4. 結果構築
            return BuildProcessingResult(events);
        }
        catch (Exception ex)
        {
            return new DifyProcessingResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    private DifyProcessingResult BuildProcessingResult(IEnumerable<DifyStreamEvent> events)
    {
        var result = new DifyProcessingResult { IsSuccess = true };
        var textBuilder = new StringBuilder();
        
        foreach (var evt in events)
        {
            if (evt.IsTextMessage && !string.IsNullOrEmpty(evt.answer))
            {
                textBuilder.Append(evt.answer);
                result.ConversationId = evt.conversation_id;
                result.MessageId = evt.message_id;
            }
            else if (evt.IsTTSMessage && !string.IsNullOrEmpty(evt.audio))
            {
                var audioBytes = audioHandler.ProcessBase64Audio(evt.audio);
                if (audioHandler.ValidateAudioFormat(audioBytes))
                {
                    result.AudioChunks.Add(audioBytes);
                }
            }
        }
        
        result.TextResponse = textBuilder.ToString();
        return result;
    }
}
```

---

## 📋 **Phase 4: Unity統合アダプター** (最小限のMonoBehaviour)

### **DifyQueueManager実装**
```csharp
// Scripts/Components/DifyQueueManager.cs
public class DifyQueueManager : MonoBehaviour
{
    [Header("Dify統合設定")]
    [SerializeField] private bool enableDifyIntegration = false;
    [SerializeField] private float processingInterval = 2.0f;
    
    [Header("デバッグ設定")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool logApiResponses = false;
    
    private DifyService difyService;
    private ConcurrentQueue<CommentData> difyInputQueue;
    private CancellationTokenSource cancellationTokenSource;
    
    // Dependency Injection
    void Start()
    {
        InitializeDifyService();
        difyInputQueue = new ConcurrentQueue<CommentData>();
        cancellationTokenSource = new CancellationTokenSource();
        
        if (enableDifyIntegration)
        {
            StartCoroutine(ProcessQueuePeriodically());
        }
    }
    
    private void InitializeDifyService()
    {
        var apiClient = new DifyApiClient(Constants.DIFY_BASE_URL, Constants.DIFY_API_KEY);
        var audioHandler = new AudioStreamHandler();
        difyService = new DifyService(apiClient, audioHandler);
    }
    
    public void AddComment(CommentData comment)
    {
        if (enableDifyIntegration)
        {
            difyInputQueue.Enqueue(comment);
        }
    }
    
    private IEnumerator ProcessQueuePeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(processingInterval);
            
            if (difyInputQueue.TryDequeue(out CommentData comment))
            {
                _ = ProcessCommentAsync(comment);
            }
        }
    }
    
    private async UniTask ProcessCommentAsync(CommentData comment)
    {
        try
        {
            var result = await difyService.ProcessCommentAsync(
                comment.Text, 
                comment.UserName, 
                cancellationTokenSource.Token
            );
            
            if (result.IsSuccess)
            {
                // Unity固有処理（AudioClip再生等）
                await HandleSuccessfulResponse(result);
            }
            else
            {
                LogError($"Dify processing failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error in ProcessCommentAsync: {ex.Message}");
        }
    }
    
    private async UniTask HandleSuccessfulResponse(DifyProcessingResult result)
    {
        // テキスト表示更新
        UpdateTextDisplay(result.TextResponse);
        
        // 音声再生
        foreach (var audioChunk in result.AudioChunks)
        {
            await PlayAudioChunk(audioChunk);
        }
    }
    
    private void LogError(string message)
    {
        if (debugMode)
        {
            Debug.LogError($"[DifyQueueManager] {message}");
        }
    }
}
```

---

## 🧪 **TDDテスト戦略詳細**

### **テストディレクトリ構成**
```
Tests/
├── Editor/
│   ├── DifyDataTests.cs           # データクラステスト
│   ├── SSEParserTests.cs          # SSEパーサーテスト
│   ├── AudioStreamHandlerTests.cs # 音声処理テスト
│   ├── DifyApiClientTests.cs      # HTTP通信テスト（モック使用）
│   ├── DifyServiceTests.cs        # サービステスト（モック使用）
│   └── DifyIntegrationTests.cs    # 統合テスト（実API使用）
└── Runtime/
    └── DifyUnityAdapterTests.cs   # Unity Adapterテスト
```

### **テスト実装例**
```csharp
// Tests/Editor/SSEParserTests.cs
[TestFixture]
public class SSEParserTests
{
    [Test]
    public void ParseSSEData_ValidSingleEvent_ReturnsOneEvent()
    {
        // Arrange
        var sseData = "data: {\"event\":\"message\",\"answer\":\"こんにちは\"}";
        
        // Act
        var events = SSEParser.ParseSSEData(sseData).ToList();
        
        // Assert
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("message", events[0].@event);
        Assert.AreEqual("こんにちは", events[0].answer);
    }
    
    [Test]
    public void ParseSSEData_MultipleEvents_ReturnsAllEvents()
    {
        var sseData = "data: {\"event\":\"message\",\"answer\":\"Hello\"}\ndata: {\"event\":\"tts_message\",\"audio\":\"base64data\"}";
        
        var events = SSEParser.ParseSSEData(sseData).ToList();
        
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("message", events[0].@event);
        Assert.AreEqual("tts_message", events[1].@event);
    }
    
    [Test]
    public void ParseSSEData_InvalidJSON_SkipsInvalidEvents()
    {
        var sseData = "data: {invalid json}\ndata: {\"event\":\"message\",\"answer\":\"valid\"}";
        
        var events = SSEParser.ParseSSEData(sseData).ToList();
        
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("message", events[0].@event);
    }
}

// Tests/Editor/DifyServiceTests.cs (モック使用)
[TestFixture]
public class DifyServiceTests
{
    private Mock<IDifyApiClient> mockApiClient;
    private Mock<IAudioStreamHandler> mockAudioHandler;
    private DifyService difyService;
    
    [SetUp]
    public void SetUp()
    {
        mockApiClient = new Mock<IDifyApiClient>();
        mockAudioHandler = new Mock<IAudioStreamHandler>();
        difyService = new DifyService(mockApiClient.Object, mockAudioHandler.Object);
    }
    
    [Test]
    public async Task ProcessCommentAsync_ValidComment_ReturnsSuccessResult()
    {
        // Arrange
        var comment = "こんにちは";
        var userId = "test-user";
        var mockSSEResponse = "data: {\"event\":\"message\",\"answer\":\"こんにちは、元気ですか？\"}";
        
        mockApiClient.Setup(x => x.CreateRequestAsync(comment, userId))
                    .ReturnsAsync(new DifyApiRequest { query = comment, user = userId });
        
        mockApiClient.Setup(x => x.SendChatMessageStreamAsync(It.IsAny<DifyApiRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockSSEResponse);
        
        // Act
        var result = await difyService.ProcessCommentAsync(comment, userId);
        
        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("こんにちは、元気ですか？", result.TextResponse);
        mockApiClient.Verify(x => x.SendChatMessageStreamAsync(It.IsAny<DifyApiRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## 🚀 **TDD開発手順**

### **1. 開発サイクル**
```
1. Red   → テスト作成 → 失敗確認
2. Green → 最小実装 → テスト通過
3. Refactor → リファクタリング → テスト保持
```

### **2. 実装順序**
1. **データクラス** + Unit Tests
2. **SSEParser** + Unit Tests
3. **AudioStreamHandler** + Unit Tests
4. **DifyApiClient** + Unit Tests (Mock使用)
5. **DifyService** + Unit Tests (Mock使用)
6. **統合テスト** (実際のDify API)
7. **Unity Adapter** (最小限)

### **3. CI/CD準備**
```csharp
// Tests/Editor/DifyIntegrationTests.cs
[TestFixture]
public class DifyIntegrationTests
{
    [Test]
    [Category("Integration")]
    public async Task DifyService_RealAPI_ProcessesCommentSuccessfully()
    {
        // 実際のDify APIを使用
        // CI環境でのみ実行
        Assume.That(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") == "true");
        
        var apiClient = new DifyApiClient("http://localhost", "app-LuO5iGAR5Q5XGhkT7xF0zjFW");
        var audioHandler = new AudioStreamHandler();
        var service = new DifyService(apiClient, audioHandler);
        
        var result = await service.ProcessCommentAsync("こんにちは", "integration-test-user");
        
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotEmpty(result.TextResponse);
    }
}
```

## 📊 **パフォーマンス指標**

### **テスト実行時間目標**
- Unit Tests: < 1秒
- Integration Tests: < 10秒
- 全体テスト: < 30秒

### **カバレッジ目標**
- Pure C# Layer: 90%以上
- Service Layer: 85%以上
- Unity Adapter: 70%以上

---

## 📝 **更新履歴**
- 2025-06-20: TDD対応プラン作成
- 2025-06-20: Pure C# + Interface設計追加
- 2025-06-20: テスト戦略詳細化