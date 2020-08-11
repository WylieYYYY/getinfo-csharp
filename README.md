# Getinfo CSharp
[![pipeline status](https://gitlab.com/WylieYYYY/getinfo-csharp/badges/master/pipeline.svg)](https://gitlab.com/WylieYYYY/getinfo-csharp/commits/master)  
Getinfo CSharp is a replacement for `getinfo.py` in [HK Service Map](https://gitlab.com/WylieYYYY/hk-service-map). Use this when the target device for generating `unitinfo.js` is not permitted to install interpreter for the default `getinfo.py` due to administrative right.
### Features:
- Written in .NET Core 3.1, can be compiled for major platforms;
- Executable is compiled so no installation is required on target device for `unitinfo.js` generation;
- Using asynchronised batch request, has the same speed as `getinfo.py`;
- Has the same interface as `getinfo.py`;
- Extra `amend` function for amending overrides to the map quickly;

### Setup
#### Using pre-built binaries for Windows directly on the target device for generation:
1. Download the zipped binaries from the [pipeline](https://gitlab.com/WylieYYYY/getinfo-csharp/-/jobs/artifacts/master/download?job=build-exe) when the pipeline status indicator above is green and show `passed`. If it is `running` or `failed`, go to the [pipeline list](https://gitlab.com/WylieYYYY/getinfo-csharp/pipelines) and search the first ticked pipeline from the top, click the download button at right-hand side and select `Download build artifacts` to download.
2. Extract `artifacts.zip`
3. Execute `HK Service Map/getinfo/getinfo-csharp.exe` by double clicking.
4. The same interface as `getinfo.py` will appear and is used the same way.

#### Alternatively, on any desktop device with administrative right:
1. Download the source code here.
2. Install [.NET Core SDK version 3.1](https://docs.microsoft.com/en-us/dotnet/core/install/sdk) (If Visual Studio 2019 version 16.4 or higher with .NET Core is installed, this step is not required)
3. Open terminal in repository directory containing `getinfo.csproj`
4. Execute `dotnet publish -c release -r [Platform RID] --self-contained` to compile ([RID catalog](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog), `--self-contained` is required for compiling tranferrable executable)
5. When compilation is finished successfully, subdirectory `getinfo-csharp/bin/release/netcoreapp3.1` should exist.
6. In `getinfo-csharp/bin/release/netcoreapp3.1/[Platform RID]`, move the directory `publish` to the root of `HK Service Map`
7. The whole of `HK Service Map` can be transferred to the target device for generation.
8. Check that no files in this repository is at the root of `HK Service Map`
9. In directory `publish`, locate an executable named `getinfo-csharp` (with `.exe` if in Microsoft Windows)
10. Execute in terminal using `./getinfo-csharp` (`chmod +x` might be needed in Linux, double clicking the file also works in Microsoft Windows)
11. The same interface as `getinfo.py` will appear and is used the same way.

> Remove `publish` or `getinfo` directory additionally before starting a public server

### Amend function
After generating `unitinfo.js`, if changes are made to the `override.csv`, `amend` function can help to apply changes quickly without requesting all geospatial information again.

Just enter `amend` when prompted to provide an URL, all entries in `override.csv` must already exist in `unitinfo.js` for `amend` to work.
