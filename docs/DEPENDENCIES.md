# Dependency and license evaluation

| Component | Version / source | License | Role | Distribution decision |
|---|---|---|---|---|
| .NET | 10 LTS | MIT and component notices | Runtime and WPF | Publish self-contained release payloads; retain Microsoft notices. |
| PDFsharp GDI | 6.2.4, NuGet.org | MIT | Create, merge, split, extract, and rotate PDFs | Included. Pinned centrally. |
| System.Security.Cryptography.Pkcs | 8.0.2, NuGet.org | MIT | Transitive PDFsharp dependency on portable targets | Centrally pinned to Microsoft's patched version for CVE-2026-70162 and CVE-2026-84008. |
| LibreOffice | User-installed supported release | MPL-2.0 plus bundled third-party notices | Local DOCX/XLSX/PPTX conversion | Detected, not bundled in the MVP MSI. Bundling requires the complete LibreOffice license set. |
| Microsoft Office | Optional user-installed application | Commercial, separately licensed by the user | Local fallback conversion through Windows COM automation | Detected, never bundled or redistributed; BilisPDF adds no Office binary dependency. |
| WiX Toolset SDK/UI | 5.0.2, build-time only | Microsoft Reciprocal License | Build MSI and license UI | Not shipped as an application runtime dependency. Version 5 is pinned because later releases require separate OSMF EULA acceptance by each builder. |
| MSTest + test SDK | 4.0.1 / 18.0.0 | MIT | Automated tests | Development/CI only. |
| GitHub Actions official actions | Pinned commit SHAs | MIT | CI checkout/setup/artifacts | CI only; pins are reviewed during dependency updates. |

The project intentionally avoids commercial PDF SDKs, cloud conversion APIs, SQLite, and telemetry SDKs. Microsoft Office automation is an optional local fallback only when the user already has Office installed. `THIRD-PARTY-NOTICES.md` is included in release payloads.

Before a dependency update, verify its upstream package owner, license, release notes, published checksum/signature when available, transitive graph, and known vulnerabilities. Preview packages are not accepted for production builds.
