# ============================================================
#  start-day.ps1 - 오늘 작업 브랜치를 만들고 그 위로 이동한다.
#  브랜치 이름 형식: 2026_09_05_KNJ
#  실행: start_day.bat 더블클릭
# ============================================================

. (Join-Path $PSScriptRoot '_lib.ps1')

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  오늘 작업 시작  ($(Get-Date -Format 'yyyy년 M월 d일 dddd'))" -ForegroundColor Magenta
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta

$root = Enter-Repo
Assert-Git

if (-not (Test-Path (Join-Path $root '.git'))) {
    Die "아직 Git 저장소가 아닙니다. 먼저 setup.bat 을 실행해 주세요."
}

$initials = Get-Initials
$branch   = Get-TodayBranch $initials

# ---------------------------------------------------------------
Step "원격 최신 정보 가져오기"
if (-not (Git-Run fetch origin --prune)) {
    Warn "GitHub 에 접속하지 못했습니다. 오프라인 상태로 계속합니다."
    $offline = $true
} else {
    Ok "fetch 완료"
    $offline = $false
}

$mainBranch = Get-MainBranch
$current    = (Git-Out rev-parse --abbrev-ref HEAD).Trim()

# ---------------------------------------------------------------
# 이미 오늘 브랜치라면 최신화만 하고 끝낸다
if ($current -eq $branch) {
    Ok "이미 오늘 브랜치($branch)에 있습니다"
    if (-not $offline) {
        if (Git-Ok show-ref --verify --quiet "refs/remotes/origin/$branch") {
            Step "내 브랜치 최신화"
            $null = Git-Run pull --ff-only origin $branch
        }
    }
    Step "현재 상태"
    Git-Out status --short | ForEach-Object { Write-Host "    $_" }
    Write-Host ""
    Write-Host "  그대로 이어서 작업하시면 됩니다." -ForegroundColor Green
    Pause-End
    exit 0
}

# ---------------------------------------------------------------
Step "작업 중이던 변경사항 확인"
$dirty = Git-Out status --porcelain

if ($dirty) {
    Warn "'$current' 브랜치에 저장하지 않은 변경사항이 있습니다:"
    ($dirty -split "`n" | Select-Object -First 20) | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
    Write-Host ""
    Say "  1) 지금 브랜치에 커밋하고 넘어가기  (권장)"
    Say "  2) 잠시 치워두기 (git stash) - 나중에 sync.bat 옆의 안내 참고"
    Say "  3) 그대로 들고 새 브랜치로 넘어가기"
    Say "  4) 취소"
    $c = Read-Host "선택 (1/2/3/4)"
    switch ($c) {
        '1' {
            $msg = Read-Host "커밋 메시지 (엔터 = 자동)"
            if (-not $msg.Trim()) { $msg = "wip: $current 작업 정리" }
            $null = Git-Run add -A
            $null = Git-Run commit -m $msg
            Ok "커밋 완료"
            if (-not $offline -and $current -ne $mainBranch) {
                $null = Git-Run push -u origin $current
            }
        }
        '2' {
            $null = Git-Run stash push -u -m "start-day 자동 보관 $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
            Ok "보관 완료.  되돌리려면:  git stash pop"
        }
        '3' { Warn "변경사항을 들고 이동합니다" }
        default { Say "취소했습니다."; Pause-End; exit 0 }
    }
}

# ---------------------------------------------------------------
Step "$mainBranch 브랜치 최신화"

if (-not (Git-Run checkout $mainBranch)) {
    Die "$mainBranch 브랜치로 이동하지 못했습니다."
}

if (-not $offline) {
    if (-not (Git-Run pull --ff-only origin $mainBranch)) {
        Warn "$mainBranch 을(를) 빨리감기로 갱신하지 못했습니다."
        Warn "로컬 $mainBranch 에 원격에 없는 커밋이 있을 수 있습니다. 그대로 진행합니다."
    } else {
        Ok "$mainBranch 최신 상태"
    }
}

# ---------------------------------------------------------------
Step "오늘 브랜치 준비: $branch"

$localExists  = Git-Ok show-ref --verify --quiet "refs/heads/$branch"
$remoteExists = Git-Ok show-ref --verify --quiet "refs/remotes/origin/$branch"

if ($localExists) {
    $null = Git-Run checkout $branch
    Ok "기존 브랜치로 이동 (오늘 이미 만들어 두었네요)"
    if ($remoteExists -and -not $offline) { $null = Git-Run pull --ff-only origin $branch }
}
elseif ($remoteExists) {
    $null = Git-Run checkout -b $branch "origin/$branch"
    Ok "원격에 있던 오늘 브랜치를 받아왔습니다"
}
else {
    if (-not (Git-Run checkout -b $branch)) { Die "브랜치 생성 실패" }
    Ok "새 브랜치 생성 ($mainBranch 기준)"
    if (-not $offline) {
        if (Git-Run push -u origin $branch) { Ok "GitHub 에 업로드 (팀원이 바로 볼 수 있습니다)" }
        else { Warn "업로드 실패 - 나중에 end_day.bat 이 다시 시도합니다" }
    }
}

# ---------------------------------------------------------------
Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  준비 끝. 오늘 브랜치: $branch" -ForegroundColor Green
Write-Host "════════════════════════════════════════════" -ForegroundColor Green

$web = Get-RepoWebUrl
if ($web) { Say "  저장소: $web/tree/$branch" }
Say ""
Say "  이제 Unity 를 열고 작업하세요."
Say "  작업 끝나면  end_day.bat  더블클릭."
Pause-End
