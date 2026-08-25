# Contributing to DiskAnalyzer

Thank you for your interest in contributing. DiskAnalyzer is a Windows-focused project, so changes that touch Win32, NTFS structures, WPF, or shell integration should include a short explanation of the platform assumptions.

## Before opening an issue

- Search existing issues first.
- Include the Windows version, project version or commit, scan mode, reproduction steps, and the smallest useful error message.
- Remove personal paths, filenames, screenshots, and other sensitive information unless it is necessary to reproduce the problem.
- For a suspected security issue, avoid posting details publicly until a private reporting channel is available on the GitHub repository.

## Development workflow

1. Fork the repository and create a focused branch.
2. Make the smallest change that addresses the issue.
3. Add or update a regression test when behavior changes or a bug is fixed.
4. Run the test suite:

   ```powershell
   dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
   ```

5. Open a pull request with a concise summary, testing notes, and any known limitations.

## Code and documentation expectations

- Keep the existing C# nullable and naming conventions.
- Preserve the distinction between UI display text and filesystem paths.
- Avoid logging full user paths or file contents unless explicitly required for diagnostics.
- Update the relevant README or localized documentation when user-visible behavior changes.
- Do not commit `bin/`, `obj/`, portable publish output, installer output, or user-specific configuration files.

## Pull requests

Pull requests should be focused and reviewable. If a change affects more than one layer, describe the data flow between the scanner, model, ViewModel, and UI. Screenshots are helpful for visual changes, but please redact personal information.
