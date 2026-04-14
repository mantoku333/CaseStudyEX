pipeline {
  agent any

  parameters {
    string(name: 'git_sha', defaultValue: '', description: 'Commit SHA from GitHub Actions')
    string(name: 'git_ref', defaultValue: '', description: 'Git ref from GitHub Actions')
    string(name: 'cause', defaultValue: '', description: 'Trigger cause')
  }

  options {
    disableConcurrentBuilds()
    timestamps()
    timeout(time: 90, unit: 'MINUTES')
    buildDiscarder(logRotator(numToKeepStr: '20'))
  }

  environment {
    BUILD_METHOD = 'CaseStudy.Editor.CI.JenkinsBuild.PerformWindows64Build'
    BUILD_TARGET = 'StandaloneWindows64'
    BUILD_OUTPUT = 'Build/Windows/CaseStudy.exe'
    UNITY_LOG = 'Logs/jenkins-build.log'
  }

  stages {
    stage('Branch Info') {
      steps {
        script {
          def branchName = env.BRANCH_NAME ?: env.GIT_BRANCH ?: ''
          echo "Detected branch: ${branchName}"
          echo "Trigger cause: ${params.cause}"
          echo "Git SHA: ${params.git_sha}"
          echo "Git Ref: ${params.git_ref}"
        }
      }
    }

    stage('Unity Build (master only)') {
      when {
        expression {
          def branchName = env.BRANCH_NAME ?: env.GIT_BRANCH ?: ''
          def gitRef = params.git_ref ?: ''
          return gitRef == 'refs/heads/master' || branchName == 'master' || branchName.endsWith('/master')
        }
      }
      steps {
        bat '''
          if "%UNITY_PATH%"=="" (
            echo ERROR: UNITY_PATH is not set.
            echo Set UNITY_PATH in Jenkins global environment.
            exit /b 1
          )

          if not exist "%UNITY_PATH%" (
            echo ERROR: Unity executable not found at "%UNITY_PATH%"
            exit /b 1
          )

          git lfs version
          if %ERRORLEVEL% NEQ 0 (
            echo ERROR: git-lfs is not available on Jenkins node.
            exit /b 1
          )

          git lfs pull
          if %ERRORLEVEL% NEQ 0 (
            echo ERROR: git lfs pull failed.
            exit /b 1
          )

          if not exist "Logs" mkdir Logs

          "%UNITY_PATH%" -batchmode -nographics -quit ^
            -projectPath "%WORKSPACE%" ^
            -buildTarget "%BUILD_TARGET%" ^
            -executeMethod "%BUILD_METHOD%" ^
            -logFile "%WORKSPACE%\\%UNITY_LOG%"
        '''
      }
    }

    stage('Skip Non-master') {
      when {
        expression {
          def branchName = env.BRANCH_NAME ?: env.GIT_BRANCH ?: ''
          def gitRef = params.git_ref ?: ''
          return !(gitRef == 'refs/heads/master' || branchName == 'master' || branchName.endsWith('/master'))
        }
      }
      steps {
        echo 'Skipping build because branch is not master.'
      }
    }
  }

  post {
    always {
      archiveArtifacts artifacts: 'Logs/jenkins-build.log,Build/**/*', allowEmptyArchive: true, fingerprint: true
    }
  }
}
