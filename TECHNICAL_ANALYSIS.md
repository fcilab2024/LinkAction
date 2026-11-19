# LinkAction Technical Analysis

## Current component boundaries

The application runs in one process. The Windows executable and portable test project both reference `LinkAction.Core`. Core targets ordinary `net8.0`, so its build and test entry points work without the Windows Desktop SDK or runtime.

| Component | Input and responsibility |
| --- | --- |
| `LinkRequest` | Trims the first argument, records the HTTP(S) prefix and Barracuda marker checks, and extracts the display/allowlist host for protected links. Retains the original trimmed URL for launching. |
| `LinkPolicy`, `LinkDecision` | Classifies supplied ancestor names and evaluates a parsed request, a caller flag and allowlist lines. Returns `Reject`, `OpenDirectly` or `RequireConfirmation`. All inputs are data. |
| `LinkWorkflow` | Requests ancestry only for marked URLs. Reads the allowlist only for an Outlook caller with a known target. Interprets one decision and calls the launcher once after direct authorization or Yes. |
| `Program.Main` | Preserves the STA entry point, initializes per-user configuration, connects the workflow to Windows operations, passes the first argument and catches unhandled errors. |
| `ConfigurationFiles` | Creates the template and reads `allowlist.txt` and `browser.txt`. Missing or unreadable files supply an empty list or no override. |
| `ProcessAncestry` | Uses Toolhelp and process IDs to collect up to five parent names, nearest first. A short chain or inspection failure returns the names obtained so far. |
| `ConfirmationPrompt`, `ConfirmDialog` | Initialize WinForms on the confirmation path, load the embedded logo and map the dialog result to consent. |
| `BrowserLauncher` | Selects the configured executable, the first existing candidate in the original order, or shell execution. Calls `Process.Start` once. |
| `DiagnosticLog` | Appends timestamped diagnostics and handles failure to write the final error report. |

A separate core assembly makes the platform boundary enforceable through project references. The workflow takes method delegates for its five external operations. Tests supply ordinary functions and lists to observe those calls. The application uses concrete classes at startup and needs no DI container or mocking library.

## Compatibility and regression risks

- Prefix validation and the case-insensitive Barracuda substring check preserve existing routing, including unusual inputs such as `http://`. A marker embedded in a different outer host retains that outer host for allowlist matching.
- For Barracuda hosts, the first case-insensitive `a` parameter is unescaped once and parsed as an absolute URI. A missing host requires confirmation for Outlook. The original wrapper remains the browser argument.
- The Outlook heuristic recognizes names containing `outlook` (including `hxoutlook` and `microsoft.outlook`) or exactly matching `olk`, ignoring case. An unavailable ancestor can prevent detection, which permits direct opening under the existing rules.
- Allowlist entries still match exact domains and dot-delimited subdomains. Missing or unreadable entries grant no bypass.
- The one-launch guarantee applies to each handler invocation. Windows shell fallback still relies on the machine's default association. Registration, process timing and the dialog require Windows integration checks.

## Implementation changes affecting operations

- All authorization paths converge on one launch call. A launch exception reaches the existing top-level logging boundary without a retry. Explicit browser launches pass the URL as one argument through `ArgumentList`, including when it contains a literal quotation mark.
- The ancestry reader collects a bounded snapshot of names before classification. Diagnostics can include ancestors above the first Outlook match and now record the explicit decision.
- First-run template creation uses exclusive creation, so a concurrent start cannot overwrite a newly created allowlist.
- Deploy the full Release output, including `LinkAction.Core.dll`, alongside the executable. The executable name, registry command lines and per-user file formats are preserved.

## Validation

On Windows with SDK 8.0.426, the Release solution rebuild completed with zero warnings and zero errors. All 38 tests passed on the .NET 8 runtime with zero skips. Cross-platform execution has not been exercised on a second OS.

Run `dotnet build LinkAction.sln -c Release` on Windows and `dotnet test Tests/LinkAction.Tests.csproj -c Release` on any .NET 8 test host. The tests cover the decision matrix, malformed destinations, legacy marker behavior, exact and subdomain boundaries, ancestry depth, repeated evaluations, prompt text, declines and launch counts including a throwing launcher.

The automated tests substitute in-memory inputs for Windows operations. Manual checks remain for opening links from classic and new Outlook, registered HTTP/HTTPS handlers (including x86 callers), dialog appearance and keyboard interaction, browser selection, and shell fallback.

## Pre-refactor review

The following sections describe the earlier single-file implementation and its original build results.

### Scope

Inspected every tracked project file: Program.cs, LinkAction.csproj, LinkAction.sln, both .reg files, .gitignore, Assets/Logo.png, and the existing README. bin/, obj/, and .vs/ were treated as generated output. No credentials or secrets were found.

### Architecture

This is a single-project, single-process Windows Forms executable. Registry URL associations pass the first URL argument to Program.Main. Main performs validation and a linear decision pipeline, calling helpers for URL parsing, target extraction, process ancestry, allowlisting, browser selection, logging, and UI. There is no server, database, API, network client, service layer, DI container, or multi-project boundary.

### Technologies and dependencies

- C#; implicit usings and nullable enabled.
- .NET 8 target net8.0-windows.
- Windows Forms; OutputType WinExe.
- Standard .NET namespaces plus System.Drawing and System.Windows.Forms.
- kernel32.dll Toolhelp functions: CreateToolhelp32Snapshot, Process32First, Process32Next, CloseHandle.
- No explicit NuGet packages.
- Assets/Logo.png embedded as LinkAction.logo.png.

### Main responsibilities

| Component | Responsibility |
| --- | --- |
| Main | Startup, argument validation, decision flow, and top-level exception handling. |
| URL helpers | HTTP(S) validation, Barracuda target extraction, display text. |
| Allowlist helpers | Template creation and exact/subdomain matching. |
| Browser helpers | Override, known-path detection, and process launch. |
| Outlook detection | Up to five parent-process checks. |
| P/Invoke | Parent process IDs through Toolhelp snapshot enumeration. |
| ConfirmDialog | Topmost Yes/No warning dialog with logo. |
| Logging | ISO-8601 timestamped append-only text log. |

### Important workflows

- Startup creates %LOCALAPPDATA%\LinkAction and allowlist.txt if needed.
- Only args[0] is used; missing, blank, and non-HTTP(S) values exit.
- Non-Barracuda URLs open immediately.
- Barracuda URLs open immediately when no Outlook-like ancestor exists.
- Otherwise the target host is resolved, allowlisted hosts bypass the warning, and all other hosts require Yes.
- browser.txt has priority, followed by five fixed Chrome, Edge, and Firefox paths, then shell fallback.

### Configuration and integrations

There are no environment variables. The runtime files are allowlist.txt, browser.txt, and log.txt under the current user's LocalApplicationData. The .reg files create http/https associations through LinkActionURL and invoke C:\Program Files\LinkAction\LinkAction.exe. The browser registration file also mirrors entries under WOW6432Node, but does not add a corresponding 32-bit RegisteredApplications value.

### Verified validation

dotnet restore LinkAction.sln succeeded.
dotnet build LinkAction.sln --configuration Release --no-restore succeeded with zero errors.

Warnings:

- NETSDK1137: Microsoft.NET.Sdk.WindowsDesktop is no longer necessary according to the installed SDK.
- CS8604: possible null argument to the Font constructor at the dialog font construction.

No automated tests exist.

### Technical findings and gaps

1. All behavior is concentrated in one roughly 500-line source file, limiting separation of concerns and unit-testability.
2. The original review reported duplicate confirmation handling. The source inspected immediately before refactoring contained one Yes/No block and multiple direct-launch branches.
3. Barracuda detection is a substring Contains check, not strict host/query validation.
4. Caller detection is heuristic and process-name based.
5. The plain-text per-user allowlist is convenient but can be modified by any process with profile access.
6. Top-level failures are swallowed after logging and normally produce no user-visible error.
7. Full URLs are logged and may include sensitive query values.
8. Registry integration assumes a fixed path and has no uninstall or rollback script.
9. No license, tests, coverage, CI, release process, installer, publish profile, updater, Windows compatibility matrix, formal support process, threat model, or signed-binary procedure is documented.

### Limitations

Interactive behavior involving Outlook ancestry, Windows URL association resolution, shell fallback, registry permissions, and 32-bit callers was not exercised. The README identifies these as manual verification items.
