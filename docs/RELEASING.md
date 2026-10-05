# Release process

1. Update version and third-party notices.
2. Run formatting, Release build, all tests, `dotnet list package --vulnerable --include-transitive`, and representative manual conversions.
3. Publish self-contained `win-x64` CLI, Desktop, and Shell payloads.
4. Build the MSI and sparse shell package, then verify install, mandatory terms acceptance, both Explorer menu paths, direct background conversion, shortcuts, repair, upgrade, and uninstall on clean Windows 10 and Windows 11 virtual machines.
5. Sign EXEs/DLLs, the sparse MSIX shell package, and the MSI with a trusted Authenticode certificate. Use `signtool` with SHA-256 and an RFC 3161 timestamp. Register the sparse package with `Add-AppxPackage -Path <package> -ExternalLocation <install-dir>` from the installer and remove it during uninstall.
6. Generate an SPDX SBOM with Microsoft's SBOM tool or an equivalent maintained generator.
7. Create checksums and publish release notes and artifacts.

Signing certificates and passwords must be CI secrets or held by a hardware-backed signing service. They must never be committed, passed on ordinary command lines, or printed in logs.
