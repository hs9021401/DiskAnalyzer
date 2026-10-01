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

### Project boundaries

- `src/DiskAnalyzer.Core` is the single source of truth for scanning, NTFS parsing, models, search, export, and treemap layout. It targets `net10.0`; native scan and shell operations still require Windows.
- `src/DiskAnalyzer.UI` targets `net10.0-windows`, references Core, and owns WPF controls, ViewModels, and localization. Do not copy Core source files into the UI project.
- The test project references Core directly for engine tests and also references UI for WPF/ViewModel tests. UI tests must run on Windows and rendering tests require an STA thread.
- The published-executable smoke test is opt-in (`DISKANALYZER_RUN_EXE_TESTS=1`) and expects `DiskAnalyzer.exe` in the repository root. It launches the application; ordinary tests report it as skipped instead of silently passing when the executable is absent.
- Folder scans use the parallel directory walker in Auto/USN mode. USN enumeration is volume-wide; do not silently substitute it for a subtree scan.
- Automatic scans fall back directly from MFT to the directory walker. Explicit `ScanMode.UsnJournal` remains an experimental library option, not an automatic fast fallback: it must query file sizes separately and may fall back to the walker when metadata cannot be read.
- USN parsing rejects corrupt record lengths and unsupported layouts rather than guessing the next record boundary or silently counting unknown file sizes as zero.
- Scanners must wait for all workers to finish before aggregating or publishing a tree. Cancellation must propagate rather than trigger another scan strategy.
- Search runs against a size-sorted snapshot, debounces typing by 200 ms, checks cancellation during enumeration, and stops after 5,000 matches for the UI. Hover/selection rendering must not rebuild the treemap's base tiles.
- Batch deletion collects successful targets first, then replaces the search cache and refreshes summaries/views once. Failed targets stay in the model, and overlapping selected subtrees must not be counted twice. The old cache remains unchanged for in-flight searches.
- Log fallback modes and exception type/error codes without full paths or filenames. A failed Recycle Bin operation must not fall back to direct permanent deletion.

### General expectations

- Keep the existing C# nullable and naming conventions.
- Preserve the distinction between UI display text and filesystem paths.
- Avoid logging full user paths or file contents unless explicitly required for diagnostics.
- Update the relevant README or localized documentation when user-visible behavior changes.
- Do not commit `bin/`, `obj/`, portable publish output, installer output, or user-specific configuration files.

## Pull requests

Pull requests should be focused and reviewable. If a change affects more than one layer, describe the data flow between the scanner, model, ViewModel, and UI. Screenshots are helpful for visual changes, but please redact personal information.
