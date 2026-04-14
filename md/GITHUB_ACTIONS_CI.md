# GitHub Actions CI

## What it does

- Runs CI checks on:
  - Pull request to `master`
  - Push to `master`
- Triggers Jenkins only after CI passes on `master` push

Workflow file:

- `.github/workflows/master-ci.yml`

## CI checks included

- Repository checkout with Git LFS
- Unity version detection from `ProjectSettings/ProjectVersion.txt`
- Merge conflict marker check in `Assets`, `Packages`, `ProjectSettings`

## Jenkins trigger integration

Set this secret in GitHub Actions:

- `JENKINS_WEBHOOK_URL`

Expected format:

- `https://<jenkins-host>/job/<job-name>/buildWithParameters?token=<your-token>`

When secret is not set:

- CI still succeeds
- Workflow prints warning and skips Jenkins trigger
