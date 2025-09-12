<#
.SYNOPSIS
    Deploys the .NET publish output to a remote Linux host via SSH/SCP.
.DESCRIPTION
    This script performs the following actions:
    1. Ensures the Posh-SSH module is installed.
    2. Connects to the remote host using provided credentials.
    3. Cleans the remote target directory (creates it if it doesn't exist).
    4. Uploads all files from the local publish directory to the remote target.
    5. Disconnects the SSH session.
#>

# ===================================================================
#               SCRIPT CONFIGURATION - 请修改以下变量
# ===================================================================

# --- 远程主机设置 ---
$remoteHost = "Echo.local"       # 你的开发板的 IP 地址或主机名
$remoteUser = "ReEd"                 # SSH 登录用户名
$remotePassword = "echo13234"    # SSH 登录密码 (!!! 不安全，仅用于个人开发环境 !!!)

# --- 目录设置 ---
$remoteDir = "/home/ReEd/Documents/Projects/CourseDesign/MarineLitterMonitor"  # 文件要上传到的远程服务器上的绝对路径
$localPublishDir = "MarineLitterMonitorServer/bin/Release/net9.0/publish/" # 本地 publish 文件夹的相对路径
                                                 # (请根据你的项目配置修改, 比如 net7.0, Debug/Release等)

# ===================================================================
#                         SCRIPT LOGIC - 以下部分无需修改
# ===================================================================

# --- 1. 检查并安装 Posh-SSH 模块 ---
Write-Host "Checking for Posh-SSH module..." -ForegroundColor Cyan
if (-not (Get-Module -ListAvailable -Name Posh-SSH)) {
    Write-Host "Posh-SSH module not found. Attempting to install..." -ForegroundColor Yellow
    try {
        Install-Module Posh-SSH -Scope CurrentUser -Force -SkipPublisherCheck -Confirm:$false -AllowClobber
        Write-Host "Posh-SSH installed successfully." -ForegroundColor Green
    } catch {
        Write-Host "Failed to install Posh-SSH. Please install it manually by running: 'Install-Module Posh-SSH -Scope CurrentUser -Force -AllowClobber'" -ForegroundColor Red
        exit 1
    }
}
Import-Module Posh-SSH -Force

# --- 2. 检查本地 publish 目录是否存在 ---
if (-not (Test-Path $localPublishDir)) {
    Write-Host "Local publish directory not found at: '$localPublishDir'" -ForegroundColor Red
    Write-Host "Please make sure you have published your project first." -ForegroundColor Yellow
    exit 1
}

$sshSession = $null
$sftpSession = $null
try {
    # --- 3. 创建凭据对象 ---
    Write-Host "Creating credential object..." -ForegroundColor Cyan
    $securePassword = ConvertTo-SecureString -String $remotePassword -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential($remoteUser, $securePassword)

    # --- 4. 建立持久 SSH 会话 (用于 shell 命令) ---
    Write-Host "Connecting to $remoteHost for shell commands..." -ForegroundColor Cyan
    $sshSession = New-SSHSession -ComputerName $remoteHost -Credential $credential -AcceptKey
    if (-not $sshSession) { throw "Failed to create SSH session." }
    Write-Host "SSH session established." -ForegroundColor Green

    # --- 5. 清空远程目录 ---
    $cleanupCommand = "mkdir -p '$remoteDir' && rm -rf '$remoteDir'/*"
    Write-Host "Cleaning remote directory: $remoteDir" -ForegroundColor Cyan
    Invoke-SSHCommand -SSHSession $sshSession -Command $cleanupCommand
    Write-Host "Remote directory cleaned." -ForegroundColor Green

    # --- 6. 建立持久 SFTP 会话 (用于文件传输) ---
    Write-Host "Establishing SFTP session for file transfers..." -ForegroundColor Cyan
    # 创建 SFTP 会话
    $sftpSession = New-SFTPSession -ComputerName $remoteHost -Credential $credential -AcceptKey
    if (-not $sftpSession) { throw "Failed to create SFTP session." }
    Write-Host "SFTP session established. Starting file upload..." -ForegroundColor Green

    # --- 7. 遍历并高效上传所有文件 ---
    $itemsToUpload = Get-ChildItem -Path $localPublishDir
    foreach ($item in $itemsToUpload) {
        Write-Host "  -> Uploading $($item.Name)..." -ForegroundColor Gray

		# 构造完整的远程文件路径
		$remoteFilePath = "$remoteDir/$($item.Name)"

		try {
			# 读取本地文件的所有字节内容
			$fileBytes = [System.IO.File]::ReadAllBytes($item.FullName)

			Set-SFTPContent -SFTPSession $sftpSession -Path $remoteFilePath -Value $fileBytes
		}
		catch {
			Write-Warning "Failed to upload $($item.Name): $($_.ToString())"
		}
    }

    Write-Host "----------------------------------------"
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host "----------------------------------------"

} catch {
    Write-Host "An error occurred during deployment:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally {
    # --- 8. 断开所有连接 ---
    if ($sftpSession) {
        Write-Host "Disconnecting SFTP session..." -ForegroundColor Cyan
        Remove-SFTPSession -SFTPSession $sftpSession
    }
    if ($sshSession) {
        Write-Host "Disconnecting SSH session..." -ForegroundColor Cyan
        Remove-SSHSession -SSHSession $sshSession
    }
}