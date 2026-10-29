# ExamNexus &mdash; Multi-Tenant Assessment & Real-Time Proctored Exam SaaS Platform

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-purple.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time%20WebSocket-orange.svg)](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-9.0.2%20Global%20Filters-green.svg)](https://learn.microsoft.com/en-us/ef/core/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20Onion-success.svg)](#architecture)

---

## 📌 Executive Summary

**ExamNexus** is an enterprise-grade EdTech SaaS platform designed for conducting high-stakes certifications and university assessments with **strict multi-tenant isolation** and **real-time WebSocket proctoring**.

It features automated tab-switch tracking, live candidate heartbeats, remote proctor intervention (warnings & disqualification), AJAX auto-saving, and automated grading with audit logs.

Targeted for **.NET 9.0 (`net9.0`)** ensuring full, seamless compatibility with Visual Studio 2022 (v17.x).

---

## 🏗️ Clean Architecture Overview

The solution adheres strictly to Clean Architecture (Domain-Driven Design):

```
ExamNexus/
├── global.json                         # SDK pinned to 9.0.311
├── ExamNexus.sln                       # Solution (Web set as default startup)
├── src/
│   ├── ExamNexus.Domain/               # Core business entities & invariants
│   │   ├── Common/BaseEntity.cs        # BaseTenantEntity & ITenantEntity
│   │   ├── Enums/Enums.cs              # TenantPlan, QuestionType, SessionStatus, Severity
│   │   └── Entities/                   # Tenant, Course, Question, Exam, CandidateSession
│   │
│   ├── ExamNexus.Application/          # Use cases, interfaces, DTOs
│   │   ├── Interfaces/                 # ITenantService, ICurrentUserService, IProctorHubClient
│   │   └── DTOs/                       # Auth, Exam lobby, Live session, Proctor dashboard
│   │
│   ├── ExamNexus.Infrastructure/       # EF Core, Identity, Multi-Tenancy & JWT
│   │   ├── Data/ApplicationDbContext.cs # EF Core Global Query Filters for Tenant Isolation
│   │   ├── Data/DbInitializer.cs       # Seeds tenants, courses, exams, & test users
│   │   └── Services/                   # TenantService, CurrentUserService, JwtTokenService
│   │
│   └── ExamNexus.Web/                  # ASP.NET Core 9.0 MVC + SignalR + REST API
│       ├── Controllers/                # Account, Dashboard, Exam, Proctor, Api/Auth
│       ├── Hubs/ProctorHub.cs          # SignalR WebSocket Hub for Live Telemetry
│       ├── Middleware/                 # TenantResolutionMiddleware & SecurityHeaders
│       └── Views/                      # Razor UI with 1-Click Role Logins
```

---

## 🛡️ Multi-Tenancy Architecture

ExamNexus implements a **shared-database, isolated-schema multi-tenant design**:

1. **Resolution Pipeline** (`TenantResolutionMiddleware`):
   - **Claims**: Reads `TenantId` from the authenticated cookie/JWT.
   - **HTTP Header**: Fallback for REST API clients (`X-Tenant-Id: acme-corp`).
   - **Persistent Cookie**: `.ExamNexus.Tenant` stores the active organization.
   - **Query Parameter**: URL fallback (`?tenant=acme-corp`).

2. **Database Engine Isolation** (`ApplicationDbContext`):
   - Every tenant entity implements `ITenantEntity` with a `Guid TenantId`.
   - EF Core **Global Query Filters** automatically inject `WHERE TenantId = @CurrentTenantId` into every SELECT, UPDATE, and DELETE statement.
   - Cross-tenant data leakage is physically prevented at the query generation layer.

3. **ChangeTracker Auto-Population**:
   - Newly inserted records automatically inherit the active `CurrentTenantId` without manual controller assignment.

---

## 📡 Real-Time Anti-Cheat Proctoring Engine (SignalR)

Live candidate monitoring is powered by **SignalR WebSockets** (`/hubs/proctor`):

- **Browser Tab-Switch & Blur Interception**:
  - `document.addEventListener("visibilitychange", ...)` fires whenever a candidate switches tabs, minimizes their browser, or clicks off-screen.
  - An encrypted packet is dispatched over WebSocket directly to `ProctorHub.ReportTabSwitch(sessionId)`.
  - Exceeding allowable switches triggers `IsFlagged = true` with cheating audit trails.

- **Continuous Heartbeat Telemetry**:
  - Background heartbeats sync remaining time every 15 seconds to the Proctor Command Center.

- **Proctor Intervention**:
  - Live proctors can broadcast instant warnings directly to candidate screens (`SendProctorWarningToCandidate`).
  - Proctors can remotely terminate and disqualify candidates with 1 click (`TerminateSession`).

---

## 🚀 How to Run

### Option A: Visual Studio 2022
1. Open `D:\Anubhaw\Projects\Structure\ExamNexus\ExamNexus.sln`.
2. Notice that **`ExamNexus.Web`** is already configured as the startup project.
3. Press **F5** (or click the green **`https`** play button).
4. The browser will open directly to `https://localhost:7172` (or your assigned localhost port).

### Option B: .NET CLI
```powershell
cd "D:\Anubhaw\Projects\Structure\ExamNexus\src\ExamNexus.Web"
dotnet run
```
Then navigate to: `http://localhost:5139` or `https://localhost:7172`.

---

## 🔑 Ready Test Credentials (1-Click Login on UI)

The login page at `/Account/Login` features **instant 1-click test credential buttons** for seamless evaluation:

| Organization Tenant | Role | Email | Password | Access Capabilities |
| :--- | :--- | :--- | :--- | :--- |
| **Acme Corp** (`acme-corp`) | **Admin** | `admin@acme.com` | `P@ssword123!` | Full tenant admin, course/exam creation, proctor console |
| **Acme Corp** (`acme-corp`) | **Instructor** | `instructor@acme.com` | `P@ssword123!` | Question banks, test authoring, grading review |
| **Acme Corp** (`acme-corp`) | **Proctor** | `proctor@acme.com` | `P@ssword123!` | Real-time WebSocket command center, live candidate monitoring |
| **Acme Corp** (`acme-corp`) | **Candidate** | `student@acme.com` | `P@ssword123!` | Timed exam taking, live autosave, instant results |
| **Apex University** (`apex-university`) | **Apex Admin** | `admin@apex.edu` | `P@ssword123!` | Isolated tenant testing; completely separate courses & exams |

---

## 🗄️ Database Script & Production Deployment

- **Full Enterprise SQL Script**: `D:\Anubhaw\Projects\DB\ExamNexus SaaS DB.sql`
  - Targets SQL Server with dedicated schema `ExamSaaS`.
  - 15 relational tables with foreign keys and tenant indexing.
  - 4 Stored Procedures:
    - `sp_StartCandidateExamSession`
    - `sp_SubmitCandidateExamSession`
    - `sp_LogProctorEvent`
    - `sp_GetTenantProctorDashboardSummary`
- **Development Zero-Config**: Runs automatically on SQLite `ExamNexus.db` out of the box with zero setup required.
