# Contributing to Decorative Plant Backend

Thank you for your interest in contributing to the Decorative Plant Backend project! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Git Workflow](#git-workflow)
- [Commit Message Guidelines](#commit-message-guidelines)
- [Code Style and Standards](#code-style-and-standards)
- [Unit of Work (UOW) Pattern](#unit-of-work-uow-pattern)
- [Repository Pattern](#repository-pattern)
- [MediatR Behaviors](#mediatr-behaviors)
- [Testing](#testing)
- [Pull Request Process](#pull-request-process)
- [Husky Git Hooks](#husky-git-hooks)
- [Project Structure](#project-structure)
- [Environment Setup](#environment-setup)
- [Docker Setup](#docker-setup)
- [Supabase Integration](#supabase-integration)

## Code of Conduct

- Be respectful and inclusive
- Welcome newcomers and help them learn
- Focus on constructive feedback
- Follow the project's coding standards

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for Husky and commitlint)
- [Git](https://git-scm.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for local database and Redis)
- Visual Studio 2022 or VS Code (recommended)

### Initial Setup

1. **Fork and Clone the Repository**
   ```bash
   git clone https://github.com/your-username/Decorative-Plant_Monoservices.git
   cd Decorative-Plant_Monoservices
   ```

2. **Install Node.js Dependencies (for Husky)**
   ```bash
   npm install
   ```
   This will automatically set up Husky git hooks.

3. **Set Up Environment Variables**
   ```bash
   cp env.example .env
   ```
   Edit `.env` with your configuration (see [Environment Setup](#environment-setup) section for details).

4. **Start Docker Services (PostgreSQL and Redis)**
   ```bash
   docker-compose up -d postgres redis
   ```
   This starts the PostgreSQL database (with Supabase extensions) and Redis cache.

5. **Restore .NET Packages**
   ```bash
   dotnet restore
   ```

6. **Run Database Migrations**
   ```bash
   cd decorativeplant-be.API
   dotnet ef migrations add InitialCreate --project ../decorativeplant-be.Infrastructure
   dotnet ef database update --project ../decorativeplant-be.Infrastructure
   ```

7. **Run the Application**
   ```bash
   dotnet run --project decorativeplant-be.API
   ```
   
   Or use Docker Compose to run everything:
   ```bash
   docker-compose up -d
   ```

## Development Workflow

### Branch Naming Convention

- `feature/description` - New features
- `fix/description` - Bug fixes
- `refactor/description` - Code refactoring
- `docs/description` - Documentation updates
- `test/description` - Test additions/updates
- `chore/description` - Maintenance tasks

Examples:
- `feature/user-authentication`
- `fix/jwt-token-expiration`
- `refactor/repository-pattern`

### Creating a New Feature

1. **Create a feature branch from main**
   ```bash
   git checkout main
   git pull origin main
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**
   - Follow the project structure and patterns
   - Write clean, maintainable code
   - Add comments where necessary

3. **Test your changes**
   - Ensure the application builds successfully
   - Test your feature manually
   - Run any existing tests

4. **Commit your changes** (see [Commit Message Guidelines](#commit-message-guidelines))
   ```bash
   git add .
   git commit -m "feat: add user authentication endpoint"
   ```

5. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request** (see [Pull Request Process](#pull-request-process))

## Git Workflow

### Standard Workflow

We follow a **feature branch workflow**:

1. **Main branch** - Production-ready code
2. **Feature branches** - New features and changes
3. **Pull Requests** - Code review and integration

### Workflow Steps

```bash
# 1. Update your local main branch
git checkout main
git pull origin main

# 2. Create a new feature branch
git checkout -b feature/your-feature-name

# 3. Make your changes and commit
git add .
git commit -m "feat: your feature description"

# 4. Push to remote
git push origin feature/your-feature-name

# 5. Create Pull Request on GitHub
```

### Keeping Your Branch Updated

If the main branch has new commits while you're working:

```bash
# Switch to main and pull latest
git checkout main
git pull origin main

# Switch back to your feature branch
git checkout feature/your-feature-name

# Rebase or merge main into your branch
git rebase main
# OR
git merge main

# Resolve any conflicts if they occur
# Then push (force push if rebased)
git push origin feature/your-feature-name
# If rebased: git push --force-with-lease origin feature/your-feature-name
```

## Commit Message Guidelines

We use **Conventional Commits** format enforced by Husky and commitlint.

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Code style changes (formatting, missing semicolons, etc.)
- `refactor`: Code refactoring without changing functionality
- `perf`: Performance improvements
- `test`: Adding or updating tests
- `build`: Changes to build system or external dependencies
- `ci`: Changes to CI/CD configuration
- `chore`: Other changes (maintenance, etc.)
- `revert`: Revert a previous commit

### Examples

**Good commit messages:**
```
feat: add user registration endpoint
fix: resolve JWT token expiration issue
docs: update API documentation
refactor: improve repository pattern implementation
test: add unit tests for authentication service
chore: update NuGet packages
```

**Bad commit messages:**
```
update code
fix bug
changes
WIP
```

### Commit Message Validation

Husky will automatically validate your commit messages. If your message doesn't follow the format, the commit will be rejected with helpful error messages.

## Code Style and Standards

### C# Coding Standards

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Keep methods focused and small
- Add XML documentation comments for public APIs
- Use nullable reference types appropriately

### Code Formatting

- **Automatic formatting** is enforced via Husky pre-commit hook
- The `format-staged.sh` script formats C# files before commit
- Ensure your IDE is configured to use consistent formatting

### Project Structure

Follow the existing clean architecture structure:

```
decorativeplant-be.Domain/        # Domain entities and business logic
decorativeplant-be.Application/   # Application services, DTOs, CQRS
decorativeplant-be.Infrastructure/# Data access, external services
decorativeplant-be.API/           # Controllers, middleware, startup
```

### Architecture Patterns

- **CQRS**: Use MediatR commands and queries
  - Commands: Operations that modify state (Create, Update, Delete)
  - Queries: Operations that read data (Get, List, Search)
  - Each feature has its own folder: `Features/{FeatureName}/Commands` and `Features/{FeatureName}/Queries`
- **Repository Pattern**: Use IRepository and UnitOfWork (see [Repository Pattern](#repository-pattern) section)
- **Unit of Work**: Use IUnitOfWork for transaction management (see [Unit of Work (UOW) Pattern](#unit-of-work-uow-pattern) section)
- **Dependency Injection**: Register all services properly
- **DTOs**: Use AutoMapper for entity-to-DTO mapping
- **Validation**: Use FluentValidation for input validation (see [MediatR Behaviors](#mediatr-behaviors) section)

### Unit of Work (UOW) Pattern

The **Unit of Work** pattern ensures that all changes to multiple repositories are committed together as a single transaction, maintaining data consistency.

#### What is Unit of Work?

The `IUnitOfWork` interface provides:
- **Transaction Management**: Begin, commit, or rollback database transactions
- **Change Tracking**: Save all changes across multiple repositories atomically
- **Consistency**: Ensures all-or-nothing operations

#### When to Use Unit of Work

Use UOW when:
- ✅ Multiple repository operations need to be atomic (all succeed or all fail)
- ✅ You need explicit transaction control
- ✅ Working with multiple entities that must be saved together
- ✅ Implementing complex business operations

**Example: Creating an order with order items**
```csharp
// All operations must succeed together
await _unitOfWork.BeginTransactionAsync();
try
{
    var order = await _orderRepository.AddAsync(newOrder);
    foreach (var item in orderItems)
    {
        item.OrderId = order.Id;
        await _orderItemRepository.AddAsync(item);
    }
    await _unitOfWork.SaveChangesAsync();
    await _unitOfWork.CommitTransactionAsync();
}
catch
{
    await _unitOfWork.RollbackTransactionAsync();
    throw;
}
```

#### Using Unit of Work in Handlers

**Basic Usage (without explicit transactions):**
```csharp
public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateProductHandler(
        IRepositoryFactory repositoryFactory,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var productRepository = _repositoryFactory.CreateRepository<Product>();
        
        var product = _mapper.Map<Product>(request);
        await productRepository.AddAsync(product, cancellationToken);
        
        // SaveChangesAsync commits all pending changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return _mapper.Map<ProductDto>(product);
    }
}
```

**Advanced Usage (with explicit transactions):**
```csharp
public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
{
    var orderRepository = _repositoryFactory.CreateRepository<Order>();
    var orderItemRepository = _repositoryFactory.CreateRepository<OrderItem>();
    var inventoryRepository = _repositoryFactory.CreateRepository<Inventory>();

    // Begin transaction for atomic operation
    await _unitOfWork.BeginTransactionAsync(cancellationToken);
    
    try
    {
        // Create order
        var order = new Order { /* ... */ };
        await orderRepository.AddAsync(order, cancellationToken);
        
        // Create order items and update inventory
        foreach (var item in request.Items)
        {
            var orderItem = new OrderItem { OrderId = order.Id, /* ... */ };
            await orderItemRepository.AddAsync(orderItem, cancellationToken);
            
            // Update inventory atomically
            var inventory = await inventoryRepository.FirstOrDefaultAsync(
                i => i.ProductId == item.ProductId, cancellationToken);
            if (inventory != null)
            {
                inventory.Quantity -= item.Quantity;
                await inventoryRepository.UpdateAsync(inventory, cancellationToken);
            }
        }
        
        // Save all changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // Commit transaction
        await _unitOfWork.CommitTransactionAsync(cancellationToken);
        
        return _mapper.Map<OrderDto>(order);
    }
    catch (Exception)
    {
        // Rollback on any error
        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
        throw;
    }
}
```

#### Unit of Work Best Practices

1. **Always inject `IUnitOfWork`** in your handlers, not `ApplicationDbContext` directly
2. **Use transactions** for operations that modify multiple entities
3. **Always rollback** on exceptions when using explicit transactions
4. **Call `SaveChangesAsync`** after all repository operations
5. **Don't call `SaveChangesAsync` multiple times** - batch your changes
6. **Use `CancellationToken`** for all async operations

### Repository Pattern

The **Repository Pattern** abstracts data access logic and provides a consistent interface for working with entities.

#### Repository Factory

Use `IRepositoryFactory` to create repositories for specific entities:

```csharp
// In your handler constructor
private readonly IRepositoryFactory _repositoryFactory;
private readonly IUnitOfWork _unitOfWork;

public MyHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
{
    _repositoryFactory = repositoryFactory;
    _unitOfWork = unitOfWork;
}

// In your handler method
var productRepository = _repositoryFactory.CreateRepository<Product>();
var categoryRepository = _repositoryFactory.CreateRepository<Category>();
```

#### Available Repository Methods

All repositories implement `IRepository<T>` with these methods:

- `GetByIdAsync(int id)` - Get entity by ID
- `GetAllAsync()` - Get all entities
- `FindAsync(Expression<Func<T, bool>> predicate)` - Find entities matching predicate
- `FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)` - Get first matching entity
- `AddAsync(T entity)` - Add new entity (sets `CreatedAt` automatically)
- `UpdateAsync(T entity)` - Update entity (sets `UpdatedAt` automatically)
- `DeleteAsync(T entity)` - Delete entity
- `ExistsAsync(Expression<Func<T, bool>> predicate)` - Check if entity exists
- `CountAsync(Expression<Func<T, bool>>? predicate)` - Count entities

#### Repository Examples

**Querying:**
```csharp
var productRepository = _repositoryFactory.CreateRepository<Product>();

// Get by ID
var product = await productRepository.GetByIdAsync(1, cancellationToken);

// Find all active products
var activeProducts = await productRepository.FindAsync(
    p => p.IsActive == true, 
    cancellationToken);

// Get first product by name
var product = await productRepository.FirstOrDefaultAsync(
    p => p.Name == "Rose", 
    cancellationToken);

// Check existence
var exists = await productRepository.ExistsAsync(
    p => p.Sku == "SKU123", 
    cancellationToken);

// Count
var count = await productRepository.CountAsync(
    p => p.Price > 100, 
    cancellationToken);
```

**Modifying:**
```csharp
var productRepository = _repositoryFactory.CreateRepository<Product>();

// Add
var newProduct = new Product { Name = "Rose", Price = 29.99m };
await productRepository.AddAsync(newProduct, cancellationToken);
await _unitOfWork.SaveChangesAsync(cancellationToken);

// Update
var product = await productRepository.GetByIdAsync(1, cancellationToken);
if (product != null)
{
    product.Price = 39.99m;
    await productRepository.UpdateAsync(product, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
}

// Delete
var product = await productRepository.GetByIdAsync(1, cancellationToken);
if (product != null)
{
    await productRepository.DeleteAsync(product, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
}
```

#### BaseEntity Pattern

All domain entities inherit from `BaseEntity`, which provides:
- `Id` (int) - Primary key
- `CreatedAt` (DateTime) - Automatically set when entity is created
- `UpdatedAt` (DateTime?) - Automatically set when entity is updated

**Example:**
```csharp
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    // Id, CreatedAt, UpdatedAt inherited from BaseEntity
}
```

The repository automatically manages `CreatedAt` and `UpdatedAt` timestamps:
- `AddAsync` sets `CreatedAt` to `DateTime.UtcNow`
- `UpdateAsync` sets `UpdatedAt` to `DateTime.UtcNow`

### MediatR Behaviors

MediatR **Pipeline Behaviors** are cross-cutting concerns that execute before and after request handlers. They provide a way to add common functionality (validation, logging, etc.) without modifying individual handlers.

#### Available Behaviors

1. **ValidationBehavior** - Automatically validates requests using FluentValidation
2. **LoggingBehavior** - Logs request handling with performance metrics

#### How Behaviors Work

Behaviors execute in the order they're registered:
1. **ValidationBehavior** runs first (validates input)
2. **LoggingBehavior** runs second (logs request start/end)
3. **Handler** executes
4. Behaviors complete in reverse order

#### ValidationBehavior

**What it does:**
- Automatically finds and runs all `IValidator<TRequest>` validators
- Throws `ValidationException` if validation fails
- Only executes if validators exist for the request type

**How to use:**
1. Create a validator for your command/query:
```csharp
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(100).WithMessage("Product name must not exceed 100 characters");
        
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}
```

2. The validator is automatically discovered and used:
```csharp
// In your controller
[HttpPost]
public async Task<IActionResult> CreateProduct(CreateProductCommand command)
{
    // ValidationBehavior automatically validates before handler executes
    var result = await _mediator.Send(command);
    return Ok(result);
}
```

**Validation errors are automatically handled:**
- Returns `400 Bad Request` with error details
- Error response format:
```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    "Product name is required",
    "Price must be greater than 0"
  ],
  "statusCode": 400
}
```

#### LoggingBehavior

**What it does:**
- Logs when a request handler starts processing
- Logs when a request handler completes (with execution time)
- Logs errors if the handler throws an exception

**Example logs:**
```
[INFO] Handling CreateProductCommand
[INFO] Handled CreateProductCommand in 45ms
```

Or on error:
```
[INFO] Handling CreateProductCommand
[ERROR] Error handling CreateProductCommand in 12ms
```

**No configuration needed** - works automatically for all MediatR requests.

#### Custom Behaviors

To add your own behavior:

1. **Create the behavior:**
```csharp
public class CustomBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Pre-processing (before handler)
        // ... your logic here ...
        
        var response = await next(); // Execute handler
        
        // Post-processing (after handler)
        // ... your logic here ...
        
        return response;
    }
}
```

2. **Register in `ApplicationServiceRegistration.cs`:**
```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CustomBehavior<,>));
```

**Important:** Order matters! Behaviors execute in registration order.

#### Exception Handling

Custom exceptions are automatically handled by `ExceptionHandlingMiddleware`:

- **ValidationException** → `400 Bad Request`
- **NotFoundException** → `404 Not Found`
- **UnauthorizedException** → `401 Unauthorized`
- **Other exceptions** → `500 Internal Server Error`

**Creating custom exceptions:**
```csharp
// In your handler
if (product == null)
{
    throw new NotFoundException("Product not found");
}

if (!user.HasPermission)
{
    throw new UnauthorizedException("Insufficient permissions");
}
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests for a specific project
dotnet test decorativeplant-be.Tests
```

### Writing Tests

- Write unit tests for business logic
- Write integration tests for API endpoints
- Aim for good test coverage
- Follow AAA pattern (Arrange, Act, Assert)

## Pull Request Process

### Before Submitting

1. ✅ Ensure your code builds without errors
2. ✅ All tests pass
3. ✅ Code follows project standards
4. ✅ Commit messages follow conventional format
5. ✅ No merge conflicts with main branch
6. ✅ Environment variables documented (if new ones added)

### PR Checklist

- [ ] Code compiles without errors
- [ ] All tests pass
- [ ] Code follows project structure and patterns
- [ ] Commit messages follow conventional format
- [ ] Documentation updated (if needed)
- [ ] Environment variables documented (if added)
- [ ] No sensitive data in commits

### PR Description Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
How was this tested?

## Checklist
- [ ] Code follows project standards
- [ ] Tests added/updated
- [ ] Documentation updated
```

### Review Process

1. PR is created and automatically triggers CI checks
2. Maintainers review the code
3. Address any feedback or requested changes
4. Once approved, maintainers will merge

## Husky Git Hooks

This project uses [Husky](https://typicode.github.io/husky/) to enforce code quality and commit standards.

### Pre-Commit Hook

**What it does:**
- Automatically formats staged C# files using `dotnet format`
- Runs lint-staged to ensure code quality
- **Checks for build errors** - validates that the solution compiles successfully
- Blocks commits if the code doesn't build

**Execution Order:**
1. Code formatting (whitespace, style, analyzers)
2. Build validation (ensures solution compiles)
3. Commit proceeds only if all checks pass

**What you need to know:**
- Runs automatically before each commit
- If formatting or build fails, the commit is blocked
- Formatted files are automatically staged
- Build errors must be fixed before committing

**Example output:**
```bash
$ git commit -m "feat: add new feature"
Checking for build errors...
❌ Build failed! Please fix build errors before committing.
```

**To skip (not recommended):**
```bash
git commit --no-verify -m "your message"
```
⚠️ **Warning:** Skipping hooks may allow broken code to be committed!

### Commit-Msg Hook

**What it does:**
- Validates commit message format using commitlint
- Ensures commits follow Conventional Commits format

**What you need to know:**
- Runs automatically when you commit
- Invalid commit messages are rejected
- Provides helpful error messages with examples

**Example of rejected commit:**
```bash
$ git commit -m "update code"
Invalid Commit Message Format
----------------------------------------------------
Examples of valid commits:
   feat: add login page
   fix: resolve navigation bug
   ...
----------------------------------------------------
```

### Setting Up Husky

Husky is automatically set up when you run `npm install`. If you need to set it up manually:

```bash
npm install
npx husky install
```

### Troubleshooting Husky

**Hooks not running?**
```bash
# Reinstall Husky
npm install
npx husky install

# Make hooks executable (Linux/Mac)
chmod +x .husky/*
```

**Build errors in pre-commit?**
```bash
# Build manually to see detailed error messages
dotnet build DecorativePlant-BE.sln

# Common issues:
# - Missing NuGet packages: dotnet restore
# - Syntax errors: Fix compilation errors
# - Missing references: Check project references
```

**Skip hooks temporarily (use with caution):**
```bash
git commit --no-verify -m "message"
```
⚠️ **Warning:** Only skip hooks if absolutely necessary. Build errors should be fixed before committing!

## Project Structure

### Clean Architecture Layers

1. **Domain Layer** (`decorativeplant-be.Domain`)
   - Entities
   - Value Objects
   - Domain Interfaces

2. **Application Layer** (`decorativeplant-be.Application`)
   - Commands/Queries (CQRS)
   - Handlers
   - DTOs
   - Services
   - Validators

3. **Infrastructure Layer** (`decorativeplant-be.Infrastructure`)
   - Data Access (DbContext, Repositories)
   - External Services
   - Identity Configuration
   - JWT Implementation

4. **API Layer** (`decorativeplant-be.API`)
   - Controllers
   - Middleware
   - Startup Configuration

## Environment Setup

### Required Environment Variables

**Quick setup:**
```bash
cp env.example .env
# Edit .env with your values
```

### Key Configuration

The `.env` file should contain:

- **Database Connection String** (`ConnectionStrings__DefaultConnection`)
  - Local Docker: `Host=localhost;Port=5432;Database=DecorativePlantDB;Username=postgres;Password=postgres`
  - Supabase Cloud: `Host=your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password`

- **Redis Connection String** (`ConnectionStrings__Redis`)
  - Local Docker: `redis:6379` (from within container) or `localhost:6379` (from host)
  - Redis Labs: `redis://username:password@host:port`

- **JWT Settings**
  - `JwtSettings__SecretKey`: At least 32 characters for HS256 algorithm
  - `JwtSettings__Issuer`: Your API identifier
  - `JwtSettings__Audience`: Your client identifier
  - `JwtSettings__AccessTokenExpirationMinutes`: Access token lifetime (default: 30)
  - `JwtSettings__RefreshTokenExpirationDays`: Refresh token lifetime (default: 7)

- **Environment** (`ASPNETCORE_ENVIRONMENT`)
  - `Development`, `Staging`, or `Production`

See `env.example` for detailed examples and comments.

## Docker Setup

This project uses Docker Compose for local development with PostgreSQL and Redis.

### Quick Start

```bash
# Start all services (PostgreSQL, Redis, API)
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Stop and remove volumes (clears database data)
docker-compose down -v
```

### Services

1. **PostgreSQL Database** (`postgres`)
   - Image: `supabase/postgres:16.1.1.117` (includes Supabase extensions and RLS support)
   - Port: `5432`
   - Database: `DecorativePlantDB`
   - Username: `postgres`
   - Password: `postgres` (change in production!)
   - Includes: PostgreSQL extensions (pg_stat_statements, pgcrypto, uuid-ossp, postgis, etc.)
   - Supports: Row Level Security (RLS) policies
   - Data persisted in `postgres_data` volume

2. **Redis Cache** (`redis`)
   - Port: `6379`
   - Used for refresh token storage
   - Data persisted in `redis_data` volume

3. **API Application** (`api`)
   - Ports: `8080` (HTTP), `8081` (HTTPS)
   - Automatically connects to PostgreSQL and Redis
   - Logs available in `./logs` directory

### Running Migrations with Docker

```bash
# Ensure the database is running
docker-compose up -d postgres

# Run migrations from host machine
dotnet ef database update --project decorativeplant-be.Infrastructure --startup-project decorativeplant-be.API
```

### Accessing Services

**Database:**
```bash
# Using psql from host (if installed)
psql -h localhost -p 5432 -U postgres -d DecorativePlantDB

# Using Docker
docker-compose exec postgres psql -U postgres -d DecorativePlantDB
```

**Redis:**
```bash
# Using redis-cli from host (if installed)
redis-cli -h localhost -p 6379

# Using Docker
docker-compose exec redis redis-cli
```

### Troubleshooting Docker

**Port Already in Use:**
- Change ports in `docker-compose.yml` if 5432, 6379, 8080, or 8081 are in use

**Database Connection Issues:**
- Ensure PostgreSQL container is healthy: `docker-compose ps`
- Check connection string format in `.env`
- Verify network connectivity: `docker-compose exec api ping postgres`

## Supabase Integration

This project uses the `supabase/postgres` Docker image for local development, providing Supabase-compatible PostgreSQL with extensions and RLS support.

### What's Included

**✅ Using Supabase PostgreSQL Image:**
- **PostgreSQL Extensions**:
  - `pg_stat_statements` - Query performance statistics
  - `pgcrypto` - Cryptographic functions
  - `uuid-ossp` - UUID generation
  - `postgis` - Geographic objects support
  - `pgjwt` - JSON Web Token support
  - And more Supabase-specific extensions

- **Row Level Security (RLS)**:
  - Full RLS support for fine-grained access control
  - Test RLS policies locally before deploying to Supabase cloud
  - Compatible with Supabase's RLS implementation

- **Supabase-Compatible Schema**:
  - Matches Supabase production environment
  - Easy migration to Supabase cloud
  - Same extensions and features available

**❌ Not Using (Building Our Own):**
- **Supabase Auth**: Using ASP.NET Core Identity + JWT instead
- **Supabase REST API**: Building our own REST API with ASP.NET Core
- **Supabase Frontend SDK**: Not needed for backend development

### Using Supabase Extensions

**Example: Using UUID Extension**
```sql
-- Enable uuid-ossp extension (already available in supabase/postgres)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Use UUID in your tables
CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL
);
```

**Example: Using pgcrypto**
```sql
-- Enable pgcrypto extension
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Use cryptographic functions
SELECT crypt('password', gen_salt('bf'));
```

**Example: Row Level Security (RLS)**
```sql
-- Enable RLS on a table
ALTER TABLE products ENABLE ROW LEVEL SECURITY;

-- Create a policy
CREATE POLICY "Users can view their own products"
ON products
FOR SELECT
USING (auth.uid() = user_id);
```

Note: In local development, you'll need to mock `auth.uid()` or use your own authentication context since we're using ASP.NET Core Identity, not Supabase Auth.

### Migrating to Supabase Cloud

When ready to deploy to Supabase cloud:

1. **Get Connection String**:
   - Go to Supabase Dashboard > Settings > Database
   - Copy the connection string

2. **Update Environment Variables**:
   ```env
   ConnectionStrings__DefaultConnection=Host=your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password
   ```

3. **Run Migrations**:
   ```bash
   dotnet ef database update --project decorativeplant-be.Infrastructure --startup-project decorativeplant-be.API
   ```

4. **Verify Extensions**:
   ```sql
   -- Check available extensions
   SELECT * FROM pg_available_extensions;
   
   -- Enable needed extensions
   CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
   CREATE EXTENSION IF NOT EXISTS "pgcrypto";
   ```

### Available Extensions

The `supabase/postgres` image includes these extensions (and more):
- `pg_stat_statements` - Query performance monitoring
- `pgcrypto` - Cryptographic functions
- `uuid-ossp` - UUID generation
- `postgis` - Geographic/spatial data
- `pgjwt` - JWT support
- `pg_net` - Network requests from PostgreSQL
- `pg_graphql` - GraphQL support
- `pg_hashids` - HashID generation
- `pg_jsonschema` - JSON schema validation

### Benefits of This Approach

1. **Local Development Matches Production**: Same extensions and features
2. **RLS Testing**: Test RLS policies locally before deploying
3. **Extension Compatibility**: Use Supabase extensions in your schema
4. **Easy Migration**: Seamless transition to Supabase cloud
5. **Full Control**: Build your own auth and API while leveraging Supabase's PostgreSQL features

## Additional Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [Husky Documentation](https://typicode.github.io/husky/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

## Getting Help

- Open an issue for bugs or feature requests
- Check existing issues before creating new ones
- Provide clear descriptions and reproduction steps
- Be patient and respectful

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to Decorative Plant Backend! 🎉
