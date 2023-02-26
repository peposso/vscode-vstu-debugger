// @ts-check
const vscode = require('vscode');

/**
 * @extends {vscode.DebugAdapterDescriptorFactory}
 */
class DebugAdapterFactory {

    /**
     * @param {vscode.ExtensionContext} context 
     */
    constructor(context) {
        this._context = context;
    }

    /**
     * @param {vscode.DebugSession} session 
     * @param {vscode.DebugAdapterExecutable?} executable 
     * @returns {vscode.DebugAdapterDescriptor}
     */
    createDebugAdapterDescriptor(session, executable) {
        if (typeof session.configuration.adapterPort === 'number') {
            return new vscode.DebugAdapterServer(session.configuration.adapterPort);
        }

        if (!executable) {
            const command = "dotnet";
            const args = [
                "run",
                "--project",
                "VstuBridgeDebugAdapter/VstuBridgeDebugAdapter.csproj",
            ];
            const options = {
                cwd: this._context.extensionPath,
                env: { "envVariable": "some value" }
            };
            executable = new vscode.DebugAdapterExecutable(command, args, options);
        }

        return executable;
    }
}

/**
 * @extends {vscode.DebugConfigurationProvider}
 */
class DebugConfigurationProvider {

    /**
     * Massage a debug configuration just before a debug session is being launched,
     * e.g. add all missing attributes to the debug configuration.
     * @param {vscode.WorkspaceFolder | undefined} folder
     * @param {vscode.DebugConfiguration} config
     * @param {vscode.CancellationToken} token
     */
    resolveDebugConfiguration(folder, config, token) {

        // if launch.json is missing or empty
        if (!config.type && !config.request && !config.name) {
            const editor = vscode.window.activeTextEditor;
            if (editor && editor.document.languageId === 'csharp') {
                config.type = 'vstu';
                config.name = 'Attach Unity using VSTU';
                config.request = 'attach';
            }
        }

        return config;
    }
}
/**
 * @param {vscode.ExtensionContext} context
 */
function activate(context) {

    // https://github.com/microsoft/vscode-mock-debug/blob/63e33ea07d9769ae9605212a13e96796333a61fd/src/activateMockDebug.ts#L155
    const provider = new DebugConfigurationProvider();
    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider('vstu', provider));

    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider('vstu', {
        /**
         * 
         * @param {vscode.WorkspaceFolder} _folder 
         * @returns {vscode.DebugConfiguration[]}
         */
        provideDebugConfigurations(_folder) {
            return [
                {
                    name: "Attach Unity by VSTU",
                    request: "attach",
                    type: "vstu",
                    adapterPort: 0,
                },
            ];
        }
    }, vscode.DebugConfigurationProviderTriggerKind.Dynamic));

    const factory = new DebugAdapterFactory(context);
    context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory('vstu', factory));

    const command = 'myExtension.sayHello';
    const commandHandler = (name) => {
        console.log(`Hello ${name}!!!`);
    };

    context.subscriptions.push(vscode.commands.registerCommand(command, commandHandler));
}

// This method is called when your extension is deactivated
function deactivate() { }

module.exports = {
    activate,
    deactivate
}
