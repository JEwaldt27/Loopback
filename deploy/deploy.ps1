<#
.SYNOPSIS
  Publish Loopback (the Server host) and deploy it to your own server over SSH.

.DESCRIPTION
  Runs `dotnet publish Server`, copies the build to your server via scp, and
  copies the server-side deploy.sh alongside it. With -Deploy it also runs the
  server-side install over SSH (installs the build into the app folder and
  restarts the systemd service; you'll be prompted for sudo).

  If you don't pass -Server, the script uses $env:LOOPBACK_SERVER, or otherwise
  prompts for the server IP/hostname and SSH username. Your accounts file
  (users.json) lives only in the app folder on the server and is never part of
  the build, so deploys never overwrite your logins.

.EXAMPLE
  # Prompts for IP + username, then builds, copies, and installs
  ./deploy/deploy.ps1 -Deploy

.EXAMPLE
  # Pass the server explicitly
  ./deploy/deploy.ps1 -Server user@192.168.1.50 -Deploy

.EXAMPLE
  # Copy only; finish the install on the server yourself
  ./deploy/deploy.ps1 -Server user@192.168.1.50
#>
[CmdletBinding()]
param(
    # user@host of your server. If omitted, falls back to $env:LOOPBACK_SERVER,
    # otherwise you're prompted for the IP/hostname and username.
    [string]$Server = $env:LOOPBACK_SERVER,
    # Remote staging directory the build is copied to before install.
    [string]$RemoteStaging = "~/loopback-staging",
    # Also run the server-side deploy.sh over SSH after copying.
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $Server) {
    $ip   = Read-Host "Server IP or hostname"
    $user = Read-Host "SSH username"
    if (-not $ip -or -not $user) { throw "Both an IP/hostname and a username are required." }
    $Server = "$user@$ip"
}
Write-Host "Deploying to $Server" -ForegroundColor DarkGray

# Runs a NATIVE command (dotnet/ssh/scp) and fails the script on nonzero exit.
function Invoke-Step($desc, [scriptblock]$block) {
    Write-Host "==> $desc" -ForegroundColor Cyan
    & $block
    if ($LASTEXITCODE -ne 0) { throw "$desc failed (exit code $LASTEXITCODE)" }
}

Push-Location $repoRoot
try {
    Invoke-Step "Publishing Server (Release)" {
        dotnet publish Server -c Release -o publish
    }
    Invoke-Step "Clearing remote staging ($RemoteStaging)" {
        ssh $Server "rm -rf $RemoteStaging"
    }
    Invoke-Step "Copying build to server" {
        scp -r publish "${Server}:$RemoteStaging"
    }
    Invoke-Step "Copying deploy.sh to server (LF-normalized)" {
        # Send the shell script with Unix line endings so bash doesn't choke on
        # \r. We normalize into a temp file rather than trusting the on-disk EOLs.
        $sh = (Get-Content -Raw (Join-Path $PSScriptRoot 'deploy.sh')) -replace "`r`n", "`n"
        $tmp = Join-Path $env:TEMP "deploy-loopback.sh"
        [System.IO.File]::WriteAllText($tmp, $sh, (New-Object System.Text.UTF8Encoding($false)))
        scp $tmp "${Server}:~/deploy-loopback.sh"
    }

    if ($Deploy) {
        Write-Host "==> Installing on server (you'll be prompted for sudo)" -ForegroundColor Cyan
        ssh -t $Server "bash ~/deploy-loopback.sh"
        if ($LASTEXITCODE -ne 0) { throw "Remote deploy failed (exit code $LASTEXITCODE)" }
    }
    else {
        Write-Host ""
        Write-Host "Build copied. Finish on the server with:" -ForegroundColor Green
        Write-Host "  ssh $Server"
        Write-Host "  bash ~/deploy-loopback.sh"
        Write-Host "(or re-run with -Deploy to do this automatically)"
    }
    Write-Host "Done." -ForegroundColor Green
}
finally {
    Pop-Location
}
