# Unofficial VSTU Unity Debugger for VSCode

This project is highly experimental and still under development.

The extension works as a Unity debugger based on [Visual Studio Tools for Unity (VSTU)](https://learn.microsoft.com/visualstudio/gamedev/unity/get-started/using-visual-studio-tools-for-unity).

## Features

* Expression Conditional Breakpoints
* Hit Count Conditional Breakpoints
    * '{hitCount}' or '{operator} {hitCount}'
    * Available Operators: =, >, >=, %

## Requirements

* .Net 7.0 SDK
* Visual Studio Tools for Unity

## Extension Settings

* `vstu-debugger.vstuPath`: The path to 'Visual Studio Tools for Unity' folder. (Default:'auto')
* `vstu-debugger.targetFramework`: The TargetFramework to use when running the debug adapter. (Default:'auto')

## License

To use VSTU, you must accept [MICROSOFT SOFTWARE LICENSE TERMS](https://visualstudio.microsoft.com/license-terms/).

The code for this project itself is licensed under MIT.
