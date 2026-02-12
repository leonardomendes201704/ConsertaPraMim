# ConsertaPraMim - Backend Status

## Completed Tasks
- [x] Create Visual Studio Solution (.sln)
- [x] Configure Clean Architecture Layers (API, Application, Domain, Infrastructure)
- [x] Entity Framework Core Setup (SQLite)
- [x] Domain Implementation:
    - [x] Entities: User, ProviderProfile, ServiceRequest, Proposal, Review
    - [x] Enums: UserRole, ProviderPlan, ServiceRequestStatus, ServiceCategory
- [x] Infrastructure Implementation:
    - [x] DbContext with Relations and Converters
    - [x] Repositories: UserRepository, ServiceRequestRepository
- [x] Application Implementation:
    - [x] Services: AuthService, ServiceRequestService
    - [x] DTOs and Interfaces
    - [x] JWT Authentication Logic
- [x] API Implementation:
    - [x] Controllers: AuthController, ServiceRequestsController
    - [x] Swagger Configuration
    - [x] JWT Bearer Auth Configuration
- [x] Database Migrations (InitialCreate applied)

## Next Steps (Backend)
- [x] Implement Proposal Logic (Create, Accept, Reject)
- [x] Implement Review Logic
- [x] Refine Provider Matching Algorithm (Radius, Category)
- [ ] Add Unit Tests (xUnit)
- [x] Add Validation Pipeline (FluentValidation)
- [ ] Setup Docker/Deployment

## How to Run
1. Navigate to `Backend` folder.
2. Run `dotnet run --project src/ConsertaPraMim.API`
3. Open `http://localhost:5109/swagger` (or configured port) to test API.
