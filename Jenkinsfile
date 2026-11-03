pipeline {
    agent any 

    environment {
        DEPLOY_PATH = '/app/deploy'
    }

    stages {
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
                    userRemoteConfigs: [[url: 'https://github.com/HedpE-/GameStash']]
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
                // Restaura explicitamente os pacotes direcionados ao ecossistema Windows
                sh 'dotnet restore GameStash.slnx -r win-x64'
                
                // AJUSTADO: Força o nome do executável para GameStash (.exe é anexado automaticamente pelo compilador no Runtime win-x64)
                sh 'dotnet publish GameStash.Web.GUI/GameStash.Web.GUI.csproj -c Release -r win-x64 --self-contained false -p:AssemblyName=GameStash -o ./publish_output'
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
