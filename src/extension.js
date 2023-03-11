// @ts-check
const vscode = require('vscode');
const util = require('util');
const { exec } = require('child_process');
const path = require('path');

/** @type {vscode.OutputChannel?} */
let outputChannel;

/**
 * @param {*} o
 */
function output(o) {
    outputChannel = outputChannel || vscode.window.createOutputChannel("VSTU Unity Debugger");
    if (typeof o === 'string') {
        outputChannel.appendLine(o)
    } else {
        outputChannel.appendLine(JSON.stringify(o))
    }
}

/**
 * @returns {Promise<{
 *  command: string,
*   args: string[],
 *  options: {
 *      cwd: string,
 *      env: {[key: string]: string}
 *  }
 * }>}
 */
async function createAdaptorCommand(context) {
    const conf = vscode.workspace.getConfiguration('vstu-debugger');
    /** @type {string?} */
    let vstuPath = conf.get("vstuPath");
    /** @type {string?} */
    let targetFramework = conf.get("targetFramework");

    if (!vstuPath || vstuPath == 'auto') {
        vstuPath = await searchVstuPath();
        vstuPath = vstuPath || "";
    }

    if (!targetFramework || targetFramework == 'auto') {
        if (process.platform === "win32") {
            const is2019 = vstuPath.replace(/\\/g, '/').includes('/2019/')
            targetFramework = is2019 ? "net472" : 'net7.0';
        } else {
            targetFramework = 'net7.0';
        }
    }

    const command = "dotnet";
    const args = [
        "run",
        "--project",
        "VstuBridgeDebugAdapter/VstuBridgeDebugAdapter.csproj",
    ];

    const options = {
        cwd: context.extensionPath,
        env: {
            CONF_VSTU_PATH: vstuPath,
            CONF_TARGET_FRAMEWORK: targetFramework,
        },
    };

    return { command, args, options }
}

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
     * @returns {Promise<vscode.DebugAdapterDescriptor>}
     */
    async createDebugAdapterDescriptor(session, executable) {
        if (typeof session.configuration.adapterPort === 'number') {
            return new vscode.DebugAdapterServer(session.configuration.adapterPort);
        }

        if (!executable) {
            const { command, args, options } = await createAdaptorCommand(this._context);
            if (!options.env.CONF_VSTU_PATH) {
                showVstuNotFoundError();
                throw new Error("'Visual Studio Tools for Unity' was not found.");
            }

            if (!session.configuration.waitDebuggerAttached) {
                args.push("-c", "Release")
            }

            output({ command, args, options })
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
     * @param {vscode.CancellationToken} _token
     */
    resolveDebugConfiguration(folder, config, _token) {

        // if launch.json is missing or empty
        if (!config.type && !config.request && !config.name) {
            const editor = vscode.window.activeTextEditor;
            if (editor && editor.document.languageId === 'csharp') {
                config.name = 'Unity Editor (VSTU)';
                config.type = 'vstu-unity';
                config.request = 'attach';
                config.projectPath = '${workspaceFolder}';
            }
        }

        return config;
    }
}

async function searchVstuPath() {
    const dirs = []
    if (process.platform === "win32") {
        const vs2022 = "C:/Program Files/Microsoft Visual Studio/2022/{Edition}/Common7/IDE/Extensions/Microsoft/Visual Studio Tools for Unity"
        const vs2019 = "C:/Program Files (x86)/Microsoft Visual Studio/2019/{Edition}/Common7/IDE/Extensions/Microsoft/Visual Studio Tools for Unity"
        const editions = ["Community", "Professional", "Enterprise"]
        for (const edition of editions) {
            dirs.push(vs2022.replace("{Edition}", edition));
        }
        for (const edition of editions) {
            dirs.push(vs2019.replace("{Edition}", edition));
        }
    } else if (process.platform === "darwin") {
        dirs.push("/Applications/Visual Studio.app/Contents/MonoBundle/AddIns/MonoDevelop.Unity");
    }

    const vstuAssemblies = [
        "SyntaxTree.VisualStudio.Unity.Messaging.dll",
        "SyntaxTree.VisualStudio.Unity.Common.dll",
        "SyntaxTree.Mono.Debugger.Soft.dll",
    ]

    for (const dir of dirs) {
        if (await existsAll(dir, vstuAssemblies)) {
            return dir;
        }
    }

    return null;
}

async function existsAll(dir, names) {
    const fs = vscode.workspace.fs;

    for (const name of names) {
        try {
            const uri = vscode.Uri.file(path.join(dir, name))
            await fs.stat(uri)
        } catch (e) {
            return false;
        }
    }
    return true;
}

async function showVstuNotFoundError() {
    const visitGuideButton = "Visit guide page";
    const vstGuideWinUri = vscode.Uri.parse("https://learn.microsoft.com/visualstudio/gamedev/unity/get-started/getting-started-with-visual-studio-tools-for-unity?pivots=windows");
    const vstGuideMacUri = vscode.Uri.parse("https://learn.microsoft.com/visualstudio/gamedev/unity/get-started/getting-started-with-visual-studio-tools-for-unity?pivots=macos");

    const chosen = await vscode.window.showErrorMessage(
        "'Visual Studio Tools for Unity' was not found. Install or check the'vstu-debugger.vstuPath' setting.",
        visitGuideButton);
    if (chosen === visitGuideButton) {
        if (process.platform === "darwin") {
            vscode.env.openExternal(vstGuideMacUri);
        } else {
            vscode.env.openExternal(vstGuideWinUri);
        }
    }
}

/**
 * @param {vscode.ExtensionContext} context
 */
async function checkRuntime(context) {
    const visitDownloadButton = "Visit download page";
    const netSdkDownloadUri = vscode.Uri.parse("https://dotnet.microsoft.com/download/dotnet/7.0");

    try {
        output("exec> dotnet --version")
        const { stdout } = await util.promisify(exec)("dotnet --version")
        output(stdout.trim())
        const semVer = stdout.trim().split('.');
        if (semVer.length != 3) {
            vscode.window.showErrorMessage(
                "unknown dotnet version: " + stdout.trim());
            return;
        }
        output(semVer)
        const major = parseInt(semVer[0]);
        if (major < 7) {
            const chosen = await vscode.window.showErrorMessage(
                "requires .NET 7.0 SDK. Please install .NET 7.0 SDK.", visitDownloadButton);
            if (chosen === visitDownloadButton) {
                vscode.env.openExternal(netSdkDownloadUri);
            }
            return;
        }
    } catch (e) {
        output(e)
        const chosen = await vscode.window.showErrorMessage(
            "dotnet command not found. Please install .NET 7.0 SDK.", visitDownloadButton);
        output(chosen)
        if (chosen === visitDownloadButton) {
            vscode.env.openExternal(netSdkDownloadUri);
        }
    }

    const { options } = await createAdaptorCommand(context);
    const vstuPath = options.env.CONF_VSTU_PATH;

    if (!vstuPath) {
        return;
    }

    options.cwd += "/VstuBridgeDebugAdapter"
    options.env = {...process.env, ...(options.env || {})}
    try {
        const { stdout, stderr } = await util.promisify(exec)("dotnet build", options)
        output(stdout.trim())
        output(stderr.trim())
    } catch (e) {
        output(e.stdout.trim())
        output(e.stderr.trim())
    }

    vscode.window.showErrorMessage("unknown error. Please check the 'VSTU Unity Debugger' output.");
}

/**
 * @param {vscode.ExtensionContext} context
 */
function activate(context) {

    // https://github.com/microsoft/vscode-mock-debug/blob/63e33ea07d9769ae9605212a13e96796333a61fd/src/activateMockDebug.ts#L155
    const provider = new DebugConfigurationProvider();
    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider('vstu-unity', provider));

    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider('vstu-unity', {
        /**
         * 
         * @param {vscode.WorkspaceFolder} _folder 
         * @returns {vscode.DebugConfiguration[]}
         */
        provideDebugConfigurations(_folder) {
            return [
                {
                    name: "Unity Editor (VSTU)",
                    type: "vstu-unity",
                    request: "attach",
                    projectPath: "${workspaceFolder}",
                },
            ];
        }
    }, vscode.DebugConfigurationProviderTriggerKind.Dynamic));

    const factory = new DebugAdapterFactory(context);
    context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory('vstu-unity', factory));

    context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory('vstu-unity', {
        /**
         * @param {vscode.DebugSession} session
        */
        createDebugAdapterTracker(session) {
            let startAt = 0;
            return {
                onWillStartSession() {
                    output(`onWillStartSession: ${session.id}`)
                    startAt = Date.now()
                },
                onWillReceiveMessage(_message) {
                },
                onDidSendMessage(_message) {
                    startAt = 0
                },
                onWillStopSession() {
                },
                onError(error) {
                    output(`onError: ${error}`)
                },
                onExit(code, signal) {
                    output(`onExit: code:${code}, signal:${signal}`)
                    const elapsed = Date.now() - startAt;
                    output(`elapsed: ${elapsed}`)
                    if (code != 0 && elapsed < 60000) {
                        // maybe, server launch failed
                        checkRuntime(context);
                    }
                },
            };
        }
    }));
}

// This method is called when your extension is deactivated
function deactivate() { }

module.exports = {
    activate,
    deactivate
}
