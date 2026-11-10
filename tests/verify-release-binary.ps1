# Smoke test for the Windows release binary. Same checks as verify-release-binary.sh:
# build an image from a public base image to a local folder (--to-file), then check the
# reported version, the manifest and that the added file is in one of the layers.
#
# usage: verify-release-binary.ps1 <path-to-nibbler.exe> [expected-version]
param(
    [Parameter(Mandatory = $true)][string]$Nibbler,
    [string]$Version = "",
    [string]$FromImage = "mcr.microsoft.com/dotnet/aspnet:11.0"
)
$ErrorActionPreference = "Stop"

function Fail($msg) { Write-Error "VERIFY FAILED: $msg"; exit 1 }

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("nibbler-verify-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path (Join-Path $work "app") | Out-Null
try {
    Set-Content -Path (Join-Path $work "app\hello.txt") -Value "nibbler release check"

    Write-Host "--- binary: $Nibbler"
    if (-not (Test-Path $Nibbler)) { Fail "$Nibbler is missing" }

    Write-Host "--- build $FromImage -> $work\out"
    $Nibbler = (Resolve-Path $Nibbler).Path
    # --add splits on ':', so use a relative source path (a drive letter would break it)
    Push-Location $work
    # nibbler logs to stderr; don't let PowerShell turn that into a terminating error
    $ErrorActionPreference = "Continue"
    $log = & $Nibbler --from-image $FromImage --to-file out `
        --add "app:/home/app" --temp-folder tmp -v 2>&1 | Out-String
    $exit = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    Pop-Location
    Write-Host $log
    if ($exit -ne 0) { Fail "nibbler exited with code $exit" }

    $versionLine = ($log -split "`r?`n" | Where-Object { $_ -like "Nibbler v*" } | Select-Object -First 1)
    if (-not $versionLine) { Fail "no version line in output" }
    if ($Version -and -not $versionLine.StartsWith("Nibbler v$Version+")) {
        Fail "expected version $Version, got: $versionLine"
    }
    if ($log -notmatch "Image digest: sha256:") { Fail "no image digest in output" }
    if (-not (Test-Path "$work\out\manifest.json")) { Fail "no manifest.json in output" }

    $found = $null
    $ErrorActionPreference = "Continue"
    foreach ($blob in Get-ChildItem "$work\out" -Filter "sha256_*") {
        $entries = & tar -tzf $blob.FullName 2>$null
        if ($LASTEXITCODE -eq 0 -and ($entries -match "home/app/hello.txt")) { $found = $blob.Name; break }
    }
    $ErrorActionPreference = "Stop"
    if (-not $found) { Fail "added file home/app/hello.txt not found in any layer" }

    Write-Host "--- OK: $versionLine, added layer $found"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
