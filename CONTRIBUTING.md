# Contributing to VoxTether

Thank you for your interest in contributing to VoxTether! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Code Style](#code-style)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Enhancements](#suggesting-enhancements)

## Code of Conduct

This project adheres to a code of conduct that all contributors are expected to follow:

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on what is best for the community
- Show empathy towards other community members
- Be collaborative and constructive in discussions

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/VoxTether.git
   cd VoxTether
   ```
3. **Add the upstream repository**:
   ```bash
   git remote add upstream https://github.com/KennethHeine/VoxTether.git
   ```

## Development Setup

### Backend (Python)

```bash
cd src/backend

# Create virtual environment
python -m venv venv
.\venv\Scripts\Activate.ps1  # Windows
# OR
source venv/bin/activate  # Linux/Mac

# Install dependencies
pip install -r requirements.txt
pip install -r ../../requirements-dev.txt

# Run backend server
python -m uvicorn main:app --host 127.0.0.1 --port 5678
```

### Frontend (Electron)

```bash
cd src/frontend-electron

# Install dependencies
npm install

# Run frontend in development mode
npm start
```

## Making Changes

1. **Create a new branch** for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   # OR
   git checkout -b fix/your-bug-fix
   ```

2. **Make your changes** following the code style guidelines

3. **Test your changes** thoroughly

4. **Commit your changes** with clear, descriptive messages:
   ```bash
   git add .
   git commit -m "feat: add new feature description"
   # OR
   git commit -m "fix: resolve issue with X"
   ```

### Commit Message Guidelines

Use conventional commits format:

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `style:` - Code style changes (formatting, etc.)
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

Examples:
```
feat: add support for custom models
fix: resolve audio recording timeout issue
docs: update installation instructions
test: add unit tests for transcriber service
```

## Code Style

### Python (Backend)

- Follow [PEP 8](https://peps.python.org/pep-0008/) style guidelines
- Use type hints where appropriate
- Maximum line length: 100 characters
- Use `ruff` for linting:
  ```bash
  ruff check src/backend/
  ```
- Use `black` for formatting (optional):
  ```bash
  black src/backend/
  ```

### JavaScript (Frontend)

- Use ESLint for linting:
  ```bash
  cd src/frontend-electron
  npm run lint
  ```
- Use consistent indentation (2 spaces)
- Use modern ES6+ syntax
- Add JSDoc comments for complex functions

## Testing

### Backend Tests

Run backend tests with pytest:

```bash
cd src/backend
pytest tests/ -v
```

Run with coverage:

```bash
pytest tests/ --cov=. --cov-report=html
```

### Frontend Tests

Run Playwright E2E tests:

```bash
cd src/frontend-electron
npm test
```

Run tests in headed mode:

```bash
npm run test:headed
```

### Writing Tests

- Write tests for all new features
- Ensure tests are deterministic and isolated
- Use descriptive test names
- Follow existing test patterns in the codebase

## Submitting Changes

1. **Push your changes** to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create a Pull Request** on GitHub:
   - Go to the original VoxTether repository
   - Click "New Pull Request"
   - Select your fork and branch
   - Fill out the PR template with:
     - Clear description of changes
     - Related issue numbers (if applicable)
     - Screenshots (for UI changes)
     - Testing performed

3. **Wait for review**:
   - Address any feedback from maintainers
   - Make requested changes by pushing new commits
   - Once approved, your PR will be merged

### Pull Request Checklist

Before submitting your PR, ensure:

- [ ] Code follows project style guidelines
- [ ] All tests pass locally
- [ ] New tests added for new features
- [ ] Documentation updated (if applicable)
- [ ] Commit messages follow conventions
- [ ] No merge conflicts with main branch
- [ ] CI/CD checks pass

## Reporting Bugs

When reporting bugs, please include:

1. **Clear title** describing the issue
2. **Steps to reproduce** the bug
3. **Expected behavior** vs **actual behavior**
4. **Environment details**:
   - OS version (Windows 10/11)
   - Python version (for backend)
   - Node.js version (for frontend)
   - VoxTether version
5. **Screenshots or logs** (if applicable)
6. **Error messages** (full stack traces)

Use the GitHub issue template when available.

## Suggesting Enhancements

When suggesting new features or enhancements:

1. **Check existing issues** to avoid duplicates
2. **Provide clear use case** and motivation
3. **Describe the proposed solution** in detail
4. **Consider alternatives** you've thought about
5. **Discuss impact** on existing functionality

## Development Tips

### Running Linting

```bash
# Backend
cd src/backend
ruff check .

# Frontend
cd src/frontend-electron
npm run lint
```

### Building for Release

```bash
# Build backend executable
cd src/backend
pyinstaller --onefile --name vox-backend main.py

# Build frontend
cd src/frontend-electron
npm run build
```

### Debugging

- **Backend**: Use Python debugger or add logging
- **Frontend**: Use Chrome DevTools (View → Toggle Developer Tools in Electron)
- **Check logs**:
  - Backend: `%APPDATA%\VoxTether\logs\backend.log`
  - Frontend: `%APPDATA%\VoxTether\logs\frontend.log`

### Common Issues

1. **Model download fails**: Check internet connection and HuggingFace Hub status
2. **Audio recording not working**: Check microphone permissions in Windows settings
3. **Hotkey conflicts**: Try different key combinations in settings
4. **GPU not detected**: Ensure CUDA toolkit is installed and nvidia-smi works

## Questions?

If you have questions about contributing:

- Open a [GitHub Discussion](https://github.com/KennethHeine/VoxTether/discussions)
- Check existing [documentation](docs/)
- Review [closed issues](https://github.com/KennethHeine/VoxTether/issues?q=is%3Aissue+is%3Aclosed) for similar questions

## License

By contributing to VoxTether, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to VoxTether! 🎙️
