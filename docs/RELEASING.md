# Release process

1. Update version and third-party notices.
2. Run formatting, Release build, all tests, `dotnet list package --vulnerable --include-transitive`, and representative manual conversions.
3. Publish self-contained `win-x64` CLI, Desktop, and Shell payloads.
4. Build the MSI and verify install, mandatory terms acceptance, Explorer verbs, shortcuts, repair, upgrade, and uninstall on clean Windows 10 and Windows 11 virtual machines.
5. Sign EXEs/DLLs, any sparse MSIX shell package, and the MSI with a trusted Authenticode certificate. Use `signtool` with SHA-256 and an RFC 3161 timestamp.
6. Generate an SPDX SBOM with Microsoft's SBOM tool or an equivalent maintained generator.
7. Create checksums and publish release notes and artifacts.

Signing certificates and passwords must be CI secrets or held by a hardware-backed signing service. They must never be committed, passed on ordinary command lines, or printed in logs.
