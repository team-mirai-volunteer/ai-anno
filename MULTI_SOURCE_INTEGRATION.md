# 複数データソース統合ガイド

## 概要
このドキュメントでは、ai-annoリポジトリにおいて、PDFだけでなく、YouTube、Twitter、noteなど複数のデータソースからコンテンツを取得し、FAISSデータベースに統合する方法について説明します。

## 現在のデータソース処理
現在のシステムでは、主に以下のデータソースが使用されています：

1. PDFファイル（マニフェストスライド）
   ```python
   # python_server/src/cli/import_pdf.py
   """PDF を画像に変換したうえで unity から参照できる path に配置する"""
   ```

2. テキストファイル
   ```python
   # python_server/src/cli/import_docs_csv.py
   """PDFをテキストにしてcsvにまとめる"""
   ```

データ処理フローは以下の通りです：
1. PDFをスライド画像に変換（`import_pdf.py`）
2. PDFからテキストを抽出し、CSVファイルに保存（`import_docs_csv.py`）
3. CSVファイルからFAISSデータベースを作成（`save_faiss_knowledge_db.py`）

## 複数データソース統合のアーキテクチャ

### 1. データソースインターフェースの定義
データソースを抽象化するインターフェースを定義します：

```python
# python_server/src/data_sources/base.py
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional
from pydantic import BaseModel
from langchain.schema import Document

class SourceMetadata(BaseModel):
    """ソースメタデータ"""
    source_type: str
    source_id: str
    title: str
    url: Optional[str] = None
    image_path: Optional[str] = None
    timestamp: Optional[str] = None
    additional_info: Optional[Dict[str, Any]] = None

class DataSource(ABC):
    """データソースの基底クラス"""
    
    @abstractmethod
    async def fetch_data(self) -> List[Document]:
        """データを取得してDocumentリストに変換する"""
        pass
        
    @abstractmethod
    def get_source_type(self) -> str:
        """データソースのタイプを返す"""
        pass
        
    @abstractmethod
    def get_source_name(self) -> str:
        """データソースの名前を返す"""
        pass
```

### 2. PDF/テキストデータソースの実装
既存のPDF/テキスト処理を新しいアーキテクチャに適合させます：

```python
# python_server/src/data_sources/pdf_source.py
import os
import fitz  # PyMuPDF
from typing import List, Optional
from langchain.schema import Document
from src.data_sources.base import DataSource, SourceMetadata

class PDFDataSource(DataSource):
    """PDFからデータを取得するクラス"""
    
    def __init__(self, pdf_path: str, output_dir: Optional[str] = None):
        self.pdf_path = pdf_path
        self.output_dir = output_dir or "Assets/Resources/Slides/manifest_demo_PDF"
        
    async def fetch_data(self) -> List[Document]:
        """PDFからデータを取得する"""
        documents = []
        
        # PDFが存在しない場合は空リストを返す
        if not os.path.exists(self.pdf_path):
            print(f"PDF file not found: {self.pdf_path}")
            return documents
            
        # PDFを開く
        pdf = fitz.open(self.pdf_path)
        
        # 出力ディレクトリを作成
        os.makedirs(self.output_dir, exist_ok=True)
        
        # 各ページを処理
        for i, page in enumerate(pdf):
            # テキストを抽出
            text = page.get_text()
            
            # スライド画像を保存
            image_path = f"{self.output_dir}/slide_{i+1}.png"
            pix = page.get_pixmap(matrix=fitz.Matrix(300/72, 300/72))
            pix.save(image_path)
            
            # メタデータを設定
            metadata = SourceMetadata(
                source_type="pdf",
                source_id=self.pdf_path,
                title=f"Slide {i+1}",
                image_path=image_path
            )
            
            # Documentを作成
            document = Document(page_content=text, metadata=metadata.dict())
            documents.append(document)
            
        return documents
        
    def get_source_type(self) -> str:
        return "pdf"
        
    def get_source_name(self) -> str:
        return os.path.basename(self.pdf_path)
```

### 3. YouTubeデータソースの実装
YouTubeからデータを取得するクラスを実装します：

```python
# python_server/src/data_sources/youtube_source.py
import os
from typing import List, Optional
from langchain.schema import Document
from googleapiclient.discovery import build
from youtube_transcript_api import YouTubeTranscriptApi
from src.data_sources.base import DataSource, SourceMetadata

class YouTubeDataSource(DataSource):
    """YouTubeからデータを取得するクラス"""
    
    def __init__(self, channel_id: Optional[str] = None, video_ids: Optional[List[str]] = None):
        self.api_key = os.environ.get("YOUTUBE_API_KEY")
        self.channel_id = channel_id
        self.video_ids = video_ids or []
        
    async def fetch_data(self) -> List[Document]:
        """YouTubeからデータを取得する"""
        documents = []
        
        if not self.api_key:
            print("YouTube API key not found")
            return documents
            
        # チャンネルIDが指定されている場合、そのチャンネルの動画を取得
        if self.channel_id:
            self.video_ids.extend(self._get_channel_videos())
            
        # 各動画の字幕と情報を取得
        for video_id in self.video_ids:
            # 動画情報を取得
            video_info = self._get_video_info(video_id)
            if not video_info:
                continue
                
            title = video_info.get("title", f"YouTube Video {video_id}")
            
            # 字幕を取得
            transcript = self._get_video_transcript(video_id)
            if not transcript:
                continue
                
            # サムネイル画像を保存
            thumbnail_url = f"https://img.youtube.com/vi/{video_id}/0.jpg"
            image_path = f"Assets/Resources/YouTube/{video_id}.jpg"
            os.makedirs(os.path.dirname(image_path), exist_ok=True)
            self._download_image(thumbnail_url, image_path)
            
            # メタデータを設定
            metadata = SourceMetadata(
                source_type="youtube",
                source_id=video_id,
                title=title,
                url=f"https://www.youtube.com/watch?v={video_id}",
                image_path=image_path,
                timestamp=video_info.get("publishedAt"),
                additional_info={
                    "channel_title": video_info.get("channelTitle"),
                    "description": video_info.get("description")
                }
            )
            
            # Documentを作成
            document = Document(page_content=transcript, metadata=metadata.dict())
            documents.append(document)
            
        return documents
        
    def get_source_type(self) -> str:
        return "youtube"
        
    def get_source_name(self) -> str:
        return f"YouTube Channel: {self.channel_id}" if self.channel_id else f"YouTube Videos: {len(self.video_ids)}"
        
    def _get_channel_videos(self) -> List[str]:
        """チャンネルの動画IDリストを取得"""
        youtube = build("youtube", "v3", developerKey=self.api_key)
        request = youtube.search().list(
            part="id",
            channelId=self.channel_id,
            maxResults=50,
            type="video"
        )
        response = request.execute()
        return [item["id"]["videoId"] for item in response.get("items", [])]
        
    def _get_video_info(self, video_id: str) -> Optional[dict]:
        """動画の情報を取得"""
        youtube = build("youtube", "v3", developerKey=self.api_key)
        request = youtube.videos().list(
            part="snippet",
            id=video_id
        )
        response = request.execute()
        items = response.get("items", [])
        return items[0]["snippet"] if items else None
        
    def _get_video_transcript(self, video_id: str) -> Optional[str]:
        """動画の字幕を取得"""
        try:
            transcript_list = YouTubeTranscriptApi.get_transcript(video_id, languages=["ja"])
            return " ".join([item["text"] for item in transcript_list])
        except Exception as e:
            print(f"Error getting transcript for video {video_id}: {e}")
            return None
            
    def _download_image(self, url: str, path: str) -> None:
        """画像をダウンロードして保存"""
        import requests
        try:
            response = requests.get(url)
            with open(path, "wb") as f:
                f.write(response.content)
        except Exception as e:
            print(f"Error downloading image: {e}")
```

### 4. Twitterデータソースの実装
Twitterからデータを取得するクラスを実装します：

```python
# python_server/src/data_sources/twitter_source.py
import os
import requests
from typing import List, Optional
from langchain.schema import Document
from src.data_sources.base import DataSource, SourceMetadata

class TwitterDataSource(DataSource):
    """Twitterからデータを取得するクラス"""
    
    def __init__(self, user_names: Optional[List[str]] = None, hashtags: Optional[List[str]] = None):
        self.bearer_token = os.environ.get("TWITTER_BEARER_TOKEN")
        self.user_names = user_names or []
        self.hashtags = hashtags or []
        
    async def fetch_data(self) -> List[Document]:
        """Twitterからデータを取得する"""
        documents = []
        
        if not self.bearer_token:
            print("Twitter Bearer Token not found")
            return documents
            
        # ユーザー名が指定されている場合、そのユーザーのツイートを取得
        for user_name in self.user_names:
            tweets = self._get_user_tweets(user_name)
            documents.extend(self._convert_tweets_to_documents(tweets, f"user:{user_name}"))
            
        # ハッシュタグが指定されている場合、そのハッシュタグのツイートを取得
        for hashtag in self.hashtags:
            tweets = self._get_hashtag_tweets(hashtag)
            documents.extend(self._convert_tweets_to_documents(tweets, f"hashtag:{hashtag}"))
            
        return documents
        
    def get_source_type(self) -> str:
        return "twitter"
        
    def get_source_name(self) -> str:
        users = f"Users: {','.join(self.user_names)}" if self.user_names else ""
        hashtags = f"Hashtags: {','.join(self.hashtags)}" if self.hashtags else ""
        return f"Twitter {users} {hashtags}".strip()
        
    def _get_user_tweets(self, user_name: str) -> List[dict]:
        """ユーザーのツイートを取得"""
        # ここにTwitter APIを使用したツイート取得処理を実装
        # 実際にはTwitter API v2を使用することになります
        # この例では実装を省略しています
        return []
        
    def _get_hashtag_tweets(self, hashtag: str) -> List[dict]:
        """ハッシュタグのツイートを取得"""
        # ここにTwitter APIを使用したツイート取得処理を実装
        # 実際にはTwitter API v2を使用することになります
        # この例では実装を省略しています
        return []
        
    def _convert_tweets_to_documents(self, tweets: List[dict], source_id: str) -> List[Document]:
        """ツイートをDocumentに変換"""
        documents = []
        
        for tweet in tweets:
            # メタデータを設定
            metadata = SourceMetadata(
                source_type="twitter",
                source_id=source_id,
                title=f"Tweet by {tweet.get('author')}",
                url=f"https://twitter.com/user/status/{tweet.get('id')}",
                timestamp=tweet.get("created_at"),
                additional_info={
                    "author": tweet.get("author"),
                    "likes": tweet.get("likes"),
                    "retweets": tweet.get("retweets")
                }
            )
            
            # Documentを作成
            document = Document(page_content=tweet.get("text", ""), metadata=metadata.dict())
            documents.append(document)
            
        return documents
```

### 5. データソースマネージャーの実装
複数のデータソースを管理するクラスを実装します：

```python
# python_server/src/data_sources/manager.py
import os
import pandas as pd
from typing import List, Dict, Any, Optional
from langchain.schema import Document
from src.data_sources.base import DataSource

class DataSourceManager:
    """複数のデータソースを管理するクラス"""
    
    def __init__(self):
        self.data_sources = []
        
    def add_data_source(self, data_source: DataSource):
        """データソースを追加する"""
        self.data_sources.append(data_source)
        
    async def fetch_all_data(self) -> List[Document]:
        """全てのデータソースからデータを取得する"""
        all_documents = []
        for source in self.data_sources:
            print(f"Fetching data from {source.get_source_name()}...")
            documents = await source.fetch_data()
            all_documents.extend(documents)
            print(f"  Got {len(documents)} documents")
        return all_documents
        
    def save_to_csv(self, documents: List[Document], output_path: str):
        """DocumentリストをCSVファイルに保存する"""
        # 出力ディレクトリを作成
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        
        # CSVデータを作成
        rows = []
        for doc in documents:
            # 必要なデータを抽出
            title = doc.metadata.get("title", "")
            text = doc.page_content
            image_path = doc.metadata.get("image_path", "")
            
            # 行を追加
            rows.append([title, text, image_path])
            
        # CSVファイルに書き込み
        df = pd.DataFrame(rows, columns=["title", "text", "filename"])
        df.to_csv(output_path, index=False, quoting=1)  # quoting=1はQUOTE_ALL
        
        print(f"Saved {len(documents)} documents to {output_path}")
```

### 6. 新しいインポートスクリプトの作成
複数のデータソースからデータを取得するスクリプトを作成します：

```python
# python_server/src/cli/import_multi_sources.py
import os
import asyncio
import click
from src.data_sources.manager import DataSourceManager
from src.data_sources.pdf_source import PDFDataSource
from src.data_sources.youtube_source import YouTubeDataSource
from src.data_sources.twitter_source import TwitterDataSource

@click.command()
@click.option("--pdf-path", help="Path to PDF file")
@click.option("--youtube-channel", help="YouTube channel ID")
@click.option("--youtube-videos", help="Comma-separated YouTube video IDs")
@click.option("--twitter-users", help="Comma-separated Twitter user names")
@click.option("--twitter-hashtags", help="Comma-separated Twitter hashtags")
@click.option("--output", default="faiss_knowledge/multi_sources.csv", help="Output CSV file path")
def main(pdf_path, youtube_channel, youtube_videos, twitter_users, twitter_hashtags, output):
    """複数のデータソースからデータを取得し、CSVファイルに保存する"""
    # データソースマネージャーを作成
    manager = DataSourceManager()
    
    # PDFデータソースを追加
    if pdf_path:
        manager.add_data_source(PDFDataSource(pdf_path))
        
    # YouTubeデータソースを追加
    if youtube_channel or youtube_videos:
        video_ids = youtube_videos.split(",") if youtube_videos else None
        manager.add_data_source(YouTubeDataSource(youtube_channel, video_ids))
        
    # Twitterデータソースを追加
    if twitter_users or twitter_hashtags:
        user_names = twitter_users.split(",") if twitter_users else None
        hashtags = twitter_hashtags.split(",") if twitter_hashtags else None
        manager.add_data_source(TwitterDataSource(user_names, hashtags))
        
    # データを取得
    documents = asyncio.run(manager.fetch_all_data())
    
    # CSVファイルに保存
    manager.save_to_csv(documents, output)
    
    print(f"Import completed. Total documents: {len(documents)}")

if __name__ == "__main__":
    main()
```

## 使用方法

### 1. 環境変数の設定
`.env`ファイルに以下の変数を追加します：
```
YOUTUBE_API_KEY=your_youtube_api_key
TWITTER_BEARER_TOKEN=your_twitter_bearer_token
```

### 2. 依存関係の追加
`pyproject.toml`に以下の依存関係を追加します：
```toml
[tool.poetry.dependencies]
google-api-python-client = "^2.0.0"
youtube-transcript-api = "^0.6.0"
requests = "^2.31.0"
```

### 3. 複数データソースからのインポート
以下のコマンドを実行して、複数のデータソースからデータを取得します：
```bash
# PDFとYouTubeからデータを取得
poetry run python -m src.cli.import_multi_sources --pdf-path=path/to/manifest.pdf --youtube-channel=CHANNEL_ID

# YouTubeとTwitterからデータを取得
poetry run python -m src.cli.import_multi_sources --youtube-videos=VIDEO_ID1,VIDEO_ID2 --twitter-users=USER1,USER2

# 全てのデータソースからデータを取得
poetry run python -m src.cli.import_multi_sources --pdf-path=path/to/manifest.pdf --youtube-channel=CHANNEL_ID --twitter-users=USER1,USER2
```

### 4. FAISSデータベースの作成
通常のFAISSデータベース作成コマンドを実行します：
```bash
poetry run python -m src.cli.save_faiss_knowledge_db
```

## 注意点
- 各データソースのAPIキーが必要です
- YouTubeの字幕が利用できない場合は、音声認識サービスを使用する必要があるかもしれません
- Twitterデータの取得には、Twitter API v2のアクセス権が必要です
- 大量のデータを取得する場合は、APIの制限に注意してください
