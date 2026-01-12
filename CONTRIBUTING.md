# Contributing to Decorative Plant Backend

Thank you for your interest in contributing to the Decorative Plant Backend project! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Git Workflow](#git-workflow)
- [Commit Message Guidelines](#commit-message-guidelines)
- [Code Style and Standards](#code-style-and-standards)
- [Testing](#testing)
- [Pull Request Process](#pull-request-process)
- [Husky Git Hooks](#husky-git-hooks)
- [Project Structure](#project-structure)
- [Environment Setup](#environment-setup)

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
- SQL Server (LocalDB or full instance)
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
   Edit `.env` with your configuration (see [README_ENV.md](README_ENV.md) for details).

4. **Restore .NET Packages**
   ```bash
   dotnet restore
   ```

5. **Run Database Migrations**
   ```bash
   cd decorativeplant-be.API
   dotnet ef migrations add InitialCreate --project ../decorativeplant-be.Infrastructure
   dotnet ef database update --project ../decorativeplant-be.Infrastructure
   ```

6. **Run the Application**
   ```bash
   dotnet run --project decorativeplant-be.API
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
- **Repository Pattern**: Use IRepository and UnitOfWork
- **Dependency Injection**: Register all services properly
- **DTOs**: Use AutoMapper for entity-to-DTO mapping
- **Validation**: Use FluentValidation for input validation

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

See [README_ENV.md](README_ENV.md) for detailed environment variable setup.

**Quick setup:**
```bash
cp env.example .env
# Edit .env with your values
```

### Key Configuration

- Database connection string
- JWT secret key and settings
- Application environment

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
