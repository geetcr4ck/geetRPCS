
function Install-GeetRPCS {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateSet("minimal", "portable")]
        [string]$Version = "",

        [Parameter(Mandatory = $false)]
        [switch]$DesktopShortcut,

        [Parameter(Mandatory = $false)]
        [switch]$StartMenuShortcut,

        [Parameter(Mandatory = $false)]
        [switch]$Silent
    )

    $repo = "geetcr4ck/geetRPCS"
    $installDir = "$env:LOCALAPPDATA\geetRPCS"
    $exeName = "geetRPCS.exe"
    $versionFile = Join-Path $installDir ".version"

    # ══════════════════════════════════════════════════════════════
    # FILES TO PRESERVE DURING UPDATE
    # ══════════════════════════════════════════════════════════════
    $preserveFiles = @(
        "apps.json",
        "settings.json"
    )

    # ══════════════════════════════════════════════════════════════
    # HELPER FUNCTION: Show Menu
    # ══════════════════════════════════════════════════════════════
    function Show-Menu {
        param (
            [string]$Title,
            [string[]]$Options,
            [int]$Default = 0
        )

        while ($true) {
            Write-Host "`n$Title" -ForegroundColor Cyan
            for ($i = 0; $i -lt $Options.Count; $i++) {
                if ($i -eq $Default) {
                    Write-Host "  [$($i + 1)] $($Options[$i]) " -NoNewline
                    Write-Host "(default)" -ForegroundColor DarkGray
                }
                else {
                    Write-Host "  [$($i + 1)] $($Options[$i])"
                }
            }

            $selection = Read-Host "`nEnter choice [1-$($Options.Count)]"

            if ([string]::IsNullOrWhiteSpace($selection)) {
                return $Default
            }

            $index = 0
            if ([int]::TryParse($selection, [ref]$index)) {
                if ($index -ge 1 -and $index -le $Options.Count) {
                    return $index - 1
                }
            }

            Write-Host "Invalid choice. Please enter a number between 1 and $($Options.Count) or press Enter for default." -ForegroundColor Red
        }
    }

    function Show-YesNo {
        param (
            [string]$Question,
            [bool]$Default = $true
        )

        $defaultText = if ($Default) { "Y/n" } else { "y/N" }

        while ($true) {
            # DEBUG: Ensure new logic is active
            $response = Read-Host "$Question [$defaultText]"

            if ([string]::IsNullOrWhiteSpace($response)) {
                return $Default
            }

            $r = $response.ToLower().Trim()
            if ($r -eq 'y' -or $r -eq 'yes') {
                return $true
            }
            if ($r -eq 'n' -or $r -eq 'no') {
                return $false
            }

            Write-Host "Invalid input: '$response'. Please enter 'y' or 'n'." -ForegroundColor Red
        }
    }

    # ══════════════════════════════════════════════════════════════
    # HELPER FUNCTION: Backup Preserved Files
    # ══════════════════════════════════════════════════════════════
    function Backup-PreservedFiles {
        param (
            [string]$SourceDir,
            [string]$BackupDir,
            [string[]]$Files
        )

        $backedUp = @()

        if (-not (Test-Path $SourceDir)) {
            return $backedUp
        }

        if (-not (Test-Path $BackupDir)) {
            New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
        }

        foreach ($file in $Files) {
            $sourcePath = Join-Path $SourceDir $file
            if (Test-Path $sourcePath) {
                $destPath = Join-Path $BackupDir $file
                Copy-Item -Path $sourcePath -Destination $destPath -Force
                $backedUp += $file
                Write-Host "      ├─ 📦 Backed up: $file" -ForegroundColor DarkGray
            }
        }

        return $backedUp
    }

    # ══════════════════════════════════════════════════════════════
    # HELPER FUNCTION: Restore Preserved Files
    # ══════════════════════════════════════════════════════════════
    function Restore-PreservedFiles {
        param (
            [string]$BackupDir,
            [string]$DestDir,
            [string[]]$Files
        )

        $restored = @()

        if (-not (Test-Path $BackupDir)) {
            return $restored
        }

        foreach ($file in $Files) {
            $sourcePath = Join-Path $BackupDir $file
            if (Test-Path $sourcePath) {
                $destPath = Join-Path $DestDir $file
                Copy-Item -Path $sourcePath -Destination $destPath -Force
                $restored += $file
                Write-Host "      ├─ ✅ Restored: $file" -ForegroundColor DarkGray
            }
        }

        # Cleanup backup directory
        Remove-Item -Path $BackupDir -Recurse -Force -ErrorAction SilentlyContinue

        return $restored
    }

    # ══════════════════════════════════════════════════════════════
    # HEADER
    # ══════════════════════════════════════════════════════════════
    Clear-Host
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║                                               ║" -ForegroundColor Cyan
    Write-Host "  ║        geetRPCS Installer / Updater           ║" -ForegroundColor Cyan
    Write-Host "  ║                                               ║" -ForegroundColor Cyan
    Write-Host "  ╚═══════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    # Backup directory for preserved files
    $backupDir = Join-Path $env:TEMP "geetRPCS_backup_$(Get-Random)"

    try {
        # ══════════════════════════════════════════════════════════════
        # CHECK EXISTING SHORTCUT PREFERENCES
        # ══════════════════════════════════════════════════════════════
        $settingsPath = Join-Path $installDir "settings.json"
        $savedPreferences = $null
        $hasExistingPreferences = $false

        if (Test-Path $settingsPath) {
            try {
                $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
                if ($settings.shortcutPreferences -and $settings.shortcutPreferences.preferenceSaved -eq $true) {
                    $savedPreferences = $settings.shortcutPreferences
                    $hasExistingPreferences = $true
                    Write-Host "Found saved shortcut preferences" -ForegroundColor DarkGray
                }
            }
            catch {
                # Ignore errors, will use interactive prompts
            }
        }

        # ══════════════════════════════════════════════════════════════
        # INTERACTIVE MENU (if not Silent mode)
        # ══════════════════════════════════════════════════════════════
        if (-not $Silent) {
            if ([string]::IsNullOrWhiteSpace($Version)) {
                $versionOptions = @(
                    "Portable (Recommended) - Standalone, no dependencies",
                    "Minimal - Smaller size, requires .NET 8.0 Runtime"
                )
                $versionChoice = Show-Menu -Title "Select Version:" -Options $versionOptions -Default 0
                $Version = if ($versionChoice -eq 0) { "portable" } else { "minimal" }
            }

            # Use saved preferences if available, otherwise ask
            if ($hasExistingPreferences) {
                $DesktopShortcut = $savedPreferences.desktopShortcut
                $StartMenuShortcut = $savedPreferences.startMenuShortcut
                Write-Host "  Using saved preferences: Desktop=$DesktopShortcut, StartMenu=$StartMenuShortcut" -ForegroundColor DarkCyan
            }
            else {
                if (-not $PSBoundParameters.ContainsKey('DesktopShortcut')) {
                    $DesktopShortcut = Show-YesNo -Question "Create Desktop shortcut?" -Default $true
                }

                if (-not $PSBoundParameters.ContainsKey('StartMenuShortcut')) {
                    $StartMenuShortcut = Show-YesNo -Question "Create Start Menu shortcut?" -Default $true
                }
            }

            Write-Host ""
        }
        else {
            if ([string]::IsNullOrWhiteSpace($Version)) {
                $Version = "portable"
            }

            # Use saved preferences in silent mode if available
            if ($hasExistingPreferences) {
                $DesktopShortcut = $savedPreferences.desktopShortcut
                $StartMenuShortcut = $savedPreferences.startMenuShortcut
            }
        }

        # ══════════════════════════════════════════════════════════════
        # [1/7] CHECK VERSION
        # ══════════════════════════════════════════════════════════════
        Write-Host "[1/7] Checking latest version from GitHub..." -ForegroundColor Yellow
        $releaseInfo = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -ErrorAction Stop
        $latestTag = $releaseInfo.tag_name

        $installedVersion = $null
        $isUpdate = $false

        if (Test-Path $versionFile) {
            $versionData = Get-Content $versionFile -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
            if ($versionData) {
                $installedVersion = $versionData.version
                $installedType = $versionData.type

                if ($installedVersion -eq $latestTag -and $installedType -eq $Version) {
                    Write-Host "`n✅ Already up to date! ($installedVersion - $installedType)" -ForegroundColor Green

                    $exePath = Join-Path $installDir $exeName
                    if (Test-Path $exePath) {
                        Write-Host "`nOpening installation folder..." -ForegroundColor Gray
                        explorer.exe $installDir
                    }
                    return
                }

                $isUpdate = $true
                Write-Host "      Installed: $installedVersion ($installedType)" -ForegroundColor DarkGray
                Write-Host "      Available: $latestTag ($Version)" -ForegroundColor DarkGray
            }
        }

        $asset = $releaseInfo.assets | Where-Object { $_.name -like "*$Version.zip" } | Select-Object -First 1
        if (-not $asset) { throw "Could not find the $Version version for release $latestTag" }

        $downloadUrl = $asset.browser_download_url
        $totalSize = $asset.size
        $tempPath = Join-Path $env:TEMP $asset.name
        $tempExtractPath = Join-Path $env:TEMP "geetRPCS_extract_$(Get-Random)"

        # ══════════════════════════════════════════════════════════════
        # [2/7] CLOSE RUNNING INSTANCE
        # ══════════════════════════════════════════════════════════════
        $process = Get-Process | Where-Object { $_.ProcessName -eq "geetRPCS" }
        if ($process) {
            Write-Host "[2/7] geetRPCS is running. Closing..." -ForegroundColor Yellow
            Stop-Process -Name "geetRPCS" -Force
            Start-Sleep -Seconds 2
        }
        else {
            Write-Host "[2/7] No running instance found" -ForegroundColor DarkGray
        }

        # ══════════════════════════════════════════════════════════════
        # [3/7] BACKUP USER FILES (NEW STEP)
        # ══════════════════════════════════════════════════════════════
        $backedUpFiles = @()
        if ($isUpdate -and (Test-Path $installDir)) {
            Write-Host "[3/7] Backing up user configuration files..." -ForegroundColor Yellow
            $backedUpFiles = Backup-PreservedFiles -SourceDir $installDir -BackupDir $backupDir -Files $preserveFiles

            if ($backedUpFiles.Count -eq 0) {
                Write-Host "      └─ No user files to backup" -ForegroundColor DarkGray
            }
            else {
                Write-Host "      └─ Backed up $($backedUpFiles.Count) file(s)" -ForegroundColor DarkGray
            }
        }
        else {
            Write-Host "[3/7] Fresh installation - no backup needed" -ForegroundColor DarkGray
        }

        # ══════════════════════════════════════════════════════════════
        # [4/7] DOWNLOAD
        # ══════════════════════════════════════════════════════════════
        if ($isUpdate) {
            Write-Host "[4/7] Downloading update ($installedVersion → $latestTag)..." -ForegroundColor Green
        }
        else {
            Write-Host "[4/7] Downloading $Version version ($latestTag)..." -ForegroundColor Green
        }

        $webClient = New-Object System.Net.WebClient
        $sourceUri = New-Object System.Uri($downloadUrl)

        $startTime = Get-Date
        $webClient.DownloadFileAsync($sourceUri, $tempPath)

        while ($webClient.IsBusy) {
            $stats = Get-Item $tempPath -ErrorAction SilentlyContinue
            if ($stats) {
                $currentSize = $stats.Length

                if ($totalSize -gt 0) { $percent = [Math]::Round(($currentSize / $totalSize) * 100) } else { $percent = 0 }

                $timeElapsed = (Get-Date) - $startTime
                $secondsElapsed = $timeElapsed.TotalSeconds
                $speedBytesPerSec = if ($secondsElapsed -gt 0) { $currentSize / $secondsElapsed } else { 0 }

                $speedMBps = "{0:N2} MB/s" -f ($speedBytesPerSec / 1MB)
                $remainingBytes = $totalSize - $currentSize
                $secondsRemaining = if ($speedBytesPerSec -gt 0) { $remainingBytes / $speedBytesPerSec } else { 0 }
                $etaStr = "{0:mm}:{0:ss}" -f (New-TimeSpan -Seconds $secondsRemaining)

                $currentMB = "{0:N2}" -f ($currentSize / 1MB)
                $totalMB = "{0:N2}" -f ($totalSize / 1MB)

                $barWidth = 20
                $filled = [Math]::Floor(($percent / 100) * $barWidth)
                if ($filled -gt $barWidth) { $filled = $barWidth }
                $progressBar = "[" + ("█" * $filled) + ("░" * ($barWidth - $filled)) + "]"

                $msg = "`r      $progressBar $currentMB/$totalMB MB ($percent%) @ $speedMBps | ETA: $etaStr    "
                Write-Host -NoNewline $msg -ForegroundColor White
            }
            Start-Sleep -Milliseconds 200
        }
        Write-Host "`n      Download complete!" -ForegroundColor Green

        # ══════════════════════════════════════════════════════════════
        # [5/7] EXTRACT FILES
        # ══════════════════════════════════════════════════════════════
        Write-Host "[5/7] Extracting files..." -ForegroundColor Yellow

        New-Item -ItemType Directory -Path $tempExtractPath -Force | Out-Null
        Expand-Archive -Path $tempPath -DestinationPath $tempExtractPath -Force
        Remove-Item $tempPath -Force

        $extractedContent = Get-ChildItem -Path $tempExtractPath
        $sourceDir = $tempExtractPath

        if ($extractedContent.Count -eq 1 -and $extractedContent[0].PSIsContainer) {
            $sourceDir = $extractedContent[0].FullName
            Write-Host "      └─ Found: $($extractedContent[0].Name)" -ForegroundColor DarkGray
        }

        # ══════════════════════════════════════════════════════════════
        # [6/7] CLEAN & INSTALL
        # ══════════════════════════════════════════════════════════════
        if (Test-Path $installDir) {
            Write-Host "[6/7] Removing old installation..." -ForegroundColor Yellow
            if ($backedUpFiles.Count -gt 0) {
                Write-Host "      ℹ️  User config files will be restored after installation" -ForegroundColor DarkCyan
            }
            Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        else {
            Write-Host "[6/7] Creating installation directory..." -ForegroundColor Yellow
        }

        New-Item -ItemType Directory -Path $installDir -Force | Out-Null

        Write-Host "      └─ Installing to: $installDir" -ForegroundColor DarkGray
        Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force

        Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue

        Get-ChildItem -Path $installDir -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

        $exePath = Join-Path $installDir $exeName
        if (-not (Test-Path $exePath)) {
            throw "File $exeName not found after extraction!"
        }

        # ══════════════════════════════════════════════════════════════
        # RESTORE USER FILES
        # ══════════════════════════════════════════════════════════════
        if ($backedUpFiles.Count -gt 0) {
            Write-Host "      Restoring user configuration files..." -ForegroundColor Yellow
            $restoredFiles = Restore-PreservedFiles -BackupDir $backupDir -DestDir $installDir -Files $backedUpFiles
            Write-Host "      └─ Restored $($restoredFiles.Count) file(s)" -ForegroundColor DarkGray
        }

        # ══════════════════════════════════════════════════════════════
        # SAVE VERSION INFO
        # ══════════════════════════════════════════════════════════════
        $versionData = @{
            version     = $latestTag
            type        = $Version
            installedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        }
        $versionData | ConvertTo-Json | Set-Content -Path $versionFile -Force

        # ══════════════════════════════════════════════════════════════
        # SAVE SHORTCUT PREFERENCES TO SETTINGS.JSON
        # ══════════════════════════════════════════════════════════════
        $settingsPath = Join-Path $installDir "settings.json"
        
        # Load existing settings or create new
        if (Test-Path $settingsPath) {
            try {
                $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
                if (-not $settings) {
                    $settings = @{}
                }
            }
            catch {
                $settings = @{}
            }
        }
        else {
            $settings = @{
                language               = "en"
                disabledApps           = @()
                mouseEnergyEnabled     = $true
                trayAnimationEnabled   = $true
                telemetryEnabled       = $true
                updateNotificationMode = "Notification"
                logLevel               = "INFO"
                autoUpdateEnabled      = $false
            }
        }

        # Update shortcut preferences (only if not using saved preferences)
        if (-not $hasExistingPreferences) {
            $settings | Add-Member -MemberType NoteProperty -Name "shortcutPreferences" -Value @{
                desktopShortcut   = [bool]$DesktopShortcut
                startMenuShortcut = [bool]$StartMenuShortcut
                preferenceSaved   = $true
            } -Force
            
            Write-Host "      Saving shortcut preferences..." -ForegroundColor Yellow
            $settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Force
            Write-Host "      └─ Preferences saved to settings.json" -ForegroundColor DarkGray
        }

        # ══════════════════════════════════════════════════════════════
        # [7/7] CREATE SHORTCUTS
        # ══════════════════════════════════════════════════════════════
        Write-Host "[7/7] Creating shortcuts..." -ForegroundColor Yellow

        $shortcutsCreated = @()

        if ($DesktopShortcut) {
            $desktopPath = [Environment]::GetFolderPath("Desktop")
            $shortcutPath = Join-Path $desktopPath "geetRPCS.lnk"

            if (Test-Path $shortcutPath) { Remove-Item $shortcutPath -Force }

            $WshShell = New-Object -ComObject WScript.Shell
            $Shortcut = $WshShell.CreateShortcut($shortcutPath)
            $Shortcut.TargetPath = $exePath
            $Shortcut.WorkingDirectory = $installDir
            $Shortcut.IconLocation = "$exePath,0"
            $Shortcut.Description = "geetRPCS - Discord Rich Presence Custom Switcher"
            $Shortcut.Save()

            $shortcutsCreated += "Desktop"
            Write-Host "      ├─ ✅ Desktop shortcut" -ForegroundColor DarkGray
        }

        if ($StartMenuShortcut) {
            $startMenuPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
            $startMenuFolder = Join-Path $startMenuPath "geetRPCS"

            if (-not (Test-Path $startMenuFolder)) {
                New-Item -ItemType Directory -Path $startMenuFolder -Force | Out-Null
            }

            $shortcutPath = Join-Path $startMenuFolder "geetRPCS.lnk"

            if (Test-Path $shortcutPath) { Remove-Item $shortcutPath -Force }

            $WshShell = New-Object -ComObject WScript.Shell
            $Shortcut = $WshShell.CreateShortcut($shortcutPath)
            $Shortcut.TargetPath = $exePath
            $Shortcut.WorkingDirectory = $installDir
            $Shortcut.IconLocation = "$exePath,0"
            $Shortcut.Description = "geetRPCS - Discord Rich Presence Custom Switcher"
            $Shortcut.Save()

            $uninstallShortcutPath = Join-Path $startMenuFolder "Uninstall geetRPCS.lnk"
            $UninstallShortcut = $WshShell.CreateShortcut($uninstallShortcutPath)
            $UninstallShortcut.TargetPath = "powershell.exe"
            $UninstallShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command `"Remove-Item -Path '$installDir' -Recurse -Force; Remove-Item -Path '$startMenuFolder' -Recurse -Force; Remove-Item -Path '$([Environment]::GetFolderPath('Desktop'))\geetRPCS.lnk' -Force -ErrorAction SilentlyContinue; Write-Host 'geetRPCS has been uninstalled.' -ForegroundColor Green; Start-Sleep -Seconds 2`""
            $UninstallShortcut.IconLocation = "shell32.dll,31"
            $UninstallShortcut.Description = "Uninstall geetRPCS"
            $UninstallShortcut.Save()

            $shortcutsCreated += "Start Menu"
            Write-Host "      ├─ ✅ Start Menu shortcut" -ForegroundColor DarkGray
            Write-Host "      ├─ ✅ Uninstall shortcut" -ForegroundColor DarkGray
        }

        if ($shortcutsCreated.Count -eq 0) {
            Write-Host "      └─ No shortcuts created" -ForegroundColor DarkGray
        }
        else {
            Write-Host "      └─ Created: $($shortcutsCreated -join ', ')" -ForegroundColor DarkGray
        }

        if (Get-Command "ie4uinit.exe" -ErrorAction SilentlyContinue) {
            & "ie4uinit.exe" -show 2>$null
        }

        # ══════════════════════════════════════════════════════════════
        # DONE
        # ══════════════════════════════════════════════════════════════
        Write-Host ""
        Write-Host "  ╔═══════════════════════════════════════════════╗" -ForegroundColor Green
        Write-Host "  ║                                               ║" -ForegroundColor Green
        if ($isUpdate) {
            Write-Host "  ║       Update completed successfully!          ║" -ForegroundColor Green
            Write-Host "  ║                                               ║" -ForegroundColor Green
            Write-Host "  ╚═══════════════════════════════════════════════╝" -ForegroundColor Green
            Write-Host ""
            Write-Host "  $installedVersion -> $latestTag ($Version)" -ForegroundColor White
        }
        else {
            Write-Host "  ║     Installation completed successfully!      ║" -ForegroundColor Green
            Write-Host "  ║                                               ║" -ForegroundColor Green
            Write-Host "  ╚═══════════════════════════════════════════════╝" -ForegroundColor Green
            Write-Host ""
            Write-Host "  Version: $latestTag ($Version)" -ForegroundColor White
        }
        Write-Host ""
        Write-Host "  Location: $installDir" -ForegroundColor Cyan

        # ══════════════════════════════════════════════════════════════
        # PRESERVED FILES INFO
        # ══════════════════════════════════════════════════════════════
        if ($isUpdate -and $backedUpFiles.Count -gt 0) {
            Write-Host ""
            Write-Host "  ╔═══════════════════════════════════════════════╗" -ForegroundColor Cyan
            Write-Host "  ║                                               ║" -ForegroundColor Cyan
            Write-Host "  ║        Your settings have been kept!          ║" -ForegroundColor Cyan
            Write-Host "  ║                                               ║" -ForegroundColor Cyan
            Write-Host "  ╚═══════════════════════════════════════════════╝" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "  Preserved files:" -ForegroundColor White
            foreach ($file in $backedUpFiles) {
                Write-Host "    + $file" -ForegroundColor Green
            }
        }

        Write-Host ""
        Write-Host "  💡 Tip: Enable Auto-Update from the tray menu!" -ForegroundColor Yellow
        Write-Host "     Right-click tray icon → Settings → Auto-Update" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "Opening installation folder..." -ForegroundColor Gray
        explorer.exe $installDir

    }
    catch {
        Write-Host "`n❌ Installation failed: $($_.Exception.Message)" -ForegroundColor Red

        # Cleanup on error
        if ($tempPath -and (Test-Path $tempPath)) {
            Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
        }
        if ($tempExtractPath -and (Test-Path $tempExtractPath)) {
            Remove-Item $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue
        }
        if ($backupDir -and (Test-Path $backupDir)) {
            Remove-Item $backupDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Install-GeetRPCS