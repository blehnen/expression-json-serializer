pipeline {
    agent { label 'docker' }
    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = 'true'
    }
    stages {
        stage('Build') {
            steps {
                sh 'dotnet restore "Aq.ExpressionJsonSerializer.sln"'
                sh 'dotnet build "Aq.ExpressionJsonSerializer.sln" -c Debug --no-restore'
            }
        }
        stage('Test net10.0') {
            steps {
                sh 'dotnet test "Aq.ExpressionJsonSerializer.Tests/Aq.ExpressionJsonSerializer.Tests.csproj" -f net10.0 --no-build -c Debug'
            }
        }
        stage('Test net8.0') {
            steps {
                sh 'dotnet test "Aq.ExpressionJsonSerializer.Tests/Aq.ExpressionJsonSerializer.Tests.csproj" -f net8.0 --no-build -c Debug'
            }
        }
    }
    post {
        failure { echo 'Pipeline failed. Check stage logs for details.' }
        success { echo 'Pipeline completed successfully.' }
    }
}
