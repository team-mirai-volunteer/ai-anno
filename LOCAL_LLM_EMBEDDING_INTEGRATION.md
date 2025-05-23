# ローカルLLM埋め込み統合ガイド

## 概要
このドキュメントでは、ai-anno-2024リポジトリにおいて、現在使用されているGoogle Generative AI Embeddings（`models/text-embedding-004`）をローカルLLM埋め込み（SentenceTransformer）に置き換える方法について説明します。この方法により、APIキーなしで無料かつプライベートな埋め込み生成が可能になります。

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

## ローカルLLM埋め込み（SentenceTransformer）への移行

### 1. 依存関係の追加
`pyproject.toml`に以下の依存関係を追加します：
```toml
[tool.poetry.dependencies]
sentence-transformers = "^4.1.0"
```

依存関係をインストールします：
```bash
poetry add sentence-transformers
```

### 2. 埋め込みラッパークラスの作成
新しいファイルを作成して、埋め込みプロバイダーを簡単に切り替えられるようにします：

```python
# python_server/src/embeddings/provider.py
import os
from enum import Enum
from typing import Optional, List
from langchain_google_genai import GoogleGenerativeAIEmbeddings
from sentence_transformers import SentenceTransformer
from langchain_core.embeddings import Embeddings

class EmbeddingProviderType(str, Enum):
    """埋め込みプロバイダーの種類"""
    GOOGLE = "google"
    LOCAL = "local"

class SentenceTransformerEmbeddings(Embeddings):
    """SentenceTransformerをLangChainのEmbeddingsとして使用するためのラッパークラス"""
    
    def __init__(self, model_name: str = "paraphrase-multilingual-mpnet-base-v2"):
        """初期化"""
        self.model = SentenceTransformer(model_name)
        
    def embed_documents(self, texts: List[str]) -> List[List[float]]:
        """文書の埋め込みを取得"""
        return self.model.encode(texts).tolist()
        
    def embed_query(self, text: str) -> List[float]:
        """クエリの埋め込みを取得"""
        return self.model.encode(text).tolist()

class EmbeddingProvider:
    """埋め込みプロバイダークラス"""
    
    def __init__(self, provider_type: Optional[EmbeddingProviderType] = None):
        """初期化"""
        if provider_type is None:
            # デフォルトはローカル
            provider_type = EmbeddingProviderType.LOCAL
            
        self.provider_type = provider_type
        
        if provider_type == EmbeddingProviderType.GOOGLE:
            # Googleの埋め込み
            self.embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")
        elif provider_type == EmbeddingProviderType.LOCAL:
            # ローカルの埋め込み
            model_name = os.environ.get("LOCAL_EMBEDDING_MODEL", "paraphrase-multilingual-mpnet-base-v2")
            self.embeddings = SentenceTransformerEmbeddings(model_name=model_name)
        else:
            raise ValueError(f"Unsupported provider type: {provider_type}")
            
    def embed_documents(self, texts: List[str]) -> List[List[float]]:
        """文書の埋め込みを取得"""
        return self.embeddings.embed_documents(texts)
        
    def embed_query(self, text: str) -> List[float]:
        """クエリの埋め込みを取得"""
        return self.embeddings.embed_query(text)
```

### 3. FAISS知識ベース作成スクリプトの修正
`python_server/src/cli/save_faiss_knowledge_db.py`を以下のように修正します：

```python
# python_server/src/cli/save_faiss_knowledge_db.py
from src.embeddings.provider import EmbeddingProvider, EmbeddingProviderType

# 変更前:
# embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")

# 変更後:
# 環境変数EMBEDDING_PROVIDERを使用してプロバイダーを選択
provider_type = os.environ.get("EMBEDDING_PROVIDER", "local")
embeddings = EmbeddingProvider(
    EmbeddingProviderType(provider_type) if provider_type in ["google", "local"] else None
).embeddings
```

### 4. Q&A用FAISSデータベース作成スクリプトの修正
`python_server/src/cli/save_faiss_db.py`も同様の修正を行います：

```python
# python_server/src/cli/save_faiss_db.py
from src.embeddings.provider import EmbeddingProvider, EmbeddingProviderType

# 変更前:
# embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")

# 変更後:
provider_type = os.environ.get("EMBEDDING_PROVIDER", "local")
embeddings = EmbeddingProvider(
    EmbeddingProviderType(provider_type) if provider_type in ["google", "local"] else None
).embeddings
```

### 5. 検索処理の修正
`python_server/src/get_faiss_vector.py`の各関数も同様に修正します：

```python
# python_server/src/get_faiss_vector.py
from src.embeddings.provider import EmbeddingProvider, EmbeddingProviderType

# 変更前:
# embeddings = GoogleGenerativeAIEmbeddings(model="models/text-embedding-004")

# 変更後:
provider_type = os.environ.get("EMBEDDING_PROVIDER", "local")
embeddings = EmbeddingProvider(
    EmbeddingProviderType(provider_type) if provider_type in ["google", "local"] else None
).embeddings
```

## ローカルLLM埋め込みの利点

### 利点
- **コスト削減**: API呼び出し料金が発生しない
- **プライバシー保護**: データがローカル環境から外部に送信されない
- **APIキー不要**: 外部サービスの認証情報が不要
- **レイテンシ削減**: ネットワーク遅延がない
- **自己ホスティング**: インターネット接続がなくても動作可能

### 注意点
- ローカル環境のリソース（メモリ、CPU/GPU）を消費する
- モデルのダウンロードに時間がかかる（初回のみ）
- 最新の最先端モデルへのアクセスが制限される可能性がある

## 移行手順

1. 依存関係のインストール：`poetry add sentence-transformers`
2. 埋め込みプロバイダークラスの実装
3. 各スクリプトの修正
4. 既存のFAISSデータベースの再作成：
   ```
   poetry run python -m src.cli.import_pdf
   poetry run python -m src.cli.import_docs_csv
   poetry run python -m src.cli.save_faiss_knowledge_db
   poetry run python -m src.cli.save_faiss_db
   ```

## モデル比較

| モデル | タイプ | 次元数 | 多言語対応 | 推奨用途 |
|-------|-------|-------|----------|--------|
| paraphrase-multilingual-mpnet-base-v2 | ローカル | 768 | 多言語対応 | 一般的な多言語用途 |
| all-MiniLM-L6-v2 | ローカル | 384 | 英語中心 | 軽量・高速処理 |
| all-mpnet-base-v2 | ローカル | 768 | 英語中心 | 高精度（英語） |
| models/text-embedding-004 | Google | 768 | 多言語対応 | 現在の実装 |

## 実装例（参考リポジトリ）

このアプローチは[experiment_embed_aggregation](https://github.com/nishio/experiment_embed_aggregation)リポジトリを参考にしています。このリポジトリでは、SentenceTransformerを使用して埋め込みベクトルを生成し、クラスタリングによる類似テキスト検出を行っています。

主な特徴：
- SentenceTransformerの「paraphrase-multilingual-mpnet-base-v2」モデルを使用
- 768次元の埋め込みベクトルを生成
- 階層的クラスタリング（AgglomerativeClustering）による類似テキストのグループ化
- 処理時間：埋め込み生成とクラスタリングで約8分
