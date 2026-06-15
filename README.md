AI-Interviewer-Platform
An advanced, full-stack Mock Interview Simulator developed using ASP.NET Core (MVC), SQL Server, and integrated with the Gemini AI API Core. This platform automates technical and HR interview rounds, provides real-time intelligent feedback, and tracks comprehensive user performance analytics.

🚀 Key Features
AI-Powered Session Generation: Dynamic evaluation paths customized based on user-selected technical roles (e.g., Full-Stack Developer, Backend Engineer), experience levels, and custom question length.

Smart Evaluation Engine: Real-time analysis of user responses (both text-based answers and code snippets) using custom prompt engineering.

Deep Performance Analytics: Tracks custom metrics including overall technical score, AI feedback summaries, and question-by-question breakdowns.

Robust Database Architecture: Managed manually with strict relational constraints, custom identity seeding, cascading deletes (ON DELETE CASCADE) for optimal data cleaning, and relational mapping.

Resilient API Pipeline: Integrated handling for API throttling, custom network retry parameters, and fault-tolerant server response management.

🛠️ Tech Stack & Architecture
Backend Framework: ASP.NET Core 8.0 / .NET Core (MVC Architecture)

Database Management: Microsoft SQL Server (SSMS)

ORM: Entity Framework Core (Database-First Approach with Manual Customizations)

AI Core Integration: Google Gemini AI Studio API

Frontend Workflow: Bootstrap, HTML5, CSS3 (Neon-Dark UI Elements), JavaScript

📊 Database Schema Overview
The relational structure consists of four core interconnected tables optimized for production execution:

Users: Manages secure profile mappings and credentials.

InterviewSessions: Tracks active and completed assessment pipelines, roles, types, and lengths.

SessionQuestions: Stores generated prompts, code inputs, text answers, individual marks, and detailed AI feedback.

InterviewResults: Holds unified session overviews, overall grades, and holistic final feedback.
