# ExportProject.ps1
# Snapshot 1:1 całego projektu do jednego TXT w UTF-8 z BOM (Notepad-friendly).
# Wyklucza .git/.vs/bin/obj/node_modules/packages/Debug/Release oraz poprzednie snapshoty i pliki ExportProject.*.
# Kod skryptu to czysty ASCII (żeby parser PS nigdy się nie potknął na ogonkach).
Param(
    [string]$Root = ".",
    [string]$OutputFile = "ProjektSnapshot_utf8.txt",
    [switch]$NoTree
)
$ErrorActionPreference = 'Stop'

# (Opcjonalnie) wsparcie CP-1250 przy odczycie w PS7; w PS5.1 to no-op.
function Ensure-Cp1250Support {
    try { $null = [System.Text.Encoding]::GetEncoding(1250); return } catch {}
    try {
        $providerType = [Type]::GetType('System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages')
        if ($providerType) {
            [System.Text.Encoding]::RegisterProvider($providerType::Instance)
            $null = [System.Text.Encoding]::GetEncoding(1250) | Out-Null
        }
    } catch {}
}
Ensure-Cp1250Support

# Napisy PL bez nie-ASCII w źródle (składane z kodów Unicode)
function PL-Uzytkownik { "U" + [char]0x017C + "ytkownik" }          # U + ż + ytkownik
function PL-GalazGit   { "Ga" + [char]0x0142 + [char]0x0105 + [char]0x017A + " Git" }  # Ga + ł + ą + ź Git
function PL-Kodowanie  { "Kodowanie snapshotu" }
function PL-Maszyna    { "Maszyna" }
function PL-Katalog    { "Katalog" }
function PL-Data       { "Data" }
function PL-Tree       { "TREE (foldery)" }
function PL-Files      { "FILES (liczba)" }

# Ścieżki
try { $Root = (Resolve-Path -LiteralPath $Root).Path } catch { throw "Nie moge rozwiazac sciezki Root: $Root. $_" }
$ResolvedOut = Resolve-Path -LiteralPath $OutputFile -ErrorAction SilentlyContinue
if ($null -ne $ResolvedOut) { $OutputFile = $ResolvedOut.Path }
$rootLen = $Root.Length
function RelPath([string]$full) { $full.Substring($rootLen).TrimStart('\','/') }

# Filtry
$IgnoreDirs = @('.git', '.vs', 'bin', 'obj', 'packages', 'node_modules', 'Debug', 'Release')
$IncludeExt = @(
  '.cs','.xaml','.xaml.cs','.csproj','.sln','.config','.json','.yml','.yaml',
  '.txt','.md','.ps1','.cmd','.bat','.props','.targets','.resx','.xml'
)
$ExcludeExt = @(
  '.dll','.exe','.pdb','.png','.jpg','.jpeg','.gif','.bmp','.ico',
  '.zip','.7z','.rar','.nupkg','.db','.sqlite','.pdf','.mp3','.mp4','.avi','.mov'
)
function Should-IgnoreDir([string]$full) {
  $p = $full.Substring($Root.Length).TrimStart('\','/')
  foreach ($seg in ($p -split '[\\/]+')) {
    if ([string]::IsNullOrWhiteSpace($seg)) { continue }
    if ($IgnoreDirs -contains $seg) { return $true }
  }
  return $false
}
function Should-IgnoreFile([System.IO.FileInfo]$f, [string]$outFile) {
  if ($outFile -and ($f.FullName -ieq $outFile)) { return $true } # bieżący snapshot
  $name = $f.Name.ToLowerInvariant()
  if ($name -like "*snapshot*.txt")  { return $true }
  if ($name -like "exportproject.ps1") { return $true }
  if ($name -like "exportproject.bat") { return $true }
  if ($name -like "*.lastlog.txt")    { return $true }
  return $false
}

# Odczyt 1:1: bajty -> (BOM → UTF; inaczej: UTF-8 strict -> CP-1250 -> Default)
function Read-TextExact([string]$path) {
  $bytes = [System.IO.File]::ReadAllBytes($path)

  # UTF-8 BOM
  if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
  }
  # UTF-32 LE BOM (FF FE 00 00)
  if ($bytes.Length -ge 4 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x00) {
    return [System.Text.Encoding]::UTF32.GetString($bytes, 4, $bytes.Length - 4)
  }
  # UTF-32 BE BOM (00 00 FE FF)
  if ($bytes.Length -ge 4 -and $bytes[0] -eq 0x00 -and $bytes[1] -eq 0x00 -and $bytes[2] -eq 0xFE -and $bytes[3] -eq 0xFF) {
    $utf32be = New-Object System.Text.UTF32Encoding($true,$true)
    return $utf32be.GetString($bytes, 4, $bytes.Length - 4)
  }
  # UTF-16 LE BOM (FF FE)
  if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
    return [System.Text.Encoding]::Unicode.GetString($bytes)
  }
  # UTF-16 BE BOM (FE FF)
  if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
    return [System.Text.Encoding]::BigEndianUnicode.GetString($bytes)
  }

  # Brak BOM -> najpierw ścisły UTF-8…
  try {
    $utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
    return $utf8Strict.GetString($bytes)
  } catch {
    # …jeśli nie pasuje, spróbuj CP-1250; jeśli niedostępne, użyj Default
    try { return [System.Text.Encoding]::GetEncoding(1250).GetString($bytes) }
    catch { return [System.Text.Encoding]::Default.GetString($bytes) }
  }
}

# Zbierz pliki (czysty pipeline)
$files = Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { -not (Should-IgnoreDir $_.FullName) } |
  Where-Object { -not (Should-IgnoreFile $_ $OutputFile) } |
  Where-Object {
    $ext = $_.Extension.ToLowerInvariant()
    -not ($ExcludeExt -contains $ext) -and
    ( ($IncludeExt -contains $ext) -or $_.Name.ToLowerInvariant().EndsWith(".xaml.cs") )
  } |
  Sort-Object FullName

# Writer: UTF-8 z BOM (Notepad-friendly) + CRLF
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)  # $true => BOM
$sw = New-Object System.IO.StreamWriter($OutputFile, $false, $utf8WithBom)
$sw.NewLine = "`r`n"

try {
  function WL([string]$s="") { $sw.WriteLine($s) }

  # Git (opcjonalnie)
  function Try-Git([string]$Args) {
    try {
      $psi = New-Object System.Diagnostics.ProcessStartInfo
      $psi.FileName = 'git'
      $psi.Arguments = $Args
      $psi.WorkingDirectory = $Root
      $psi.UseShellExecute = $false
      $psi.RedirectStandardOutput = $true
      $psi.RedirectStandardError  = $true
      $p = [System.Diagnostics.Process]::Start($psi)
      $p.WaitForExit()
      if ($p.ExitCode -ne 0) { return '' }
      return ($p.StandardOutput.ReadToEnd()).Trim()
    } catch { return '' }
  }
  $gitBranch = if (Test-Path (Join-Path $Root ".git")) { Try-Git 'rev-parse --abbrev-ref HEAD' } else { '' }
  $gitLast   = if (Test-Path (Join-Path $Root ".git")) { Try-Git 'log -1 --pretty=format:%h %ci %s' } else { '' }

  # Nagłówek (PL)
  WL "# SNAPSHOT"
  WL ("# {0}:       {1}" -f (PL-Data), (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
  WL ("# {0}:    {1}" -f (PL-Katalog), $Root)
  WL ("# {0}:    {1}" -f (PL-Maszyna), $env:COMPUTERNAME)
  WL ("# {0}: {1}" -f (PL-Uzytkownik), $env:USERNAME)
  if ($gitBranch) { WL ("# {0}:  {1}" -f (PL-GalazGit), $gitBranch) }
  if ($gitLast)   { WL ("# {0}: {1}" -f "Ostatni commit", $gitLast) }
  WL ("# {0}: UTF-8 (z BOM)" -f (PL-Kodowanie))
  WL ""

  # TREE + FILES
  if (-not $NoTree) {
    WL ("## {0}" -f (PL-Tree))
    $dirs = $files |
      ForEach-Object { Split-Path -Parent (RelPath $_.FullName) } |
      Where-Object { $_ } |
      Sort-Object -Unique
    foreach ($d in $dirs) { WL ("DIR  {0}" -f $d) }
    WL ""
    WL ("## {0}: {1}" -f (PL-Files), $files.Count)
    foreach ($f in $files) { WL ("FILE {0}" -f (RelPath $f.FullName)) }
    WL ""
  }

  # Zawartość plików 1:1
  foreach ($f in $files) {
    $rel = RelPath $f.FullName
    WL ("===== FILE: {0} ({1} bytes)" -f $rel, $f.Length)
    WL "----- BEGIN -----"
    try {
      $txt = Read-TextExact $f.FullName
      $sw.Write($txt)     # bez jakichkolwiek modyfikacji
    } catch {
      WL ("[BLAD CZYTANIA: {0}]" -f $_.Exception.Message)
    }
    WL ""
    WL "----- END -----"
    WL ""
  }
}
finally {
  $sw.Flush(); $sw.Dispose()
}

Write-Host "[OK] Zapisano snapshot: $OutputFile"
