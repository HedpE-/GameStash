pipeline {
    agent {
        docker {
            image '://microsoft.com'
            args '-v nuget-cache:/root/.nuget/packages'
        }
    }

    environment {
        DEPLOY_PATH = '/app/deploy'
    }

    stages {
        // Passo executado antes de qualquer validação para libertar o Socket
        stage('Fix Docker Permissions') {
            agent any // Corre diretamente no host do Jenkins
            steps {
                script {
                    echo "A corrigir permissões do socket do Docker..."
                    // Altera as permissões diretamente usando o CLI do Jenkins
                    sh 'chmod 666 /var/run/docker.sock || true'
                    sh 'chmod +x /usr/bin/docker || true'
                }
            }
        }

        stage('Wipe Workspace') {
            steps {
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
                sh 'dotnet --version'
            }
        }

        stage('Restore & Build Release') {
            steps {
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
                    echo "Deployment completed!"
                }
            }
        }
    }
}