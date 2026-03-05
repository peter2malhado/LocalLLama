# 🇬🇧 English Version

# 📑 Table of Contents

* [About the Project](#-about-the-project)
* [Features](#-features)
* [Architecture](#-architecture)
* [Technologies](#-technologies)
* [Screenshot](#-screenshot-1)
* [Installation](#-installation)
* [Project Structure](#-project-structure)
* [Supported Models](#-supported-models)
* [Project Goals](#-project-goals)
* [Roadmap](#-roadmap-1)
* [Contributing](#-contributing)
* [License](#-license)

---

## 📌 About the Project

**LocalLlama** is a cross-platform application that allows you to run Large Language Models locally using GGUF files.

No cloud. No external APIs. Fully private.

---

## ✨ Features

* 💬 ChatGPT-style interface
* 🧠 Support for local GGUF models
* ⚡ Powered by LLamaSharp
* 🔌 Backend powered by llama.cpp
* 📱 Cross-platform (Windows / Android / Linux)
* 🔒 Fully offline execution

---

## 🏗️ Architecture

```
UI (.NET MAUI)
     ↓
LLamaSharp (C# Wrapper)
     ↓
llama.cpp (Inference Engine)
     ↓
Local GGUF Model
```

---

## 🛠️ Technologies

| Technology | Purpose           |
| ---------- | ----------------- |
| .NET MAUI  | Cross-platform UI |
| C#         | Core logic        |
| LLamaSharp | llama.cpp wrapper |
| llama.cpp  | Inference engine  |
| GGUF       | Model format      |

---

## 📸 Screenshots

<img width="1229" height="763" alt="image" src="https://github.com/user-attachments/assets/c1fd562c-a9ab-4721-8afe-d91908b458da" />

<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 29 05" src="https://github.com/user-attachments/assets/079f5727-b98c-4bc3-afe0-f2ab643dc9a9" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 32 03" src="https://github.com/user-attachments/assets/4e06e9bc-4bdf-4ecc-9ad1-c6e9077f58ca" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 32 44" src="https://github.com/user-attachments/assets/b62a1b93-ca03-432a-82e2-96d8fc8ea9ac" />

<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 36 42" src="https://github.com/user-attachments/assets/30e645e5-43af-43f0-8427-16a2d9030d52" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 39 14" src="https://github.com/user-attachments/assets/f7067d8a-7c70-49f1-8127-6c238444e628" />

<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 36 55" src="https://github.com/user-attachments/assets/7b5702a1-04c0-4c56-98e9-53b369ace601" />


---

## 📦 Installation

### Clone repository

```bash
git clone https://github.com/your-user/your-repo.git
cd your-repo
```

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

---

## 📂 Project Structure

```
📦 LocalLlama
 ┣ 📂 Models
 ┣ 📂 Services
 ┣ 📂 Views
 ┣ 📂 ViewModels
 ┗ 📜 App.xaml
```

---

## 🧠 Supported Models

* LLaMA
* Mistral
* TinyLlama
* Phi
* Any GGUF-compatible model

---

## 🎯 Project Goals

* Learn local LLM integration
* Explore offline AI inference
* Build cross-platform AI applications
* Create an open-source ChatGPT alternative

---

## 🚀 Roadmap

* [ ] Real-time token streaming
* [ ] Persistent chat history
* [ ] Dynamic parameter configuration
* [ ] Multi-model support
* [ ] Android optimization

---

## 🤝 Contributing

Pull requests are welcome!
Open an issue for suggestions or improvements 🚀

---

## 📜 License

 Apache-2.0 license

---

# 🤖 LocalLlama — ChatGPT 100% Offline

> Um chatbot moderno, privado e totalmente offline, construído com **.NET MAUI** e powered por **llama.cpp**.

---

![.Net](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white) ![Visual Studio](https://img.shields.io/badge/Visual%20Studio-5C2D91.svg?style=for-the-badge&logo=visual-studio&logoColor=white) ![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white) ![GitHub](https://img.shields.io/badge/github-%23121011.svg?style=for-the-badge&logo=github&logoColor=white) ![Git](https://img.shields.io/badge/git-%23F05033.svg?style=for-the-badge&logo=git&logoColor=white)

# 📑 Índice (Português)

* [Sobre o Projeto](#-sobre-o-projeto)
* [Funcionalidades](#-principais-funcionalidades)
* [Arquitetura](#-arquitetura)
* [Tecnologias](#-tecnologias-utilizadas)
* [Screenshot](#-screenshot)
* [Instalação](#-instalação)
* [Estrutura do Projeto](#-estrutura-do-projeto)
* [Modelos Compatíveis](#-modelos-compatíveis)
* [Objetivo](#-objetivo-do-projeto)
* [Roadmap](#-roadmap)
* [Contribuições](#-contribuições)
* [Licença](#-licença)
* [English Version](#-english-version)

---

# 🇵🇹 Versão em Português

## 📌 Sobre o Projeto

O **LocalLlama** é uma aplicação multiplataforma que permite correr modelos LLM localmente, sem necessidade de internet ou APIs externas.

Tudo funciona 100% offline, garantindo privacidade total.

---

## ✨ Principais Funcionalidades

* 💬 Interface estilo ChatGPT
* 🧠 Suporte a modelos GGUF locais
* ⚡ Integração com LLamaSharp
* 🔌 Backend com llama.cpp
* 📱 Multiplataforma (Windows / Android / Linux)
* 🔒 Execução totalmente offline

---

## 🏗️ Arquitetura

```
UI (.NET MAUI)
     ↓
LLamaSharp (Wrapper C#)
     ↓
llama.cpp (Motor de Inferência)
     ↓
Modelo GGUF Local
```

---

## 🛠️ Tecnologias Utilizadas

| Tecnologia | Função                   |
| ---------- | ------------------------ |
| .NET MAUI  | Interface Cross-Platform |
| C#         | Lógica da aplicação      |
| LLamaSharp | Wrapper para llama.cpp   |
| llama.cpp  | Motor de inferência      |
| GGUF       | Formato de modelos       |

---

## 📸 Screenshot
<img width="1229" height="763" alt="image" src="https://github.com/user-attachments/assets/c1fd562c-a9ab-4721-8afe-d91908b458da" />

<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 29 05" src="https://github.com/user-attachments/assets/079f5727-b98c-4bc3-afe0-f2ab643dc9a9" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 32 03" src="https://github.com/user-attachments/assets/4e06e9bc-4bdf-4ecc-9ad1-c6e9077f58ca" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 32 44" src="https://github.com/user-attachments/assets/b62a1b93-ca03-432a-82e2-96d8fc8ea9ac" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 36 30" src="https://github.com/user-attachments/assets/a1ff9133-c9f1-4c6c-a7da-54cb08d70062" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 36 42" src="https://github.com/user-attachments/assets/30e645e5-43af-43f0-8427-16a2d9030d52" />
<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 39 14" src="https://github.com/user-attachments/assets/f7067d8a-7c70-49f1-8127-6c238444e628" />

<img width="1512" height="952" alt="Captura de ecrã 2026-02-25, às 12 36 55" src="https://github.com/user-attachments/assets/7b5702a1-04c0-4c56-98e9-53b369ace601" />



## 📦 Instalação

### Clonar o projeto

```bash
git clone https://github.com/teu-user/teu-repo.git
cd teu-repo
```

### Build

```bash
dotnet build
```

### Executar

```bash
dotnet run
```

---

## 📂 Estrutura do Projeto

```
📦 LocalLlama
 ┣ 📂 Models
 ┣ 📂 Services
 ┣ 📂 Views
 ┣ 📂 ViewModels
 ┗ 📜 App.xaml
```

---

## 🧠 Modelos Compatíveis

* LLaMA
* Mistral
* TinyLlama
* Phi
* Qualquer modelo em formato GGUF

---

## 🎯 Objetivo do Projeto

* Aprender integração de LLMs locais
* Explorar inferência offline
* Desenvolver aplicações AI multiplataforma
* Criar alternativa open-source ao ChatGPT

---

## 🚀 Roadmap

* [ ] Streaming de tokens
* [ ] Histórico persistente
* [ ] Configuração dinâmica (temperature, top-p…)
* [ ] Multi-model support
* [ ] Otimização Android

---

## 🤝 Contribuições

Pull requests são bem-vindos!
Abre uma issue para sugestões ou melhorias 🚀

---

## 📜 Licença

MIT License

---




![.Net](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white) ![Visual Studio](https://img.shields.io/badge/Visual%20Studio-5C2D91.svg?style=for-the-badge&logo=visual-studio&logoColor=white) ![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white) ![GitHub](https://img.shields.io/badge/github-%23121011.svg?style=for-the-badge&logo=github&logoColor=white) ![Git](https://img.shields.io/badge/git-%23F05033.svg?style=for-the-badge&logo=git&logoColor=white)
