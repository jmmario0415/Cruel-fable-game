# ============================================================
#  sync.ps1 - 팀원이 main 에 병합한 내용을 지금 브랜치로 가져온다.
#  실행: sync.bat 더블클릭
# ============================================================

. (Join-Path $PSScriptRoot '_lib.ps1')

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  팀 최신 내용 받아오기" -ForegroundColor Magenta
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta

$root = Enter-Repo
Assert-Git
if (-not (Test-Path (Join-Path $root '.git'))) { Die "먼저 setup.bat 을 실행해 주세요." }

$branch = (Git-Out rev-parse --abbrev-ref HEAD).Trim()
Say "현재 브랜치: $branch"

Step "원격 정보 가져오기"
if (-not (Git-Run fetch origin --prune)) { Die "GitHub 에 접속하지 못했습니다." }
$mainBranch = Get-MainBranch
Ok "기준 브랜치: $mainBranch"

# ---------------------------------------------------------------
Step "작업 중 변경사항 확인"
$dirty = Git-Out status --porcelain
$stashed = $false
if ($dirty) {
    ($dirty -split "`n" | Select-Object -First 20) | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
    Warn "저장하지 않은 변경사항이 있습니다."
    Say "  1) 잠시 치워두고(stash) 받아온 뒤 되돌리기  (권장)"
    Say "  2) 먼저 커밋하고 받아오기"
    Say "  3) 취소"
    $c = Read-Host "선택 (1/2/3)"
    switch ($c) {
        '1' { $null = Git-Run stash push -u -m "sync 자동 보관"; $stashed = $true; Ok "보관 완료" }
        '2' {
            $msg = Read-Host "커밋 메시지 (엔터 = 자동)"
            if (-not $msg.Trim()) { $msg = "wip: $(Get-Date -Format 'yyyy-MM-dd HH:mm')" }
            $null = Git-Run add -A
            $null = Git-Run commit -m $msg
            Ok "커밋 완료"
        }
        default { Say "취소했습니다."; Pause-End; exit 0 }
    }
} else { Ok "깨끗한 상태" }

# ---------------------------------------------------------------
$behind = Git-Out rev-list --count "$branch..origin/$mainBranch"
if ($behind -eq '0') {
    Ok "이미 최신입니다. 받아올 내용이 없습니다."
} else {
    Step "$mainBranch 의 새 커밋 $behind 개를 가져옵니다"
    (Git-Out log "$branch..origin/$mainBranch" --pretty=format:"  %h  %s  (%an)") -split "`n" | ForEach-Object { Write-Host $_ }
    Write-Host ""
    if (Git-Run merge "origin/$mainBranch" --no-edit) {
        Ok "합치기 완료"
    } else {
        Fail "충돌이 났습니다."
        Say  "  충돌 파일:"
        (Git-Out diff --name-only --diff-filter=U) -split "`n" | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        Say  ""
        Say  "  * .cs 파일: 편집기에서 <<<<<<< 표시를 정리한 뒤  git add <파일>"
        Say  "  * .unity / .prefab: 둘 중 하나를 통째로 고르는 게 안전합니다."
        Say  "      내 것 유지  →  git checkout --ours  <파일>"
        Say  "      팀원 것 사용 →  git checkout --theirs <파일>"
        Say  "      그 다음      →  git add <파일>"
        Say  "  * 전부 정리했으면  →  git commit"
        if ($stashed) { Warn "치워둔 변경사항이 있습니다. 정리 후  git stash pop  하세요." }
        Pause-End
        exit 1
    }
}

if ($stashed) {
    Step "치워둔 변경사항 되돌리기"
    if (Git-Run stash pop) { Ok "복원 완료" }
    else { Warn "복원 중 충돌. 파일을 정리한 뒤  git add .  해 주세요." }
}

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  최신 상태입니다." -ForegroundColor Green
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Pause-End
