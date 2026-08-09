# WareConnect Reporting Tool + Copilot

An enterprise financial reporting application with an AI-powered copilot assistant built on **Azure OpenAI (o4-mini)**. Users can browse year-by-year report data, edit amounts inline, and ask natural-language questions to the copilot — which answers using live database queries, never fabricated data.

---

## Table of Contents

- [Screenshots](#screenshots)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture Overview](#architecture-overview)
- [Database Schema](#database-schema)
- [API Endpoints](#api-endpoints)
- [AI Copilot & Tools](#ai-copilot--tools)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
- [Running Locally](#running-locally)
- [Environment Variables / Secrets](#environment-variables--secrets)

---

## Features

- 📊 **Year-based report viewer** — browse financial data tables (`Data_2018` → `Data_2026`) with pagination, sorting, and page-size control
- ✏️ **Inline Amount editing** — double-click any Amount cell to edit and save directly to the database
- 🤖 **AI Copilot** — floating chat panel powered by Azure OpenAI (o4-mini) with:
  - Natural-language financial queries ("What were total sales in June 2026?")
  - Automatic year resolution ("this year" = 2026, "last year" = 2025)
  - Streaming responses via Server-Sent Events (SSE)
  - Conversation memory persisted in SQL Server
  - Tool-calling architecture — the AI calls internal APIs to get real data, never guesses
- 📈 **Rich aggregate APIs** — year summaries, monthly breakdowns, group/account-type/item-type drill-downs, and year-over-year comparisons

---

## Tech Stack

### Backend — `WareConnect.Api`

| Layer | Technology |
|---|---|
| Runtime | .NET 8 (ASP.NET Core) |
| Language | C# 12 |
| AI SDK | Azure.AI.OpenAI `2.1.0` + OpenAI `2.3.0` |
| Database | Microsoft SQL Server (SQL Express) |
| SQL Client | Microsoft.Data.SqlClient `6.1.2` |
| API Docs | Swashbuckle / Swagger `6.6.2` |
| HTTP Client | Microsoft.Extensions.Http `8.0.1` |
| AI Model | Azure OpenAI — `o4-mini` (reasoning model) |

### Frontend — `wareconnect-ui`

| Layer | Technology |
|---|---|
| Framework | Angular 20 |
| UI Library | PrimeNG 20 + PrimeIcons 8 |
| Styling | CSS (custom, no Tailwind) |
| HTTP | Angular HttpClient |
| AI Streaming | EventSource (SSE) |
| Build | Angular CLI / esbuild |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Browser (Angular 20)                     │
│                                                             │
│  ┌──────────────────────┐   ┌───────────────────────────┐  │
│  │  Report Data Table   │   │   Copilot Chat Panel      │  │
│  │  (PrimeNG p-table)   │   │   (SSE EventSource)       │  │
│  └──────────┬───────────┘   └──────────────┬────────────┘  │
└─────────────┼──────────────────────────────┼───────────────┘
              │ REST                         │ SSE / REST
              ▼                             ▼
┌─────────────────────────────────────────────────────────────┐
│               ASP.NET Core 8 API  (:5256)                   │
│                                                             │
│  ┌──────────────────┐    ┌───────────────────────────────┐  │
│  │ ReportData       │    │ Copilot Controller            │  │
│  │ Controller       │    │  POST /api/copilot/chat (SSE) │  │
│  │ (CRUD + agg.)    │    │  GET  /api/copilot/models     │  │
│  └────────┬─────────┘    └──────────────┬────────────────┘  │
│           │                             │                   │
│           │                   ┌─────────▼──────────────┐   │
│           │                   │ CopilotOrchestrator    │   │
│           │                   │  PromptBuilder         │   │
│           │                   │  ConversationMemory    │   │
│           │                   │  ToolDispatcher ──────►│   │
│           │                   └─────────┬──────────────┘   │
│           │                             │ HTTP (internal)   │
│           │◄────────────────────────────┘                   │
│           │                                                 │
│  ┌────────▼────────────────────────────────────────────┐   │
│  │            ReportDataService (raw SQL)               │   │
│  └────────────────────────┬────────────────────────────┘   │
└───────────────────────────┼─────────────────────────────────┘
                            │ ADO.NET / SqlClient
                            ▼
              ┌─────────────────────────────┐
              │  SQL Server                 │
              │  Database: Report_Sports..  │
              │  Tables: Data_2018..2026    │
              │          Copilot_Conv..     │
              │          Copilot_Messages   │
              │          Copilot_UsageLog   │
              └─────────────────────────────┘
                            │
                            ▼
              ┌─────────────────────────────┐
              │  Azure OpenAI               │
              │  Endpoint: aoai-datadevice  │
              │  Model: o4-mini             │
              └─────────────────────────────┘
```

> The AI Copilot never calls Azure OpenAI directly with raw data. It uses **tool-calling**: the model requests data via named tools → `ToolDispatcher` calls the internal REST API → results are returned to the model to formulate the answer.

---

## Database Schema

**Database:** `Report_SportsmanHotel` (SQL Server)

### Report Data Tables — `Data_YYYY` (one per year, 2018–2026)

| Column | Type | Description |
|---|---|---|
| `RowID` | `int IDENTITY` | Primary key |
| `Year` | `int` | Financial year |
| `MYOBAccount` | `nvarchar(50)` | MYOB account code (e.g. `9-1000`) |
| `AccountName` | `nvarchar(255)` | Full account name |
| `AccountType` | `nvarchar(50)` | e.g. `Others`, `Income` |
| `Amount` | `decimal(18,2)` | Transaction amount (**editable via UI**) |
| `StartDate` / `EndDate` | `date` | Period start/end |
| `MonthName` | `nvarchar(50)` | e.g. `January`, `June` |
| `WeekInMonth` | `nvarchar(50)` | Week label within month |
| `MonthAmount` | `decimal(18,2)` | Month-level aggregate amount |
| `GroupName` | `nvarchar(255)` | Business group (e.g. `Other Expenses`) |
| `ItemType` | `nvarchar(255)` | Item category |
| `Sales` | `decimal(18,2)` | Sales figure |
| `OtherExp` | `decimal(18,2)` | Other expenses |
| `GP2` | `decimal(18,2)` | Gross Profit 2 |
| `DistinctGP2` | `decimal(18,2)` | Distinct GP2 value |
| `BudgetAmount` | `decimal(18,2)` | Budgeted amount |
| `LYRBudgetAmount` | `decimal(18,2)` | Last-year-revised budget |
| `MonthBudgetAmount` | `decimal(18,2)` | Monthly budget |
| `MonthLYRBudgetAmount` | `decimal(18,2)` | Monthly LYR budget |

> Tables are named `Data_2018`, `Data_2019`, …, `Data_2026`. The API auto-discovers them from `INFORMATION_SCHEMA.TABLES`.

---

### Copilot Persistence Tables

#### `Copilot_Conversations`
Stores one row per chat session.

| Column | Type | Notes |
|---|---|---|
| `ConversationId` | `nvarchar(64)` PK | UUID |
| `UserId` | `nvarchar(256)` | Default `'1'` |
| `Title` | `nvarchar(512)` | Auto-generated |
| `CurrentPage` / `CurrentModule` | `nvarchar` | Screen context for the AI |
| `CurrentCompany` / `CurrentVendor` / `CurrentInvoiceId` | `nvarchar` | Entity context |
| `Language` | `nvarchar(16)` | Default `'en'` |
| `TimeZone` | `nvarchar(64)` | Default `'UTC'` |
| `IsActive` | `bit` | Soft-delete flag |
| `CreatedAt` / `LastActivityAt` | `datetime2` | Timestamps |

#### `Copilot_Messages`
One row per message (user / assistant / tool / system).

| Column | Type | Notes |
|---|---|---|
| `MessageId` | `bigint IDENTITY` PK | |
| `ConversationId` | FK → `Copilot_Conversations` | CASCADE DELETE |
| `Role` | `nvarchar(32)` | `user`, `assistant`, `tool`, `system` |
| `Content` | `nvarchar(MAX)` | Message body |
| `ToolCallId` / `ToolName` | `nvarchar` | Populated for tool messages |
| `PromptTokens` / `CompletionTokens` / `TotalTokens` | `int` | Token usage |
| `Model` | `nvarchar(128)` | Model name used |
| `LatencyMs` | `int` | Response time |
| `CreatedAt` | `datetime2` | |

#### `Copilot_UsageLog`
Token and cost audit trail per conversation turn.

| Column | Type | Notes |
|---|---|---|
| `LogId` | `bigint IDENTITY` PK | |
| `ConversationId` | `nvarchar(64)` | |
| `UserId` | `nvarchar(256)` | |
| `Model` | `nvarchar(128)` | |
| `PromptTokens` / `CompletionTokens` / `TotalTokens` | `int` | |
| `ToolInvoked` | `nvarchar(256)` | Which AI tool was called |
| `LatencyMs` | `int` | |
| `CreatedAt` | `datetime2` | |

---

## API Endpoints

Base URL: `http://localhost:5256`  
Interactive docs: `http://localhost:5256/swagger`

### Report Data

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/report-data/years` | List all available year tables |
| `GET` | `/api/report-data/{year}` | Paginated raw rows (`?pageNumber=1&pageSize=20`) |
| `PUT` | `/api/report-data/{year}/rows/{rowId}/amount` | **Update Amount** — body: `{ "amount": 1234.56 }` |
| `GET` | `/api/report-data/{year}/summary` | Grand totals for the year |
| `GET` | `/api/report-data/{year}/by-month` | Monthly breakdown (Jan → Dec) |
| `GET` | `/api/report-data/{year}/by-group` | Breakdown by GroupName |
| `GET` | `/api/report-data/{year}/by-account-type` | Breakdown by AccountType |
| `GET` | `/api/report-data/{year}/by-item-type` | Breakdown by ItemType |
| `GET` | `/api/report-data/compare?yearA=2024&yearB=2025` | Year-over-year comparison + variance |
| `GET` | `/api/report-data/{year}/filter` | Filtered rows + totals (`?month=June&groupName=Food&accountType=…&itemType=…`) |

### Copilot

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/copilot/chat` | Send message → stream SSE response |
| `GET` | `/api/copilot/models` | List available AI models |

---

## AI Copilot & Tools

The copilot uses **Azure OpenAI tool-calling** (function calling). When the user asks a question, the model decides which tool to call, the `ToolDispatcher` hits the corresponding REST endpoint, and the result is fed back to the model.

| Tool | Calls | When used |
|---|---|---|
| `GetReportYears` | `GET /years` | List available years |
| `GetReportData` | `GET /{year}` | Show individual rows |
| `GetYearSummary` | `GET /{year}/summary` | "What is the total Amount for 2026?" |
| `GetMonthlyBreakdown` | `GET /{year}/by-month` | "Show me monthly sales for 2025" |
| `GetGroupBreakdown` | `GET /{year}/by-group` | "Which group has the most GP2?" |
| `GetAccountTypeBreakdown` | `GET /{year}/by-account-type` | "Break down by account type" |
| `GetItemTypeBreakdown` | `GET /{year}/by-item-type` | "Break down by item type" |
| `CompareYears` | `GET /compare` | "Compare 2024 vs 2025" |
| `GetFilteredData` | `GET /{year}/filter` | "Total sales for June in the Food group" |

**Automatic year resolution:**
- "this year" / "current year" → `2026` (no clarification asked)
- "last year" / "previous year" → `2025`
- "next year" → `2027`

---

## Project Structure

```
Reporting_Tool_Copilot/
│
├── WareConnect.Api/                   # ASP.NET Core 8 backend
│   ├── Controllers/
│   │   ├── CopilotController.cs       # SSE chat + model picker
│   │   └── ReportDataController.cs    # All report data CRUD + aggregates
│   ├── Services/
│   │   ├── IReportDataService.cs
│   │   └── ReportDataService.cs       # Raw ADO.NET SQL queries
│   ├── Models/
│   │   ├── ReportRowDto.cs
│   │   ├── ReportAggregateDto.cs      # YearSummary, Monthly, Dimension, Comparison, Filtered
│   │   ├── UpdateAmountRequest.cs
│   │   └── PagedResult.cs
│   └── AI/
│       ├── Configuration/
│       │   └── CopilotOptions.cs
│       ├── Context/
│       │   └── ContextBuilder.cs      # Resolves screen context for system prompt
│       ├── Memory/
│       │   ├── SqlConversationMemory.cs  # SQL-persisted conversation history
│       │   └── InMemoryConversationMemory.cs
│       ├── Prompts/
│       │   └── PromptBuilder.cs       # System prompt with date awareness
│       ├── Services/
│       │   ├── CopilotOrchestrator.cs # Main AI loop (tool-call cycle)
│       │   └── CopilotResponseService.cs
│       └── Tools/
│           └── ToolDispatcher.cs      # 9 AI tools → internal REST calls
│
├── wareconnect-ui/                    # Angular 20 frontend
│   └── src/app/
│       ├── app.ts                     # Main component (table, editing, pagination)
│       ├── app.html                   # Table with inline amount editing
│       ├── app.css                    # All custom styles
│       └── copilot/
│           ├── copilot.service.ts     # State management (open/close)
│           ├── copilot-window/        # Chat panel container
│           ├── chat-body/             # Message list
│           ├── chat-input/            # Message input bar
│           ├── chat-message/          # Individual message bubble
│           ├── chat-header/           # Header with model picker
│           ├── markdown-renderer/     # Renders AI markdown responses
│           ├── suggested-questions/   # Quick-start question chips
│           ├── typing-indicator/      # Animated dots while streaming
│           └── copilot-button/        # Floating action button
│
└── Database/
    └── Copilot_ConversationSchema.sql  # Run once to create all DB tables
```

---

## Configuration

### Backend — `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Report_SportsmanHotel;Integrated Security=True;TrustServerCertificate=True"
  },
  "Copilot": {
    "OpenAI": {
      "ApiKey": "<YOUR_AZURE_OPENAI_KEY>",
      "AzureEndpoint": "https://<YOUR_RESOURCE>.openai.azure.com/",
      "DeploymentName": "<YOUR_DEPLOYMENT>",
      "ApiVersion": "2025-01-01-preview",
      "Model": "o4-mini",
      "MaxTokens": 4096,
      "Temperature": 0.2,
      "TimeoutSeconds": 120,
      "MaxRetries": 3
    },
    "Memory": {
      "MaxMessagesInContext": 15,
      "MaxStoredMessages": 100,
      "EnableSqlPersistence": true
    },
    "BaseApiUrl": "http://localhost:5256",
    "AvailableModels": [
      { "Id": "<YOUR_DEPLOYMENT>", "DisplayName": "o4-mini", "IsReasoning": true }
    ]
  }
}
```

> ⚠️ **Never commit real API keys.** Use `appsettings.Development.json` (git-ignored) or environment variables / Azure Key Vault in production.

### Frontend — API base URL

In `wareconnect-ui/src/app/app.ts`:

```typescript
private readonly apiBaseUrl = 'http://localhost:5256/api/report-data';
```

Change this for different environments or use Angular environment files.

---

## Running Locally

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 8.0+ |
| Node.js | 18+ |
| SQL Server | Express 2019+ |
| Angular CLI | `npm install -g @angular/cli` |

### 1 — Set up the Database

1. Create a SQL Server database named `Report_SportsmanHotel`
2. Run the schema script:

```sql
-- In SSMS or sqlcmd against Report_SportsmanHotel:
-- Run: Database/Copilot_ConversationSchema.sql
```

This creates:
- `Data_YYYY` report tables (structure only — import your own data)
- `Copilot_Conversations`, `Copilot_Messages`, `Copilot_UsageLog`

### 2 — Run the Backend API

```bash
cd WareConnect.Api

# Restore dependencies
dotnet restore

# Set your Azure OpenAI key (do not commit to appsettings.json)
# Option A: appsettings.Development.json
# Option B: environment variable
set Copilot__OpenAI__ApiKey=<your-key>
set Copilot__OpenAI__AzureEndpoint=https://<your-resource>.openai.azure.com/

# Run
dotnet run
```

API will be available at: `http://localhost:5256`  
Swagger UI: `http://localhost:5256/swagger`

### 3 — Run the Frontend

```bash
cd wareconnect-ui

# Install dependencies
npm install

# Start dev server
npm start
```

Angular app will be available at: `http://localhost:4200`

---

## Environment Variables / Secrets

For production or CI/CD, supply these environment variables instead of hardcoding in `appsettings.json`:

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | Full SQL Server connection string |
| `Copilot__OpenAI__ApiKey` | Azure OpenAI API key |
| `Copilot__OpenAI__AzureEndpoint` | Azure OpenAI resource endpoint URL |
| `Copilot__OpenAI__DeploymentName` | Model deployment name |
| `Copilot__BaseApiUrl` | Base URL of the API (for internal tool calls) |

---

## License

MIT — see `LICENSE` for details.
