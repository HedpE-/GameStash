pipeline {
    agent {
        docker {
            image 'mcr.microsoft.com/dotnet/sdk:9.0'
            // CORREÇÃO: Cria e mapeia um volume real para reutilizar a cache de pacotes do NuGet
            args '-v nuget-cache:/root/.nuget/packages'
        }
    }

    environment {
        DEPLOY_PATH = '/app/deploy'
    }

    stages {
        stage('Wipe Workspace') {
            steps {
                // Garante que o folder está limpo antes de tentar clonar
                cleanWs()
            }
        }

        stage('Checkout Code') {
            steps {
                checkout([
                    $class: 'GitSCM', 
                    branches: [[name: '*/main']], 
                    extensions: [[$class: 'CleanBeforeCheckout']], // Força limpeza do Git
                    userRemoteConfigs: [[url: 'https://github.com/HedpE-/GameStash.git']]
                ])
            }
        }

        stage('Verify .NET Version') {
            steps {
                sh 'dotnet --version'
            }
        }

        stage('Restore & Build Release') {
            steps {
                // Executa o restauro explícito aproveitando o volume de cache criado acima
                sh 'dotnet restore GameStash.slnx'
                sh 'dotnet publish GameStash.Web.GUI/GameStash.Web.GUI.csproj -c Release -o ./publish_output'
            }
        }

        stage('Deploy to Folder Mount') {
            steps {
                script {
                    echo "Deploying release to target folder: ${env.DEPLOY_PATH}"
                    sh "mkdir -p ${env.DEPLOY_PATH}"
                    sh "cp -r ./publish_output/* ${env.DEPLOY_PATH}/"
                    echo "Deployment completed and files successfully written!"
                }
            }
        }
    }

    post {
        success {
            echo 'Pipeline executed successfully and release files are in place!'
        }
        failure {
            echo 'Pipeline failed during execution. Check logs above.'
        }
    }
}
