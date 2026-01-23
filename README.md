# 🧠 CORTEX
**Central Operations & Routing Technology EXpert**

An AI-powered intelligent support operations platform for enterprise ticket management.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![React](https://img.shields.io/badge/React-18-blue)
![TypeScript](https://img.shields.io/badge/TypeScript-5-blue)

---

## 🎯 Overview

CORTEX revolutionizes support operations by intelligently routing tickets based on skills, workload, and historical patterns. Built to solve real workflow problems in Syniti's DSP platform operations.

**Key Innovation:** Dual ownership model separating technical execution (Syniti Owner) from business verification (Business Owner) - solving the ambiguity in current support workflows.

---

## ❓ Why CORTEX?

### The Problem:
In enterprise support operations, ticket ownership is often ambiguous. A single "owner" field doesn't capture the reality of how work actually flows - technical teams execute fixes while business stakeholders must verify outcomes. This leads to:

- 🔄 Tickets bouncing between teams with unclear responsibility
- ⏱️ Delays waiting for the "right person" to take action
- 📉 Difficulty tracking who did what and when
- 🤷 No clear accountability for technical vs. business sign-off

### The Solution:
CORTEX introduces a dual ownership model that mirrors how enterprise support actually works:

| Role | Responsibility |
|------|----------------|
| **Syniti Owner** | Technical execution - implements fixes, configurations, deployments |
| **Business Owner** | Business verification - confirms the solution meets requirements |

### Built from Experience:
This isn't theoretical. CORTEX is designed by someone with 5 years of hands-on experience supporting JnJ's mDPI operations on the DSP platform - solving real problems I see every day.

---

## ✨ Current Features

### Backend API (.NET 10)
- ✅ **7 RESTful endpoints** with full CRUD operations
- ✅ **Dual ownership tracking** (Technical + Business owners)
- ✅ **String-based ticket IDs** (TICKET-001, TICKET-002, etc.)
- ✅ **Filtering capabilities** (by status, priority)
- ✅ **Audit trail** (CreatedBy, LastModifiedBy, timestamps)
- ✅ **Immutable field protection** (ID, creation metadata)
- ✅ **Swagger/OpenAPI documentation**
- ✅ **CORS enabled** for frontend integration
- ✅ Database persistence (SQL Server + EF Core)

### Frontend (React + TypeScript + Tailwind)
- ✅ **Modern, responsive UI** with gradient design
- ✅ **Real-time ticket display** from API
- ✅ **Interactive filtering** (status, priority with partial match)
- ✅ **Modal editing** (click any ticket to view/edit)
- ✅ **Editable fields:** Priority, Status, Syniti Owner, Business Owner
- ✅ **Visual priority badges** (color-coded: Critical/High/Medium/Low)
- ✅ **Status indicators** (New/In Progress/Pending Review/Resolved/Closed)
- ✅ **Metadata display** (creation date, last modified, owners)

### Planned Features
- ⏳ Smart routing engine (skill + workload based)
- ⏳ Environment deployment tracking (Dev → QA → Prod)
- ⏳ CTS archive integration
- ⏳ AI-powered ticket categorization (ML.NET)
- ⏳ Predictive analytics
- ⏳ Real-time updates (SignalR)

---

## 🛠 Tech Stack

**Backend:**
- .NET 10
- ASP.NET Core Minimal APIs
- Swagger/OpenAPI for documentation

**Frontend:**
- React 18
- TypeScript 5
- Tailwind CSS 3
- Vite (build tool)

**Planned:**
- SQL Server + Entity Framework Core
- ML.NET for AI features
- SignalR for real-time
- Azure for deployment

---

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK ([Download](https://dotnet.microsoft.com/download))
- Node.js 20+ ([Download](https://nodejs.org/))
- Visual Studio 2022+ or VS Code

### Run Backend API
```bash
cd Cortex.API
dotnet run
```

API runs at: `http://localhost:5214`

**Swagger UI:** `http://localhost:5214/swagger`

### Run Frontend
```bash
cd cortex-ui
npm install
npm run dev
```

Frontend runs at: `http://localhost:5173`

---

## 📍 API Endpoints

### Health
- `GET /` - API health check

### Tickets
- `GET /api/tickets` - List all tickets
- `GET /api/tickets/{id}` - Get specific ticket by ID
- `GET /api/tickets/status/{status}` - Filter tickets by status
- `GET /api/tickets/priority/{priority}` - Filter tickets by priority
- `POST /api/tickets` - Create new ticket (auto-generates ID)
- `PUT /api/tickets/{id}` - Update ticket (preserves immutable fields)

**Protected Fields (Immutable):**
- Ticket ID
- Created By
- Created Date

**Editable Fields:**
- Title, Description
- Priority (Critical/High/Medium/Low)
- Status (New/In Progress/Pending Business Review/Resolved/Closed)
- Syniti Owner
- Business Owner

---

## 🏗 Architecture

**Clean Separation of Concerns:**
```
Backend (Cortex.API)
├── Extensions/         # Endpoint definitions
│   └── TicketEndpoints.cs
├── Models/            # Data models
│   └── Ticket.cs
└── Program.cs         # Application setup

Frontend (cortex-ui)
├── components/        # React components
│   ├── TicketCard.tsx
│   └── TicketModal.tsx
├── services/          # API integration
│   └── api.ts
├── types/             # TypeScript definitions
│   └── ticket.ts
└── App.tsx           # Main application
```

---

## 📝 Development Timeline

**Week 1 (Jan 13-19, 2026):** ✅ **COMPLETE**
- Backend API with 7 endpoints
- React frontend with Tailwind UI
- Full CRUD operations
- Filtering and editing capabilities
- Swagger documentation
- GitHub repository established

**Next Steps:**
- Database integration (SQL Server + EF Core)
- Authentication (JWT)
- Smart routing algorithm
- AI-powered categorization
- Environment tracking

**Target:** Production-ready by November 2026 DemoJam

---

## 👨‍💻 About

**Developer:** Adam Hooper  
**Role:** Senior Consultant at Syniti  
**Purpose:** Full-stack engineering demonstration for internal Engineer II position

**Project Goals:**
1. Demonstrate full-stack development capability
2. Solve real support operations problems
3. Showcase modern tech stack proficiency
4. Build production-quality software
5. Present at Syniti DemoJam (November 2026)

---

## 📧 Contact

**Adam Hooper**  
Senior Consultant | Syniti  
[GitHub](https://github.com/Airsicktitan)

---

**Status:** 🚧 Active Development | **Visibility:** 🔓 Public | **License:** Private (Syniti Internal Project)


## 📸 Demo Screenshots

### React Frontend
![CORTEX Main View](screenshots/frontend-main.png)
*Main dashboard showing all tickets with filtering capability*

![Filtered View](screenshots/frontend-filter.png)
*Filtering tickets by status*

### Interactive Features
![Edit Modal](screenshots/modal-edit.png)
![Edit Modal](screenshots/modal-edit-audit-history.png)
![Edit Modal](screenshots/modal-edit-save.png)
*Click any ticket to view details and edit priority, status, and owners*

### API Documentation
![Swagger UI](screenshots/swagger-api.png)
*RESTful API with 7 endpoints documented in Swagger*

![Swagger UI](screenshots/swagger-detail.png)
*RESTful API with endpoint Get displaying sample data*

### Code Quality
![Backend Code](screenshots/code-backend-program-file.png)
![Backend Code](screenshots/code-backend-endpoints-01.png)
![Backend Code](screenshots/code-backend-endpoints-02.png)
![Backend Code](screenshots/code-backend-sampledata-file.png)
![Backend Code](screenshots/code-backend-ticket-model.png)
*Clean, organized endpoint structure*

![Frontend Code](screenshots/code-frontend-app-01.png)
![Frontend Code](screenshots/code-frontend-app-02.png)
![Frontend Code](screenshots/code-frontend-app-03.png)
![Frontend Code](screenshots/code-frontend-api.png)
![Frontend Code](screenshots/code-frontend-ticketCard-component.png)
![Frontend Code](screenshots/code-frontend-ticketmodal-01.png)
![Frontend Code](screenshots/code-frontend-ticketmodal-02.png)
![Frontend Code](screenshots/code-frontend-ticketmodal-03.png)
![Frontend Code](screenshots/code-frontend-ticketmodal-04.png)
*Modern React with TypeScript and Tailwind CSS*
