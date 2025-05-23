# OpenAI埋め込み統合ガイド

## 概要
このドキュメントでは、ai-anno-2024リポジトリにおいて、現在使用されているGoogle Generative AI Embeddings（`models/text-embedding-004`）をOpenAI Embeddingsに置き換える方法について説明します。

## 現在の埋め込み使用状況

### Google Generative AI Embeddings
FAISSデータベース作成では、Google Generative AIの埋め込みモデル（`models/text-embedding-004`）が使用されています。これは主に以下の場所で使用されています：

1. 知識ベースFAISSデータベースの作成
   ```python
   # python_server/src/cli/save_faiss_knowledge_db.py
   embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")
   ```

2. Q&A用FAISSデータベースの作成
   ```python
   # python_server/src/cli/save_faiss_db.py
   embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")
   ```

3. クエリベクトルの作成（検索時）
   ```python
   # python_server/src/get_faiss_vector.py
   embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")
   ```

## OpenAI Embeddingsへの移行

### 1. 環境変数の設定
`.env`ファイルに以下の変数を追加します：
```
OPENAI_API_KEY=your_api_key_here
OPENAI_EMBEDDING_MODEL=text-embedding-3-large
```

### 2. 依存関係の追加
`pyproject.toml`に以下の依存関係を追加します：
```toml
[tool.poetry.dependencies]
langchain-openai = "^0.1.1"  # バージョンは確認してください
```

### 3. 埋め込みラッパークラスの作成
新しいファイルを作成して、埋め込みプロバイダーを簡単に切り替えられるようにします：

```python
# python_server/src/embeddings/provider.py
import os
from enum import Enum
from typing import Optional, List
from langchain_google_genai import GoogleGenerativeAIEmbeddings
from langchain_openai import OpenAIEmbeddings

class EmbeddingProviderType(str, Enum):
    """埋め込みプロバイダーの種類"""
    GOOGLE = "google"
    OPENAI = "openai"

class EmbeddingProvider:
    """埋め込みプロバイダークラス"""
    
    def __init__(self, provider_type: Optional[EmbeddingProviderType] = None):
        """初期化"""
        if provider_type is None:
            # デフォルトはOpenAI
            provider_type = EmbeddingProviderType.OPENAI
            
        self.provider_type = provider_type
        
        if provider_type == EmbeddingProviderType.GOOGLE:
            # Googleの埋め込み
            self.embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")
        elif provider_type == EmbeddingProviderType.OPENAI:
            # OpenAIの埋め込み
            model = os.environ.get("OPENAI_EMBEDDING_MODEL", "text-embedding-3-large")
            self.embeddings = OpenAIEmbeddings(model=model)
        else:
            raise ValueError(f"Unsupported provider type: {provider_type}")
            
    def embed_documents(self, texts: List[str]) -> List[List[float]]:
        """文書の埋め込みを取得"""
        return self.embeddings.embed_documents(texts)
        
    def embed_query(self, text: str) -> List[float]:
        """クエリの埋め込みを取得"""
        return self.embeddings.embed_query(text)
```

### 4. FAISS知識ベース作成スクリプトの修正
`python_server/src/cli/save_faiss_knowledge_db.py`を以下のように修正します：

```python
# python_server/src/cli/save_faiss_knowledge_db.py
from src.embeddings.provider import EmbeddingProvider, EmbeddingProviderType

# 変更前:
# embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")

# 変更後:
# 環境変数EMBEDDING_PROVIDERを使用してプロバイダーを選択
provider_type = os.environ.get("EMBEDDING_PROVIDER", "openai")
embeddings = EmbeddingProvider(
    EmbeddingProviderType(provider_type) if provider_type in ["google", "openai"] else None
).embeddings
```

### 5. Q&A用FAISSデータベース作成スクリプトの修正
`python_server/src/cli/save_faiss_db.py`も同様の修正を行います：

```python
# python_server/src/cli/save_faiss_db.py
from src.embeddings.provider import EmbeddingProvider, EmbeddingProviderType

# 変更前:
# embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")

# 変更後:
provider_type = os.environ.get("EMBEDDING_PROVIDER", "openai")
embeddings = EmbeddingProvider(
    EmbeddingProviderType(provider_type) if provider_type in ["google", "openai"] else None
).embeddings
```

### 6. 検索処理の修正
`python_server/src/get_faiss_vector.py`の各関数も同様に修正します：

```python
# python_server/src/get_faiss_vector.py
from src.embeddings.provider import EmbeddingProvider, EmbeddingProviderType

# 変更前:
# embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")

# 変更後:
provider_type = os.environ.get("EMBEDDING_PROVIDER", "openai")
embeddings = EmbeddingProvider(
    EmbeddingProviderType(provider_type) if provider_type in ["google", "openai"] else None
).embeddings
```

## OpenAI Embeddingsの利点と注意点

### 利点
- 高品質な埋め込みベクトルを提供
- 多言語対応の優れたモデル
- LangChainとの統合が簡単
- 安定したAPI提供

### 注意点
- APIキーの管理と費用の発生
- GoogleとOpenAIの埋め込みベクトルは互換性がないため、既存のFAISSデータベースは再作成が必要
- ベクトルの次元数が異なるため、パフォーマンスに影響する可能性あり

## 移行手順

1. 環境変数の設定：`.env`ファイルに`OPENAI_API_KEY`と`OPENAI_EMBEDDING_MODEL`を追加
2. 依存関係のインストール：`poetry add langchain-openai`
3. 埋め込みプロバイダークラスの実装
4. 各スクリプトの修正
5. 既存のFAISSデータベースの再作成：
   ```
   poetry run python -m src.cli.import_pdf
   poetry run python -m src.cli.import_docs_csv
   poetry run python -m src.cli.save_faiss_knowledge_db
   poetry run python -m src.cli.save_faiss_db
   ```

## モデル比較

| モデル | 次元数 | 多言語対応 | コンテキスト長 | 推奨用途 |
|-------|-------|----------|--------------|--------|
| text-embedding-3-large | 3072 | 多言語対応 | 8191 | 高精度が必要な場合 |
| text-embedding-3-small | 1536 | 多言語対応 | 8191 | コスト効率が必要な場合 |
| models/text-embedding-004 | 768 | 多言語対応 | 4096 | 現在の実装 |
