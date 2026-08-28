[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0.0',
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'artifacts/windows'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must resolve to a child of $artifactsRoot"
}
$work = Join-Path $output 'work'
$publish = Join-Path $work 'publish'
$package = Join-Path $work 'msix'
$assets = Join-Path $package 'Assets'

Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publish, $assets -ItemType Directory -Force | Out-Null

dotnet publish (Join-Path $root 'src/ScreenCatch.App/ScreenCatch.App.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    -p:DebugType=None `
    -p:DebugSymbols=false

$ffmpegUrl = 'https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.1-essentials_build.zip'
$ffmpegSha256 = 'fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9'
$ffmpegArchive = Join-Path $work 'ffmpeg.zip'
$ffmpegExtract = Join-Path $work 'ffmpeg'
Invoke-WebRequest -Uri $ffmpegUrl -OutFile $ffmpegArchive
$actualFfmpegSha256 = (Get-FileHash $ffmpegArchive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualFfmpegSha256 -ne $ffmpegSha256) {
    throw "FFmpeg archive checksum mismatch. Expected $ffmpegSha256, got $actualFfmpegSha256."
}
Expand-Archive $ffmpegArchive -DestinationPath $ffmpegExtract -Force
$tools = Join-Path $publish 'tools'
New-Item $tools -ItemType Directory -Force | Out-Null
foreach ($name in 'ffmpeg.exe', 'ffprobe.exe') {
    $binary = Get-ChildItem $ffmpegExtract -Filter $name -Recurse -File | Select-Object -First 1
    if (-not $binary) { throw "The FFmpeg archive did not contain $name." }
    Copy-Item $binary.FullName (Join-Path $tools $name)
}

$zipPath = Join-Path $output 'screencatch-win-x64.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath -CompressionLevel Optimal

$installRoot = Join-Path $package 'VFS/ProgramFilesX64/ScreenCatch'
New-Item $installRoot -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $publish '*') $installRoot -Recurse -Force

Add-Type -AssemblyName System.Drawing
function New-PackageLogo([string]$Path, [int]$Size) {
    $bitmap = [Drawing.Bitmap]::new($Size, $Size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(15, 82, 186))
        $fontSize = [Math]::Max(8, [int]($Size * 0.32))
        $font = [Drawing.Font]::new('Segoe UI', $fontSize, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
        $brush = [Drawing.Brushes]::White
        $format = [Drawing.StringFormat]::new()
        $format.Alignment = [Drawing.StringAlignment]::Center
        $format.LineAlignment = [Drawing.StringAlignment]::Center
        $graphics.DrawString('SC', $font, $brush, [Drawing.RectangleF]::new(0, 0, $Size, $Size), $format)
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
        $format.Dispose()
        $font.Dispose()
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
New-PackageLogo (Join-Path $assets 'Square44x44Logo.png') 44
New-PackageLogo (Join-Path $assets 'Square150x150Logo.png') 150
New-PackageLogo (Join-Path $assets 'StoreLogo.png') 50

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap rescap">
  <Identity Name="ScreenCatch" Publisher="CN=ScreenCatch" Version="$Version" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>ScreenCatch</DisplayName>
    <PublisherDisplayName>ScreenCatch</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources><Resource Language="en-us" /></Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Applications>
    <Application Id="ScreenCatch" Executable="VFS\ProgramFilesX64\ScreenCatch\ScreenCatch.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="ScreenCatch" Description="Privacy-first screen recorder and GIF studio"
          BackgroundColor="transparent" Square44x44Logo="Assets\Square44x44Logo.png"
          Square150x150Logo="Assets\Square150x150Logo.png" />
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
Set-Content -Path (Join-Path $package 'AppxManifest.xml') -Value $manifest -Encoding utf8

$makeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -File |
    Sort-Object FullName -Descending | Select-Object -First 1
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -File |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeAppx -or -not $signtool) { throw 'Windows SDK MakeAppx/SignTool was not found.' }

$msixPath = Join-Path $output 'screencatch-win-x64.msix'
& $makeAppx.FullName pack /d $package /p $msixPath /o

$certificate = New-SelfSignedCertificate -Type Custom -Subject 'CN=ScreenCatch' -KeyUsage DigitalSignature `
    -FriendlyName 'ScreenCatch CI package signing' -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
try {
    Export-Certificate -Cert $certificate -FilePath (Join-Path $output 'screencatch-dev-signing.cer') | Out-Null
    & $signtool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint $msixPath
}
finally {
    Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force
}

Remove-Item $work -Recurse -Force
Write-Host "Created $zipPath"
Write-Host "Created $msixPath"
