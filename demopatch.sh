#!/bin/sh
mkdir -p blazor/Pages
cat >blazor/Pages/Index.razor <<EOF
@page "/"
@inject IJSRuntime JSRuntime

<FileSession @ref="fileSessionElement"/>
<Terminal @ref="terminalElement"/>

$(awk '/^using / {print "@" substr($0, 1, length($0)-1)}' getinfo-csharp/*.cs | sort | uniq)

@code {
	private static Terminal terminalElement;
	private static FileSession fileSessionElement;

$(grep -hE '^\s.*' getinfo-csharp/*.cs |
	sed 's/using the same URL as previous run/an override.csv file is provided/g' |
	sed 's/Console.Write/terminalElement.TerminalOutWrite/g' |
	sed 's/^\(\s*\)Console.ReadLine();/\1terminalElement.TerminalOutWriteLine("PROGRAM TERMINATED");/g' |
	sed 's/Console.ReadLine();/await terminalElement.WaitTerminalIn();/g' |
	sed 's/static async Task Main(string\[\] args)/public static async Task PMain(string\[\] args)/' |
	sed -E 's/new Stream(Writer|Reader)/fileSessionElement.Get\1/g' |
	sed -E 's/Stream(Writer|Reader)/String\1/g' |
	sed 's/!stream.EndOfStream/stream.Peek() != -1/g' |
	sed 's/File.Exists/fileSessionElement.ContainsFile/g' |
	sed 's|xmlUrl == "") xmlUrl =|xmlUrl == "https://cors-anywhere.herokuapp.com/") xmlUrl +=|' |
	sed 's|string serviceUrl = |\0"https://cors-anywhere.herokuapp.com/" + |' |
	sed 's|"amend"|"https://cors-anywhere.herokuapp.com/amend"|' |
	sed 's/Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)/string.Empty/g')

	protected override async Task OnAfterRenderAsync(bool firstRender) {
		if (!firstRender) return;
		await fileSessionElement.Import("getinfo-csharp");
		try {
			await Program.PMain(new string[]{});
			await fileSessionElement.Export("hk-service-map");
			await JSRuntime.InvokeVoidAsync("toggleFileOutput", true, "hk-service-map");
		} catch (Exception err) {
			terminalElement.TerminalOutWriteLine(err.ToString());
			await JSRuntime.InvokeVoidAsync("showError");
		}
	}
}
EOF
cd blazor
dotnet publish -c release
cp -r bin/release/netstandard2.1/publish/wwwroot ../public/blazor
