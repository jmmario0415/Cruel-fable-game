# ============================================================
#  _lib.ps1 - 공용 함수 모음 (다른 스크립트에서 dot-source 로 불러 씀)
# ============================================================

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}

# ---------- 화면 출력 ----------
function Say    ([string]$m) { Write-Host $m }
function Step   ([string]$m) { Write-Host ""; Write-Host "▶ $m" -ForegroundColor Cyan }
function Ok     ([string]$m) { Write-Host "  ✔ $m" -ForegroundColor Green }
function Warn   ([string]$m) { Write-Host "  ! $m" -ForegroundColor Yellow }
function Fail   ([string]$m) { Write-Host ""; Write-Host "  ✘ $m" -ForegroundColor Red }

function Die ([string]$m) {
    Fail $m
    Write-Host ""
    Write-Host "중단했습니다. 위 메시지를 확인해 주세요." -ForegroundColor Red
    exit 1
}

# ---------- git 실행 헬퍼 ----------
# git 은 경고를 stderr 로 뱉기 때문에 $ErrorActionPreference='Stop' 과 궁합이 나쁘다.
# 아래 두 함수로만 git 을 호출한다.

function Git-Out {
    # 출력만 받아오고 실패해도 예외를 던지지 않음
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GitArgs)
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & git @GitArgs 2>&1 | Out-String
    $ErrorActionPreference = $old
    return $out.Trim()
}

function Git-Run {
    # 화면에 그대로 보여주며 실행. 실패하면 $false 반환
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GitArgs)
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & git @GitArgs 2>&1 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    return ($code -eq 0)
}

function Git-Ok {
    # 조용히 실행하고 성공 여부만 반환
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GitArgs)
    $null = Git-Out @GitArgs
    return ($LASTEXITCODE -eq 0)
}

# ---------- 환경 점검 ----------
function Assert-Git {
    $v = Git-Out --version
    if ($LASTEXITCODE -ne 0 -or $v -notmatch 'git version') {
        Die "Git 이 설치되어 있지 않습니다.`n     https://git-scm.com/download/win 에서 설치한 뒤 다시 실행해 주세요."
    }
    Ok "Git 확인 ($v)"
}

function Test-GitLfs {
    $null = Git-Out lfs version
    return ($LASTEXITCODE -eq 0)
}

# ---------- 저장소 루트 ----------
function Get-RepoRoot {
    # 이 스크립트는 <저장소>\tools\ 안에 있다
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Enter-Repo {
    $root = Get-RepoRoot
    Set-Location $root
    return $root
}

# ---------- 개인 설정 (tools\dev.config) ----------
$script:ConfigPath = Join-Path $PSScriptRoot 'dev.config'

function Read-Config {
    $cfg = @{}
    if (Test-Path $script:ConfigPath) {
        foreach ($line in Get-Content $script:ConfigPath -Encoding UTF8) {
            if ($line -match '^\s*#') { continue }
            if ($line -match '^\s*([A-Za-z_]+)\s*=\s*(.*?)\s*$') { $cfg[$Matches[1]] = $Matches[2] }
        }
    }
    return $cfg
}

function Write-Config ([hashtable]$cfg) {
    $lines = @('# 개인 설정입니다. Git 에 올라가지 않습니다 (.gitignore 처리됨).')
    foreach ($k in ($cfg.Keys | Sort-Object)) { $lines += "$k=$($cfg[$k])" }
    Set-Content -Path $script:ConfigPath -Value $lines -Encoding UTF8
}

function Get-Initials {
    # 브랜치 이름에 붙일 이니셜 (예: KNJ). 없으면 물어보고 저장한다.
    $cfg = Read-Config
    if ($cfg.INITIALS -and $cfg.INITIALS.Trim()) { return $cfg.INITIALS.Trim().ToUpper() }

    Write-Host ""
    Write-Host "브랜치 이름에 쓸 내 이니셜을 정해 주세요." -ForegroundColor Yellow
    Write-Host "  예) 김남주 → KNJ   (영문 2~4글자)"
    while ($true) {
        $v = (Read-Host "이니셜").Trim().ToUpper()
        if ($v -match '^[A-Z]{2,4}$') {
            $cfg.INITIALS = $v
            Write-Config $cfg
            Ok "이니셜 '$v' 저장 (tools\dev.config)"
            return $v
        }
        Warn "영문 2~4글자로 입력해 주세요."
    }
}

# ---------- 브랜치 이름 ----------
function Get-TodayBranch ([string]$initials) {
    return ('{0}_{1}' -f (Get-Date -Format 'yyyy_MM_dd'), $initials)
}

# ---------- 원격 주소 → 웹 주소 ----------
function Get-RepoWebUrl {
    $url = Git-Out remote get-url origin
    if ($LASTEXITCODE -ne 0 -or -not $url) { return $null }
    $url = $url.Trim()
    if ($url -match '^git@([^:]+):(.+?)(\.git)?$') { return "https://$($Matches[1])/$($Matches[2])" }
    if ($url -match '^https?://') { return ($url -replace '\.git$', '') }
    return $null
}

function Get-MainBranch {
    # 원격의 기본 브랜치 이름을 알아낸다 (main / master)
    $out = Git-Out symbolic-ref --short refs/remotes/origin/HEAD
    if ($LASTEXITCODE -eq 0 -and $out -match 'origin/(.+)$') { return $Matches[1].Trim() }
    if (Git-Ok show-ref --verify --quiet refs/remotes/origin/main)   { return 'main' }
    if (Git-Ok show-ref --verify --quiet refs/remotes/origin/master) { return 'master' }
    return 'main'
}

function Pause-End {
    Write-Host ""
    Write-Host "─────────────────────────────────────────────" -ForegroundColor DarkGray
}
