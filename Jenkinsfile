pipeline {
    agent any 

    environment {
        DEPLOY_PATH = '/app/deploy'
        // Define uma diretoria local no workspace para isolar o SDK do .NET
        DOTNET_ROOT = "${WORKSPACE}/.dotnet"
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

        stage('Install .NET 9 SDK') {
            steps {
                script {
                    echo "A descarregar o instalador oficial do .NET..."
                    // Transfere o script de instalação automatizado da Microsoft
                    sh 'curl -sSL https://dot.net -o dotnet-install.sh'
                    sh 'chmod +x dotnet-install.sh'
                    
                    echo "A instalar o .NET 9 SDK na diretoria local..."
                    // Executa a instalação silenciosa apontando para a versão estável do .NET 9
                    sh "./dotnet-install.sh --version latest --channel 9.0 --install-dir ${env.DOTNET_ROOT}"
                }
            }
        }

        stage('Verify .NET Version') {
            steps {
                // Injeta dinamicamente os binários locais no PATH apenas para o escopo desta execução
                withEnv(["PATH+DOTNET=${env.DOTNET_ROOT}"]) {
                    sh 'dotnet --version'
                }
            }
        }

        stage('Restore & Build Release') {
            steps {
                withEnv(["PATH+DOTNET=${env.DOTNET_ROOT}"]) {
                    echo "A restaurar as dependências da solução..."
                    sh 'dotnet restore GameStash.slnx -r linux-x64'
                    
                    echo "A compilar a Web GUI com o nome customizado..."
                    sh 'dotnet publish GameStash.Web.GUI/GameStash.Web.GUI.csproj -c Release -r linux-x64 --self-contained false -p:AssemblyName=GameStash -o ./publish_output'
                }
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