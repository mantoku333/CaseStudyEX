# Jenkins Setup (master merge build)

## Goal

- Develop on `develop`
- Open PR `develop -> master`
- Run GitHub Actions CI on PR
- After merge (`master` push), GitHub Actions triggers Jenkins build

## 1. Prerequisites

- Jenkins server + Windows agent (or Windows Jenkins host)
- Unity `6000.3.8f1` installed on Jenkins node
- Unity license activated on that node
- Git LFS installed on Jenkins node (`git lfs version` works)
- Jenkins plugins:
  - Pipeline
  - Git
  - GitHub Integration (or GitHub plugin)

## 2. Set Unity Path in Jenkins

Set global environment variable in Jenkins:

- Name: `UNITY_PATH`
- Value example: `C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.8f1\\Editor\\Unity.exe`

Optional (if you want custom build output):

- Name: `UNITY_BUILD_PATH`
- Value example: `Build\\Windows\\CaseStudy.exe`

## 3. Create Jenkins Pipeline Job

1. `New Item` -> `Pipeline`
2. Pipeline definition: `Pipeline script from SCM`
3. SCM: `Git`
4. Repository URL: your GitHub repository URL
5. Script Path: `Jenkinsfile`
6. Save

## 4. Enable Remote Trigger on Jenkins

In Jenkins job configuration:

1. Enable `Trigger builds remotely (e.g., from scripts)`
2. Set an auth token (example: `case-study-trigger-token`)
3. Save

Build URL format:

- `https://<jenkins-host>/job/<job-name>/buildWithParameters?token=<your-token>`

## 5. Configure GitHub Actions Secret

In GitHub repository:

1. `Settings` -> `Secrets and variables` -> `Actions`
2. Add repository secret:
   - Name: `JENKINS_WEBHOOK_URL`
   - Value: Jenkins build URL from Step 4

## 6. Branch Behavior

- GitHub Actions CI runs on:
  - PR to `master`
  - Push to `master`
- Jenkins trigger runs only when:
  - CI passed
  - Event is `push` to `master`
- `Jenkinsfile` also has `master` branch guard in build stage

## 7. Build Output

- Unity log: `Logs/jenkins-build.log`
- Build output: `Build/Windows/CaseStudy.exe`
- Jenkins archives:
  - `Logs/jenkins-build.log`
  - `Build/**/*`

## 8. Unity Build Entry Point

- Execute method used by Jenkins:
  - `CaseStudy.Editor.CI.JenkinsBuild.PerformWindows64Build`
- Source file:
  - `Assets/Editor/CI/JenkinsBuild.cs`
