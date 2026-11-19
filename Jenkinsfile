pipeline {
    agent any 

    environment {
        DEPLOY_PATH = '/app/deploy'
        // Define a local directory in the workspace to isolate the .NET SDK
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
                    echo "Purging old cache files..."
                    // Force removal of any corrupted file to ensure a clean download
                    sh 'rm -f dotnet-install.sh'
                    
                    echo "Downloading official Microsoft installation script..."
                    // Download the raw bash script using the direct CDN directory
                    sh 'curl -sSL https://dot.net -o dotnet-install.sh'
                    sh 'chmod +x dotnet-install.sh'
                    
                    echo "Installing .NET 9 SDK into local directory..."
                    // FIX: Single-quoted shell block ('') with double quotes around the variable
                    // This prevents Linux from splitting the path at the space in "Release Pipeline"
                    sh 'bash ./dotnet-install.sh --version latest --channel 9.0 --install-dir "${DOTNET_ROOT}"'
                }
            }
        }

        stage('Verify .NET Version') {
            steps {
                // Dynamically inject local binaries into PATH for this execution scope only
                withEnv(["PATH+DOTNET=${env.DOTNET_ROOT}"]) {
                    sh 'dotnet --version'
                }
            }
        }

        stage('Restore & Build Release') {
            steps {
                withEnv(["PATH+DOTNET=${env.DOTNET_ROOT}"]) {
                    echo "Restoring solution dependencies..."
                    sh 'dotnet restore GameStash.slnx -r linux-x64'
                    
                    echo "Publishing Web GUI with custom assembly name..."
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
