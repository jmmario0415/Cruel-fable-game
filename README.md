# 잔혹동화 로그라이크

Unity 6 (6000.3.22f1) · 2D URP · 개발자 2명

---

## 처음 한 번만 (각자 자기 컴퓨터에서)

1. **Git** 설치 — https://git-scm.com/download/win
2. **Git LFS** 설치 — https://git-lfs.com
3. 이 폴더에서 **`setup.bat` 더블클릭**
   - 이름 / 이메일 / 이니셜(예: `KNJ`)을 물어봅니다
   - GitHub 저장소 주소를 붙여넣으면 연결까지 끝납니다

> 팀원이 새로 합류할 때는 빈 폴더에서
> `git clone <저장소 주소> Fairy_Tale` 한 다음 `setup.bat` 을 실행하면 됩니다.

---

## 매일 이렇게 씁니다

| 언제 | 무엇을 | 하는 일 |
|---|---|---|
| 작업 시작할 때 | **`start_day.bat`** | `main` 최신화 → 오늘 브랜치 생성 → 이동 → GitHub 업로드 |
| 작업 중간중간 | **`sync.bat`** | 팀원이 `main` 에 병합한 내용을 내 브랜치로 가져오기 |
| 작업 끝낼 때 | **`end_day.bat`** | 커밋 → 업로드 → PR 작성 페이지 자동으로 열기 |

브랜치 이름은 자동으로 이렇게 만들어집니다.

```
2026_09_05_KNJ
└연도┘└월┘└일┘ └이니셜┘
```

같은 날 다시 `start_day.bat` 을 눌러도 새로 만들지 않고 오늘 브랜치로 그냥 이동합니다.

---

## 브랜치 규칙

```
main ───●────────●────────●──────►   항상 실행되는 안정 버전
         \        \        \
          ● 2026_09_05_KNJ  \        하루치 작업
           \                 ● 2026_09_06_XXX
            └─ PR ─┘
```

- `main` 에 **직접 커밋하지 않습니다.** 반드시 일일 브랜치 → PR → 병합.
- PR 은 상대방이 한 번 보고 **Merge** 를 누릅니다 (2인 팀이라 가볍게, 대충 훑고 승인해도 됩니다).
- 병합된 브랜치는 GitHub 에서 `Delete branch` 로 지워도 됩니다. 기록은 `main` 에 남습니다.
- 다음 날 `start_day.bat` 은 병합된 최신 `main` 에서 새 브랜치를 뜹니다.

### PR 올리는 법
`end_day.bat` 이 브라우저로 PR 작성 페이지를 자동으로 엽니다.
제목과 내용(템플릿이 미리 채워져 있음)을 적고 **Create pull request** 를 누르면 끝.

---

## Unity 협업할 때 꼭 지킬 것

**1. 씬(`.unity`)과 프리팹(`.prefab`)은 동시에 건드리지 않습니다.**
텍스트로 저장되긴 하지만 자동 병합이 거의 안 됩니다.
같은 씬을 만져야 하면 채팅으로 "나 Room_Prototype 씬 잡을게" 라고 먼저 말하세요.

**2. `.meta` 파일은 항상 같이 커밋합니다.**
`.gitignore` 에 `.meta` 를 넣으면 안 됩니다. 이미 그렇게 설정돼 있습니다.
Unity 에서 파일을 지울 때는 탐색기가 아니라 **Unity Project 창에서** 지우세요 (`.meta` 가 같이 지워짐).

**3. 큰 파일은 Git LFS 가 알아서 처리합니다.**
`.gitattributes` 에 png / wav / fbx / psd 등이 등록돼 있습니다.
새 확장자를 쓰게 되면 `.gitattributes` 에 한 줄 추가해 주세요.

**4. `Library/` 폴더는 절대 올라가지 않습니다.** (Unity 가 다시 만듭니다)
받아온 직후 Unity 첫 실행이 몇 분 걸리는 건 정상입니다.

---

## 씬·프리팹 충돌을 줄이고 싶다면 (선택)

Unity 가 제공하는 YAML 전용 병합 도구를 Git 에 등록해 두면
`.unity` / `.prefab` 충돌을 상당 부분 자동으로 풀어 줍니다.

`Git Bash` 나 PowerShell 에서 한 번만 실행 (경로의 Unity 버전은 본인 것에 맞게):

```powershell
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config --global merge.unityyamlmerge.recursive binary
```

---

## 충돌이 났을 때

`sync.bat` 이나 `end_day.bat` 이 충돌을 알려 주면:

```powershell
# 어떤 파일이 충돌했는지 보기
git diff --name-only --diff-filter=U
```

- **`.cs` 코드 파일** → 편집기로 열어서 `<<<<<<<` `=======` `>>>>>>>` 를 정리 → `git add <파일>`
- **`.unity` / `.prefab`** → 섞으려 하지 말고 한쪽을 통째로 고르세요
  ```powershell
  git checkout --ours   <파일>   # 내 것 유지
  git checkout --theirs <파일>   # 팀원 것 사용
  git add <파일>
  ```
- 전부 정리했으면 `git commit`

혼자 못 풀겠으면 그냥 두고 상대방과 화면 공유하면서 같이 보세요. 되돌리는 게 더 빠를 때도 많습니다.

---

## 폴더 구조

```
Fairy_Tale/
├─ setup.bat            최초 1회 - 환경 설정
├─ start_day.bat        매일 아침 - 오늘 브랜치 만들기
├─ end_day.bat          매일 저녁 - 커밋·업로드·PR
├─ sync.bat             수시로   - main 최신 내용 받아오기
├─ tools/
│   ├─ _lib.ps1         공용 함수
│   ├─ setup.ps1
│   ├─ start-day.ps1
│   ├─ end-day.ps1
│   ├─ sync.ps1
│   └─ dev.config       개인 설정 (이니셜) — 공유 안 됨
├─ .gitignore           Unity 용 무시 규칙
├─ .gitattributes       줄바꿈 · LFS · 씬 병합 설정
├─ .github/
│   └─ pull_request_template.md
├─ Assets/              ← Unity 프로젝트 (여기에 생성)
├─ Packages/
└─ ProjectSettings/
```

---

## Unity 프로젝트 만들기

`setup.bat` 을 끝낸 뒤:

1. Unity Hub → **New project** → **2D (URP)** → Unity **6000.3.22f1**
2. Project name: `Fairy_Tale`, Location: `C:\Projects` **→ 저장 위치가 `C:\Projects\Fairy_Tale` 이 되도록**
   (이미 폴더가 있다고 나오면, Unity Hub 에서 `Add project from disk` 로 이 폴더를 지정해도 됩니다)
3. Unity 가 `Assets/`, `Packages/`, `ProjectSettings/` 를 만들면
4. `end_day.bat` 을 눌러 커밋 → PR → 병합

만든 다음 `Edit ▸ Project Settings ▸ Editor` 에서 아래 두 가지를 확인하세요 (Git 협업 필수):

- **Version Control ▸ Mode** = `Visible Meta Files`
- **Asset Serialization ▸ Mode** = `Force Text`
