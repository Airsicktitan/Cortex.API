# 🧠 CORTEX
**Central Operations & Routing Technology EXpert**

An AI-powered intelligent support operations platform for enterprise ticket management.

## 🎯 Project Vision
CORTEX revolutionizes support operations by intelligently routing tickets based on skills, workload, and historical patterns. Built for Syniti's DSP platform operations.

## 🚀 Features (In Progress)
- ✅ RESTful API with ticket endpoints
- ✅ Dual ownership model (Syniti + Business)
- ✅ String-based ticket IDs (TICKET-001, etc.)
- ✅ React frontend with Tailwind UI
- ⏳ Real-time environment deployment tracking
- ⏳ Smart ticket routing (skill + workload based)
- ⏳ CTS archive integration
- ⏳ AI-powered categorization
- ⏳ Predictive analytics

## 🛠 Tech Stack
- **Backend:** .NET 10, ASP.NET Core Minimal APIs
- **Frontend:** React 18, TypeScript, Tailwind CSS (coming soon)
- **Database:** SQL Server + EF Core (planned)
- **AI/ML:** ML.NET (planned)
- **Real-time:** SignalR (planned)

## 📅 Timeline
- **January 2026:** Backend API ✅
- **February-March 2026:** Frontend + Core Features
- **April-June 2026:** AI/ML Intelligence Layer
- **July-August 2026:** Advanced Features + Polish
- **September-October 2026:** DemoJam Preparation
- **November 2026:** 🏆 Syniti DemoJam

## 🏃‍♂️ Getting Started

### Prerequisites
- .NET 10 SDK
- Node.js 20+
- Visual Studio 2026 or VS Code

### Run Backend API
```bash
cd Cortex.API
dotnet run
```
API available at: `http://localhost:5214`

**Endpoints:**
- `GET /api/tickets` - List all tickets
- `GET /api/tickets/{id}` - Get ticket by ID
- `POST /api/tickets` - Create new ticket

## 📝 Current Status
**Day 1 Complete (January 15, 2026):**
- ✅ Project initialized
- ✅ Backend API functional
- ✅ 3 REST endpoints working
- ✅ Data models defined
- ✅ Version control established

**Next Up:**
- Create Ticket Button
- Automate Audit History and User Assignment
- API integration

## 👨‍💻 Developer
**Adam Hooper** | Syniti Senior Consultant  
*Building CORTEX to demonstrate full-stack engineering capabilities for internal Engineer II position*

---

**Status:** 🚧 Active Development | **Visibility:** 🔒 Private | **Target:** 🎯 Production-Ready by Nov 2026


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
