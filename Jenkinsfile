pipeline {
    agent any 

    environment {
        DEPLOY_PATH = '/app/deploy'
    }

    stages {
        stage('Wipe Workspace') {
            steps {
                // Garante um diretório de build totalmente limpo
                cleanWs()
            }
        }

        stage('Checkout Code') {
            steps {
                checkout([
                    $class: 'GitSCM', 
                    branches: [[name: '*/main']], 
                    extensions: [[$class: 'CleanBeforeCheckout']], 
                    userRemoteConfigs: [[url: 'https://github.com']]
                ])
            }
        }

        stage('Verify .NET Version') {
            steps {
                // Valida se o comando .NET está acessível globalmente
                sh 'dotnet --version'
            }
        }

        stage('Restore & Build Release') {
            steps {
                // Restaura as dependências NuGet e compila o projeto Web em modo de produção
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
                    echo "Deployment completed successfully!"
                }
            }
        }
    }

    post {
        success {
            echo 'Pipeline executed successfully! GameStash built and deployed.'
        }
        failure {
            echo 'Pipeline failed. Check the compilation logs above.'
        }
    }
}
