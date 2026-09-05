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
Step "무시돼야 할 폴더가 추적되고 있는지 점검"
$junkDirs = @('Library','Temp','Logs','UserSettings','obj','Build','Builds')
$junk = @()
$junkQuery = @('-c', 'core.quotepath=false', 'ls-files', '--') + $junkDirs
foreach ($f in ((Git-Out @junkQuery) -split "`n")) {
    $f = $f.Trim(); if ($f) { $junk += $f }
}
if ($junk) {
    Warn "Unity 가 자동 생성하는 폴더가 Git 에 추적되고 있습니다 ($($junk.Count) 개):"
    $junk | Select-Object -First 5 | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
    Warn ".gitignore 가 제대로 적용되지 않은 상태입니다."
    $c = Read-Host "지금 추적에서 빼고 계속할까요? (Y/n)"
    if ($c -ne 'n') {
        foreach ($d in $junkDirs) { $null = Git-Out rm -r --cached --ignore-unmatch -q -- $d }
        Ok "추적 해제 완료 (폴더와 파일은 그대로 남아 있습니다)"
    }
} else {
    Ok "깨끗합니다"
}

# ---------------------------------------------------------------
Step "변경사항 확인"
$dirty = Git-Out status --porcelain

if ($dirty) {
    ($dirty -split "`n") | ForEach-Object { Write-Host "    $_" }
    Write-Host ""

    $msg = Read-Host "커밋 메시지 (엔터 = 자동)"
    if (-not $msg.Trim()) { $msg = "작업 진행 $(Get-Date -Format 'yyyy-MM-dd')" }

    if (-not (Git-Run add -A)) { Die "git add 실패" }

    # ---- 올라갈 파일만 점검한다 (Library/ 같은 무시된 폴더는 아예 보지 않음) ----
    $staged = @()
    foreach ($rel in ((Git-Out -c core.quotepath=false diff --cached --name-only) -split "`n")) {
        $rel = $rel.Trim()
        if ($rel) { $staged += $rel }
    }

    # 100MB 넘는 파일 (LFS 로 올라가는 건 괜찮다)
    $big = @()
    foreach ($rel in $staged) {
        $p = Join-Path $root ($rel -replace '/', '\')
        if (Test-Path -LiteralPath $p) {
            $len = (Get-Item -LiteralPath $p).Length
            if ($len -gt 95MB) {
                $attr = Git-Out check-attr filter -- $rel
                if ($attr -notmatch 'filter:\s*lfs') { $big += [pscustomobject]@{ Path = $rel; MB = [math]::Round($len / 1MB) } }
            }
        }
    }
    if ($big) {
        Warn "GitHub 가 거부할 큰 파일이 커밋에 들어 있습니다 (100MB 초과, LFS 미적용):"
        $big | Select-Object -First 5 | ForEach-Object { Write-Host ("    {0}  ({1} MB)" -f $_.Path, $_.MB) -ForegroundColor Yellow }
        Warn ".gitattributes 에 해당 확장자의 LFS 규칙을 추가하거나 .gitignore 에 넣어 주세요."
        $c = Read-Host "그래도 커밋할까요? (y/N)"
        if ($c -ne 'y') { Say "중단했습니다. 파일 정리 후 다시 실행해 주세요."; Pause-End; exit 0 }
    }

    if (-not (Git-Run commit -m $msg)) { Die "커밋 실패" }
    Ok "커밋 완료: $msg  (파일 $($staged.Count) 개)"
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
