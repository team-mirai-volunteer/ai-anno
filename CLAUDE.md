# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI Anno is an interactive AI avatar system that provides real-time responses to questions via YouTube Live chat and phone calls. It was originally deployed during the 2024 Tokyo gubernatorial election as "AI Anno" and is now being developed for 2025 activities with focus on RAG (Retrieval Augmented Generation) applications.

## Architecture

### Multi-Component System
- **python_server/**: Backend API server (FastAPI + PostgreSQL + FAISS RAG)
- **aituber_3d/**: Unity 3D application for avatar visualization and streaming
- **phonecall/**: Phone call system using Twilio/Vocode integration

### Data Flow
1. Questions come from YouTube Live chat or phone calls
2. Backend processes queries using RAG (FAISS + Google Embeddings + Gemini 1.5 Pro)
3. Generated responses include text + associated slide images
4. Unity displays avatar speaking the response with visual slides
5. Text-to-speech converts responses to audio (Azure/ElevenLabs)

## Development Commands

### Python Server (Backend)
```bash
cd python_server

# Install dependencies
poetry install

# Database operations
make db/reset                    # Reset database (drop, create, migrate)
make migration/up               # Run database migrations

# Build knowledge base
make setup/resources            # Build FAISS databases from PDFs/CSVs

# Development
make run                        # Start development server (port 7200)
make run/production             # Start production server
make streamlit                  # Start Streamlit testing interface

# Code quality
make lint                       # Run ruff linting
make fmt                        # Format code with ruff

# Testing
make test-single-question       # Test single Q&A
make test-multiple-questions    # Test multiple Q&A
make test-filter-inappropriate-comments  # Test content filtering
```

### Unity (Frontend)
- Open Unity 2022.3.29f1
- Load `aituber_3d/Assets/Scenes/SampleScene.unity`
- Press Play button to start avatar system
- Connect to backend server at `127.0.0.1:7200`

### Phone System
```bash
cd phonecall

# Setup
docker build -t vocode-telephony-app .
docker compose up

# Configure Twilio webhook to: https://your-server-url.com/inboundcall
```

## Key Technologies

### Backend Stack
- **FastAPI**: Web framework for API endpoints
- **PostgreSQL**: Database for chat message storage
- **FAISS**: Vector search for knowledge retrieval
- **Google Generative AI**: Gemini 1.5 Pro for response generation
- **Google Embeddings**: Text embedding for similarity search
- **Alembic**: Database migration management

### AI/ML Components
- **RAG Pipeline**: Knowledge base (118 policy items + 253 FAQ) + Q&A database
- **Content Filtering**: Inappropriate comment detection
- **Hallucination Detection**: Response validation against knowledge base
- **Text-to-Speech**: Azure Cognitive Services / ElevenLabs integration

### Data Sources
- **Policy Manifesto**: PDF slides converted to images + CSV metadata
- **FAQ Database**: Predefined Q&A pairs for common questions
- **Template Messages**: Fallback responses when no user comments

## Configuration

### Required Environment Variables (.env)
```bash
# Database
PG_HOST=localhost
PG_PORT=5432
PG_DATABASE=aituber_dev
PG_USER=aituber_user
PG_PASSWORD=your_password

# AI APIs
GOOGLE_API_KEY=your_google_api_key
AZURE_SPEECH_KEY=your_azure_key
ELEVENLABS_API_KEY=your_elevenlabs_key

# YouTube Integration
YT_ID=your_youtube_video_id

# Optional
GOOGLE_APPLICATION_CREDENTIALS=path/to/credentials.json
GOOGLE_DRIVE_FOLDER_ID=your_drive_folder_id
```

## Key API Endpoints

- `POST /reply`: Main Q&A endpoint (input: text, output: response + image)
- `POST /filter`: Content filtering for inappropriate comments
- `GET /youtube/chat_message`: Retrieve YouTube Live chat messages
- `POST /voice`: Text-to-speech conversion (multiple voice options)
- `GET /get_info`: RAG information retrieval for debugging
- `POST /hallucination`: Hallucination detection for responses

## Database Schema

The system uses PostgreSQL with Alembic migrations. Key tables include:
- YouTube chat message storage
- Chat message cursors for pagination
- (See alembic/versions/ for migration history)

## Knowledge Base Construction

1. **PDF Processing**: `import_pdf.py` converts PDFs to images
2. **CSV Import**: `import_docs_csv.py` processes slide metadata
3. **FAISS Building**: `save_faiss_knowledge_db.py` creates vector indexes
4. **Q&A Database**: `save_faiss_db.py` builds FAQ search index

Run `make setup/resources` to build all knowledge bases.

## Development Workflow

1. Set up PostgreSQL database and run migrations
2. Configure Google API credentials (requires billing account)
3. Build FAISS knowledge bases from provided data
4. Start backend server with `make run`
5. Test with Unity client or Streamlit interface
6. Use `make lint` and `make fmt` before committing

## License

GPL-3.0 license with special consultation available for government organizations.