# Release process

1. Update version and third-party notices.
2. Run formatting, Release build, all tests, `dotnet list package --vulnerable --include-transitive`, and representative manual conversions.
3. Publish self-contained `win-x64` CLI, Desktop, and Shell payloads.
4. Build the MSI and sparse shell package, then verify install, mandatory terms acceptance, both Explorer menu paths, direct background conversion, shortcuts, repair, upgrade, and uninstall on clean Windows 10 and Windows 11 virtual machines.
5. For a verified-publisher release, sign EXEs/DLLs, the sparse MSIX shell package, and the MSI with a trusted Authenticode certificate as described below. Unsigned preview MSI builds register the installed manifest only on machines with Developer Mode already enabled; other machines get the classic menu only. Clearly label unsigned GitHub releases. Do not describe them as Windows verified.
6. Generate an SPDX SBOM with Microsoft's SBOM tool or an equivalent maintained generator.
7. Create checksums and publish release notes and artifacts.

Signing certificates and passwords must be CI secrets or held by a hardware-backed signing service. They must never be committed, passed on ordinary command lines, or printed in logs.

For local development, `installer/Install-Development.ps1` copies the built payload to `%LOCALAPPDATA%\Programs\BilisPDF` and registers the manifest there. Never register a manifest from disposable build output. Verify `Get-AppxPackage -Name Arjunren.EasyPDF.Shell | Get-AppxPackageManifest` succeeds after registration and after deleting build artifacts.

## Direct-download code signing (deferred for 0.4)

No signing identity is bundled with the repository. Obtain a publicly trusted code-signing certificate or service that makes its key available through the Windows certificate store. The issuer must verify the publisher's identity; BilisPDF cannot issue that trust itself. A self-signed certificate is for development and will not make public downloads trusted.

Install the certificate securely into `Cert:\CurrentUser\My` using the issuer's instructions. Keep its private key on the issuer's hardware token, HSM, or approved key store. Do not put PFX passwords in scripts or command-line arguments. Configure only the non-secret certificate thumbprint and issuer-provided RFC 3161 timestamp endpoint:

```powershell
$env:BILISPDF_SIGNING_CERT_THUMBPRINT = '<40-character certificate thumbprint>'
$env:BILISPDF_TIMESTAMP_URL = '<issuer-provided RFC 3161 URL>'
./installer/build.ps1 -Release
```

The release build rejects missing, expired, self-signed, or untrusted signing certificates and certificates without the code-signing usage or an accessible private key. It sets the package Publisher to the certificate subject, signs BilisPDF executables and DLLs plus the sparse shell package before packaging, then signs the final MSI. Each signature must pass SignTool's Authenticode policy, match the configured publisher, and contain a timestamp. Build failure stops publication; no unsigned fallback is reported as signed. `SHA256SUMS.txt` is generated after the final MSI is signed.

When moving from the development publisher to the certificate issuer's verified subject, test package registration and upgrade on a clean machine and an existing development installation. A different publisher creates a different package family even though the package name is retained.

For now, build without `-Release` and without signing environment variables. Publish the MSI and `SHA256SUMS.txt` as an **unsigned preview**, with a plain explanation that Windows can show unknown-publisher/SmartScreen prompts. Signing does not promise immediate SmartScreen reputation: see [Microsoft's signing options](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options) and [SmartScreen guidance](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation).
