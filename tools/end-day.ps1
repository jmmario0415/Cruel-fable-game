# ============================================================
#  end-day.ps1 - 오늘 작업을 커밋·업로드하고 PR 페이지를 연다.
#  실행: end_day.bat 더블클릭
# ============================================================

. (Join-Path $PSScriptRoot '_lib.ps1')

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  오늘 작업 마무리" -ForegroundColor Magenta
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta

$root = Enter-Repo
Assert-Git
if (-not (Test-Path (Join-Path $root '.git'))) { Die "먼저 setup.bat 을 실행해 주세요." }

$branch     = (Git-Out rev-parse --abbrev-ref HEAD).Trim()
$null       = Git-Out fetch origin --prune
$mainBranch = Get-MainBranch

Say "현재 브랜치: $branch"

if ($branch -eq $mainBranch) {
    Warn "$mainBranch 브랜치에서 바로 작업 중입니다."
    Warn "원칙적으로는 start_day.bat 으로 만든 일일 브랜치에서 작업해야 합니다."
    $go = Read-Host "그래도 계속할까요? (y/N)"
    if ($go -ne 'y') { Say "취소했습니다."; Pause-End; exit 0 }
}

# ---------------------------------------------------------------
Step "변경사항 확인"
$dirty = Git-Out status --porcelain

if ($dirty) {
    ($dirty -split "`n") | ForEach-Object { Write-Host "    $_" }
    Write-Host ""

    # 큰 파일 경고 (LFS 미추적 100MB 이상)
    $big = Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue |
           Where-Object { $_.Length -gt 100MB -and $_.FullName -notmatch '\\\.git\\' }
    if ($big) {
        Warn "100MB 가 넘는 파일이 있습니다. GitHub 는 100MB 초과 파일을 거부합니다:"
        $big | Select-Object -First 5 | ForEach-Object {
            Write-Host ("    {0}  ({1:N0} MB)" -f $_.FullName.Replace($root, '.'), ($_.Length / 1MB)) -ForegroundColor Yellow
        }
        Warn ".gitattributes 의 LFS 규칙에 확장자를 추가하거나 .gitignore 에 넣어 주세요."
    }

    $msg = Read-Host "커밋 메시지 (엔터 = 자동)"
    if (-not $msg.Trim()) { $msg = "작업 진행 $(Get-Date -Format 'yyyy-MM-dd')" }

    if (-not (Git-Run add -A))          { Die "git add 실패" }
    if (-not (Git-Run commit -m $msg))  { Die "커밋 실패" }
    Ok "커밋 완료: $msg"
} else {
    Ok "새 변경사항 없음"
}

# ---------------------------------------------------------------
Step "GitHub 로 업로드"
if (-not (Git-Run push -u origin $branch)) {
    Warn "push 에 실패했습니다. 원격에 새 커밋이 있는지 확인합니다..."
    if (Git-Run pull --no-rebase --no-edit origin $branch) {
        if (Git-Run push -u origin $branch) { Ok "합친 뒤 업로드 완료" }
        else { Die "업로드 실패. git status 로 상태를 확인해 주세요." }
    } else {
        Die "충돌이 났습니다. 충돌 파일을 정리한 뒤 git add . / git commit / git push 해 주세요."
    }
} else {
    Ok "업로드 완료"
}

# ---------------------------------------------------------------
Step "오늘 커밋 요약"
$log = Git-Out log "origin/$mainBranch..$branch" --pretty=format:"  %h  %s  (%an)"
if ($log) { $log -split "`n" | ForEach-Object { Write-Host $_ } }
else      { Say "  $mainBranch 대비 새 커밋이 없습니다." }

$stat = Git-Out diff --shortstat "origin/$mainBranch...$branch"
if ($stat) { Say ""; Say "  $stat" }

# ---------------------------------------------------------------
$web = Get-RepoWebUrl
if ($branch -ne $mainBranch -and $log -and $web) {
    Step "Pull Request"
    if ($true) {
        $prUrl = "$web/compare/$mainBranch...$branch" + "?expand=1"
        Say "  브라우저에서 PR 작성 페이지를 엽니다."
        Say "  $prUrl"
        try { Start-Process $prUrl } catch { Warn "브라우저를 열지 못했습니다. 위 주소를 직접 복사해 주세요." }
        Say ""
        Say "  PR 을 올린 뒤 팀원에게 리뷰를 요청하세요."
        Say "  병합(Merge)된 다음 날에는 start_day.bat 이 알아서 최신 $mainBranch 에서 새 브랜치를 땁니다."
    }
}

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  오늘도 수고하셨습니다." -ForegroundColor Green
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Pause-End
