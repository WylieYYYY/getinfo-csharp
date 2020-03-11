# Getinfo CSharp
Getinfo CSharp is a supplement for `getinfo.py` in [HK Service Map](https://gitlab.com/WylieYYYY/hk-service-map). Use this when the target device for generating `unitinfo.js` is not permitted to install Python interpreter for the default `getinfo.py` due to administrative right.
### Features:
- Written in .NET Core 3.1, can be compiled for major platforms;
- Executable is compiled so no installation is required on target device for `unitinfo.js` generation;
- Using asynchronised batch request, has the same speed as `getinfo.py`;
- Has the same interface as `getinfo.py`;

### Setup
#### On any desktop device with administrative right:
1. Download the source code here.
2. Install [.NET Core SDK version 3.1](https://docs.microsoft.com/en-us/dotnet/core/install/sdk) (If Visual Studio 2019 version 16.4 or higher with .NET Core is installed, this step is not required)
3. Open terminal in repository directory containing `getinfo.csproj`
4. Execute `dotnet publish -c release -r [Platform RID] --self-contained` to compile ([RID catalog](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog), `--self-contained` is required for compiling tranferrable executable)
5. When compilation is finished successfully, subdirectory `getinfo-csharp/bin/release/netcoreapp3.1` should exist.
6. In `getinfo-csharp/bin/release/netcoreapp3.1`, move the directory `[Platform RID]/publish` to the root of `HK Service Map`
7. And now, the directory `publish` can be renamed to something meaningful, and the whole of `HK Service Map` can be transferred to the target device for generation.

#### On the target device for generation:
1. Check that no files in `publish` directory is at the root of `HK Service Map`
2. In the directory originally named `publish`, locate an executable named `getinfo` (with `.exe` if in Microsoft Windows)
3. Execute in terminal using `./getinfo` (`chmod +x` might be needed in Linux, double clicking the file also works in Microsoft Windows)
4. The same interface as `getinfo.py` will appear and is used the same way.

> Remove `publish` directory before starting a public server

### Remarks
`getinfo.py` and this repository's code is a bit unorganised and will be cleaned up later. Hope you don't mind for the time being. :)