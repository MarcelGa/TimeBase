# Git Hooks (Husky.Net)

This project uses [Husky.Net](https://github.com/alirezanet/Husky.Net) to enforce code quality standards through Git hooks.

## Pre-commit Hook

The pre-commit hook automatically runs `dotnet format` to ensure all committed code follows the project's formatting standards defined in `.editorconfig`.

### What happens on commit?

1. You run `git commit`
2. The pre-commit hook runs automatically
3. `dotnet format` checks if code is properly formatted
4. If formatting issues are found:
   - Code is automatically formatted
   - Formatted files are staged
   - Commit proceeds with properly formatted code
5. If code is already formatted, commit proceeds immediately

### Setup (for new developers)

After cloning the repository, restore the dotnet tools:

```bash
dotnet tool restore
```

This will install Husky and set up the Git hooks automatically.

### Bypassing the hook (not recommended)

If you absolutely need to skip the pre-commit hook (e.g., for WIP commits):

```bash
git commit --no-verify -m "WIP: your message"
```

**Note:** This should be avoided in most cases to maintain code quality.

## Available Hooks

- **pre-commit**: Runs `dotnet format` to enforce code formatting

## Troubleshooting

If the hooks stop working:

```bash
# Reinstall hooks
dotnet husky install
```

If you get permission errors on Linux/Mac:

```bash
chmod +x .husky/pre-commit
```
