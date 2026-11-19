# LinkAction

LinkAction is a Windows desktop utility that registers as an HTTP/HTTPS link handler and adds a confirmation step for Barracuda-protected links opened from an Outlook-related process. Other links are forwarded to a configured, detected, or system-default browser.

It is a single-process .NET 8 Windows Forms application. There is no server, database, web API, external service client, production NuGet dependency, account system, or environment-file configuration.

## Features

- Windows URL association registration for http and https.
- Barracuda Safe Link detection using the linkprotect.cudasvc.com marker.
- Outlook caller detection by walking up to five parent processes.
- Target-host extraction from the Barracuda a query parameter.
- Exact-domain and subdomain allowlist matching.
- Optional browser executable override.
- Custom Yes/No WinForms warning with embedded logo.
- Local diagnostic logging.

## Technology Stack

| Area | Implementation |
| --- | --- |
| Language | C# |
| Targets | net8.0-windows executable and net8.0 core library |
| UI | Windows Forms |
| Project SDK | Microsoft.NET.Sdk |
| Output | WinExe |
| Native API | kernel32 Toolhelp snapshot API through P/Invoke |
| Packages | xUnit, its Visual Studio adapter and Microsoft.NET.Test.Sdk in the test project only |

## Architecture

Windows passes the URL to `Program.Main` through the registered handler. The executable supplies the Windows operations to `LinkWorkflow`. The workflow collects the inputs for `LinkPolicy`, interprets its decision and launches the original URL at a single call site.

| Condition, evaluated in order | Decision |
| --- | --- |
| First argument lacks an HTTP(S) prefix after trimming surrounding spaces and quotes | Reject |
| URL lacks the Barracuda marker | Open directly |
| No Outlook-like name among the first five ancestors | Open directly |
| Resolved target host equals an allowlist entry or is its subdomain | Open directly |
| All other protected links from Outlook, including unresolved destinations | Ask for confirmation |

The dialog displays the resolved host, falling back to the original URL. Only Yes authorizes the launch. The browser receives the protected URL so Barracuda can handle the redirect.

## Project structure

| Location | Responsibility |
| --- | --- |
| `Core/LinkAction.Core.csproj` | Portable .NET 8 library containing `LinkRequest`, `LinkPolicy`, `LinkDecision` and `LinkWorkflow`. |
| `LinkAction.csproj`, `Program.cs` | Windows executable, startup, dependency wiring and top-level failure handling. |
| `Windows/` | Configuration files, diagnostic log, Toolhelp ancestry inspection, browser selection and launch, and the WinForms confirmation dialog. |
| `Tests/LinkAction.Tests.csproj` | Portable policy and workflow tests referencing only the core project. |
| `Assets/Logo.png`, `*.reg` | Embedded dialog artwork and URL-handler registration files. |

See the [technical analysis](TECHNICAL_ANALYSIS.md) for the component boundaries, compatibility decisions and regression risks.

## Prerequisites

- Windows.
- .NET 8 SDK and Windows Desktop targeting pack.
- Outlook or another registered caller for Outlook-specific testing.
- A supported browser, or a browser override.

No global.json pins an SDK. The portable tests require the .NET 8 runtime and an SDK that supports .NET 8.

## Installation

From the repository root:

~~~powershell
dotnet restore .\LinkAction.sln
dotnet build .\LinkAction.sln --configuration Release
~~~

Output:

~~~text
bin\Release\net8.0-windows\LinkAction.exe
~~~

The registry files assume this installation path:

~~~text
C:\Program Files\LinkAction\LinkAction.exe
~~~

Install the build output:

~~~powershell
New-Item -ItemType Directory -Path 'C:\Program Files\LinkAction' -Force
Copy-Item '.\bin\Release\net8.0-windows\*' 'C:\Program Files\LinkAction' -Recurse -Force
~~~

Import one registry file as administrator:

- LinkAction-Register.reg: standard registration.
- LinkAction-Browser-Register.reg: standard registration plus WOW6432Node entries for 32-bit callers such as classic 32-bit Outlook.

These files register http and https through the LinkActionURL protocol and do not provide an uninstaller or browser-association rollback. Review them before importing in managed environments.

## Configuration

No environment variables are read. Runtime files are under %LOCALAPPDATA%\LinkAction:

| File | Required | Purpose |
| --- | --- | --- |
| browser.txt | No | Full path to an existing browser executable; takes priority over detection. |
| allowlist.txt | No | One trusted domain per line; blank lines and lines beginning with # are ignored. |
| log.txt | No | Append-only diagnostic log. |

The first run creates allowlist.txt with a template. Example:

~~~text
# LinkAction allowlist
yourcompany.com
corp.yourcompany.com
~~~

example.com matches example.com and portal.example.com, but not example.com.evil.test.

## Running and Browser Selection

Run with a URL argument:

~~~powershell
& 'C:\Program Files\LinkAction\LinkAction.exe' 'https://example.com'
~~~

For development:

~~~powershell
dotnet run --project .\LinkAction.csproj -- 'https://example.com'
~~~

There are no command-line switches. With no argument or a non-HTTP(S) argument, the process logs and exits. browser.txt is checked first; otherwise these paths are checked in order:

1. C:\Program Files\Google\Chrome\Application\chrome.exe
2. C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
3. C:\Program Files\Microsoft\Edge\Application\msedge.exe
4. C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
5. C:\Program Files\Mozilla Firefox\firefox.exe

If none exists, Windows shell execution uses the system default handler.

## Testing

Run the unit tests on any platform with .NET 8. This command restores the test packages and builds only the core and test projects.

~~~sh
dotnet test Tests/LinkAction.Tests.csproj --configuration Release
~~~

The suite covers URL and caller classification, host extraction, allowlist boundaries and the number of confirmation and launch calls. It uses in-memory inputs and delegates. Manual Windows checks cover the registered association, Outlook ancestry, dialog Yes/No/Escape, browser override and shell fallback, and both 32-bit and 64-bit callers.

## Deployment

No packaging or CI deployment is defined. Basic deployment is: build Release, copy output to the path in the registry file, import the appropriate registry file as administrator, configure per-user files, and test target callers.

An SDK self-contained publish command is available, but no publish profile is defined or validated:

~~~powershell
dotnet publish .\LinkAction.csproj --configuration Release --runtime win-x64 --self-contained true --output .\publish
~~~

Update registry paths if the published executable is installed elsewhere.

## Error Handling and Logging

Main catches top-level exceptions, logs when possible, and exits without a visible error dialog. Helpers return safe fallbacks. log.txt can contain complete URLs, query strings, and process ancestry, so treat it as potentially sensitive.

## Security Considerations and Limitations

- Caller trust is heuristic: process names and ancestry are not cryptographic proof of origin.
- Barracuda detection uses a substring marker rather than strict URI validation.
- The allowlist is a user-editable plain-text bypass control.
- Logs can expose sensitive URL query parameters.
- Registry commands use a fixed installation path and require machine-level privileges.
- The application does not validate TLS, inspect destination content, or detect malware/phishing.

## Troubleshooting

If no prompt appears, check the Barracuda marker, Outlook ancestry, allowlist, and %LOCALAPPDATA%\LinkAction\log.txt. If the wrong browser opens, inspect browser.txt and the hard-coded browser paths. If Windows cannot launch the app, verify the executable path in the .reg file and the required 32-bit registration. If build assets are missing, run dotnet restore first.

## Contributing

No formal contribution workflow, style guide, issue template, or CI checks are documented. Preserve the Windows/.NET 8 target, synchronize registry paths with deployment, and manually verify URL handling and registration changes.

## License

No license file or license declaration is present. Licensing is not documented and must be clarified before redistribution.
