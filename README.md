<p align="center">
  <img src="./assets/logo.png" alt="localllama logo" width="220" />
</p>

<h1 align="center">localllama</h1>

<p align="center">
  A private AI chat app that runs local GGUF models on your own device.
</p>

<p align="center">
  Chat locally. Keep your history. Add your documents. Stay in control.
</p>

<p align="center">
  <img alt=".NET MAUI" src="https://img.shields.io/badge/.NET-MAUI-512BD4?style=for-the-badge" />
  <img alt="C#" src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" />
  <img alt="SQLite" src="https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
  <img alt="LLamaSharp" src="https://img.shields.io/badge/LLamaSharp-Local%20Inference-0F172A?style=for-the-badge" />
</p>

## What is localllama?

`localllama` is a local AI chat application built for people who want a simple, private, and practical way to run language models without depending on a cloud chatbot for the core experience.

You can sign in, load a local `.gguf` model, start conversations, save your chat history, and even add your own documents so the app can use them as extra context during replies.

## Why use it?

- Private-first experience with local model execution
- Persistent chat history saved on your device
- Support for personal document context through local RAG
- Simple model import and selection
- Optional web search when you want extra online context
- Multi-user support with separated local data

## Main features

- Chat with local GGUF models
- Create and reopen conversations
- Automatically name chats from the first message
- Import and manage local models
- Download models from a remote catalog
- Add `.txt`, `.md`, `.json`, and `.pdf` files to your knowledge base
- Enable optional web search with Tavily
- Manage separate local databases per user

## How it works

1. Create an account or sign in.
2. Import a `.gguf` model or download one from the built-in model catalog.
3. Start a new conversation.
4. Optionally add documents to the local RAG library.
5. Ask questions and chat fully on your own device.

## Screenshots

<img width="1229" height="763" alt="localllama main view" src="https://github.com/user-attachments/assets/c1fd562c-a9ab-4721-8afe-d91908b458da" />

<img width="1512" height="952" alt="localllama screenshot 1" src="https://github.com/user-attachments/assets/079f5727-b98c-4bc3-afe0-f2ab643dc9a9" />
<img width="1512" height="952" alt="localllama screenshot 2" src="https://github.com/user-attachments/assets/4e06e9bc-4bdf-4ecc-9ad1-c6e9077f58ca" />
<img width="1512" height="952" alt="localllama screenshot 3" src="https://github.com/user-attachments/assets/b62a1b93-ca03-432a-82e2-96d8fc8ea9ac" />
<img width="1512" height="952" alt="localllama screenshot 4" src="https://github.com/user-attachments/assets/30e645e5-43af-43f0-8427-16a2d9030d52" />
<img width="1512" height="952" alt="localllama screenshot 5" src="https://github.com/user-attachments/assets/f7067d8a-7c70-49f1-8127-6c238444e628" />
<img width="1512" height="952" alt="localllama screenshot 6" src="https://github.com/user-attachments/assets/7b5702a1-04c0-4c56-98e9-53b369ace601" />

## Installation

### Requirements

- A machine that supports the current target platforms
- .NET SDK with MAUI support
- MAUI workloads installed
- A compatible `.gguf` model

### Current target platforms

- macOS via `net10.0-maccatalyst`
- iOS via `net10.0-ios`
- Windows via `net10.0-windows10.0.19041.0`

### Setup

```bash
git clone https://github.com/your-user/localllama.git
cd localllama
dotnet build
```

Then launch the project from Rider or Visual Studio and run it on your target platform.

## First-time use

1. Open the app.
2. Create an account or log in.
3. Import a local `.gguf` model, or download one from the model manager.
4. Open a new chat.
5. If you want document-aware answers, import files into the document manager.
6. Start chatting.

## Supported content

### Models

- GGUF-based language models
- The code expects a default model named `llama-3.2-1b-instruct-q8_0.gguf` when available

### Documents for RAG

- `.txt`
- `.md`
- `.json`
- `.pdf`

## Privacy

The app is designed around local usage:

- Conversations are stored locally
- User data is separated by account
- Chat titles and messages are encrypted when the user encryption key is available
- Web search is optional and only used when explicitly enabled

## Notes

- Android-related files exist in the repository, but Android is not currently active in the main project target list
- Some features, such as web search, require internet access and a valid Tavily API key
- I could not verify `dotnet build` in this environment because the `dotnet` command is not available here

## Roadmap

- Better RAG retrieval with embeddings
- More polished onboarding
- Improved model management experience
- Additional platform optimization

## License

Apache-2.0 license

LocalLLama by joao malhado
