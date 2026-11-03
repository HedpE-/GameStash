pipeline {
    // Usa diretamente um contentor oficial da Microsoft com o .NET SDK instalado
    agent {
        docker {
            image 'mcr.microsoft.com/dotnet/sdk:8.0'
            // Permite persistir o cache do NuGet entre builds para ser mais rápido
            args '-v maven-m2:/root/.nuget/packages' 
        }
    }

    environment {
        // Caminho do folder mount no contentor do Jenkins onde a release será colocada
        DEPLOY_PATH = '/app/deploy'
    }

    stages {
        stage('Checkout Code') {
            steps {
                checkout([
                    $class: 'GitSCM', 
                    branches: [[name: '*/main']], 
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
                // Compila a aplicação Web em modo Release
                sh 'dotnet publish GameStash.Web.GUI/GameStash.Web.GUI.csproj -c Release -o ./publish_output'
            }
        }

        stage('Deploy to Folder Mount') {
            steps {
                script {
                    echo "Deploying release to target folder: ${env.DEPLOY_PATH}"
                    
                    // Cria a pasta de destino caso não exista
                    sh "mkdir -p ${env.DEPLOY_PATH}"
                    
                    // Copia os ficheiros compilados para o volume mapeado
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