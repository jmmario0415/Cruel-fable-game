# ============================================================
#  setup.ps1 - 최초 1회 실행. 이 폴더를 GitHub 저장소와 연결한다.
#  실행: setup.bat 더블클릭
# ============================================================

. (Join-Path $PSScriptRoot '_lib.ps1')

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  잔혹동화 로그라이크 - 개발 환경 최초 설정" -ForegroundColor Magenta
Write-Host "════════════════════════════════════════════" -ForegroundColor Magenta

$root = Enter-Repo
Say "작업 폴더: $root"

# ---------------------------------------------------------------
Step "1/7  Git 확인"
Assert-Git

if (Test-GitLfs) {
    Ok "Git LFS 확인"
    $null = Git-Out lfs install
    Ok "Git LFS 활성화"
} else {
    Warn "Git LFS 가 없습니다. 이미지·오디오 같은 큰 파일 관리에 필요합니다."
    Warn "https://git-lfs.com 에서 설치한 뒤 이 스크립트를 다시 실행하는 걸 권합니다."
    $go = Read-Host "지금은 LFS 없이 계속할까요? (y/N)"
    if ($go -ne 'y') { Die "설치 후 다시 실행해 주세요." }
}

# ---------------------------------------------------------------
Step "2/7  내 정보"

$cfg = Read-Config

$name = Git-Out config --global user.name
if ($LASTEXITCODE -ne 0 -or -not $name.Trim()) {
    $name = Read-Host "커밋에 쓸 이름 (예: 김남주)"
    $null = Git-Out config --global user.name $name
}
Ok "이름: $name"

$mail = Git-Out config --global user.email
if ($LASTEXITCODE -ne 0 -or -not $mail.Trim()) {
    $mail = Read-Host "GitHub 계정 이메일"
    $null = Git-Out config --global user.email $mail
}
Ok "이메일: $mail"

$initials = Get-Initials
Ok "브랜치 이니셜: $initials  (오늘이면 $(Get-TodayBranch $initials))"

# 윈도우 줄바꿈 문제 예방
$null = Git-Out config core.autocrlf true
$null = Git-Out config core.longpaths true
$null = Git-Out config pull.rebase false
Ok "Windows 용 Git 옵션 설정 (autocrlf / longpaths / merge pull)"

# ---------------------------------------------------------------
Step "3/7  저장소 초기화"

if (Test-Path (Join-Path $root '.git')) {
    Ok "이미 Git 저장소입니다"
} else {
    if (-not (Git-Run init -b main)) { Die "git init 실패" }
    Ok "저장소 생성"
}

# ---------------------------------------------------------------
Step "4/7  GitHub 원격 연결"

$remote = Git-Out remote get-url origin
if ($LASTEXITCODE -ne 0 -or -not $remote.Trim()) {
    Write-Host ""
    Write-Host "GitHub 저장소 주소를 붙여넣어 주세요." -ForegroundColor Yellow
    Write-Host "  GitHub 저장소 페이지 → 초록색 [Code] 버튼 → HTTPS 탭의 주소"
    Write-Host "  예) https://github.com/사용자명/fairy-tale.git"
    while ($true) {
        $remote = (Read-Host "저장소 주소").Trim()
        if ($remote -match '^(https?://|git@)') { break }
        Warn "https://... 또는 git@... 형태로 입력해 주세요."
    }
    if (-not (Git-Run remote add origin $remote)) { Die "원격 추가 실패" }
}
Ok "origin = $remote"

Step "5/7  원격 상태 확인"
if (-not (Git-Run fetch origin)) {
    Die "GitHub 에 접속하지 못했습니다.`n     - 주소가 맞는지`n     - GitHub 로그인(자격 증명)이 되어 있는지 확인해 주세요."
}

$heads = Git-Out ls-remote --heads origin
$remoteEmpty = [string]::IsNullOrWhiteSpace($heads)
$mainBranch = if ($remoteEmpty) { 'main' } else { Get-MainBranch }

if ($remoteEmpty) { Ok "원격 저장소가 비어 있습니다 (첫 커밋을 올립니다)" }
else              { Ok "원격 기본 브랜치: $mainBranch" }

# ---------------------------------------------------------------
Step "6/7  첫 커밋 / 원격과 합치기"

$null = Git-Out checkout -B $mainBranch

if (-not (Git-Run add -A)) { Die "git add 실패" }

$staged = Git-Out diff --cached --name-only
if ($staged) {
    $null = Git-Out commit -m "chore: 개발 환경 설정 (gitignore, LFS, 브랜치 자동화 스크립트)"
    Ok "첫 커밋 완료"
} else {
    Ok "커밋할 새 파일 없음"
}

if (-not $remoteEmpty) {
    Say "  원격 내용을 가져와 합칩니다..."
    $merged = Git-Run pull origin $mainBranch --allow-unrelated-histories --no-rebase --no-edit
    if (-not $merged) {
        Write-Host ""
        Fail "원격과 합치는 중 충돌이 났습니다."
        Say  "  아래처럼 처리해 주세요:"
        Say  "    1) 충돌 난 파일을 열어 <<<<<<< 표시를 정리"
        Say  "    2) git add .  →  git commit"
        Say  "    3) 이 스크립트를 다시 실행"
        Pause-End
        exit 1
    }
    Ok "합치기 완료"
}

if (-not (Git-Run push -u origin $mainBranch)) {
    Die "push 실패. GitHub 저장소에 쓰기 권한(협업자 등록)이 있는지 확인해 주세요."
}
Ok "$mainBranch 브랜치 업로드 완료"

# ---------------------------------------------------------------
Step "7/7  LFS 상태"
if (Test-GitLfs) {
    $tracked = Git-Out lfs track
    Say "  .gitattributes 기준 추적 규칙:"
    ($tracked -split "`n" | Select-Object -First 12) | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    Ok "LFS 설정 완료"
}

Write-Host ""
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  설정 끝!" -ForegroundColor Green
Write-Host "════════════════════════════════════════════" -ForegroundColor Green
Say ""
Say "  이제부터 매일 아침:"
Say "     start_day.bat  더블클릭  →  $(Get-TodayBranch $initials) 브랜치에서 작업 시작"
Say ""
Say "  하루 끝날 때:"
Say "     end_day.bat    더블클릭  →  커밋·업로드 + PR 페이지 열기"
Say ""
Say "  작업 중 상대방 변경사항 받아오기:"
Say "     sync.bat       더블클릭"
Pause-End
