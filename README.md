# 🎥 YouTubester

**YouTubester** is a layered .NET project that automates YouTube metadata updates and comment management.  
It integrates with the **YouTube Data API** and can optionally use a local AI model (via [Ollama](https://ollama.ai/)) to generate titles, descriptions, and replies.

This project demonstrates:
- ASP.NET Core Web API with Swagger UI
- Clean architecture separation (Domain, Persistence, Application, API)
- EF Core with migrations (SQLite by default, Postgres/SQL Server optional)
- Dependency injection & configuration
- AI integration for automated replies and metadata suggestions

---

## ✨ Features
- 🔑 OAuth2 authentication with YouTube Data API
- 🏷 Sync video tags and append hashtags to descriptions
- 📂 Add Shorts to playlists automatically
- 💬 List unanswered comments and generate AI-powered draft replies
- ✅ Review/edit/approve drafts via API (future React UI planned)
- 📝 Persist drafts and posted replies with EF Core
- 🌍 Configurable persistence (SQLite, Postgres, SQL Server)
- 🤖 Local AI integration via Ollama (no API costs)

---

## 🏗️ Architecture

### Layers explained
- **Domain**: Pure business entities, no EF or API dependencies.
- **Persistence**: EF Core DbContext, entity mappings, repositories.
- **Application**: Business rules (approving drafts, posting replies, scanning comments).
- **API**: ASP.NET Core Web API with controllers, DI, Swagger.

This separation ensures portability and demonstrates a clean architecture approach.

---

## ⚡ Getting Started

### 1. Clone and restore
```bash
git clone https://github.com/yourusername/YouTubester.git
cd YouTubester
dotnet restore
```

### 2. Configure secrets
```
dotnet user-secrets init --project YouTubester.Api

dotnet user-secrets set "YouTube:ClientId" "your-client-id" --project YouTubester.Api
dotnet user-secrets set "YouTube:ClientSecret" "your-client-secret" --project YouTubester.Api

dotnet user-secrets set "YouTube:AI:Endpoint" "http://localhost:11434" --project YouTubester.Api
dotnet user-secrets set "YouTube:AI:Model" "gemma3:12b" --project YouTubester.Api
```

### 3. Database
Run the following in the solution root's folder
```
dotnet ef database update -p YouTubester.Persistence
```

## 🌐 Running the API
```
dotnet run --project YouTubester.Api
```
Swagger UI is then available at:
👉 https://localhost:5094/swagger