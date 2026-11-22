# Active Layer Scanner (XrmToolBox)

Scans a Dataverse solution and lists every component that has an unmanaged **Active** layer on top of other layers, shows a diff of what was changed manually, and removes the active customizations in bulk.

## Usage
1. Connect to an environment, click **Load solutions**, pick a solution and click **Scan**.
2. Select a row to see the base layer vs. Active layer diff (green = added, red = removed).
3. Tick the components to clean and click **Remove active layer (selected)**. This calls `RemoveActiveCustomizations` and is **irreversible**; the tool asks for confirmation and offers a CSV backup first.

## Requirements
Read access to `msdyn_componentlayer` / `msdyn_solutioncomponentsummary`; System Customizer (or higher) to remove layers.

## Build
`dotnet build -c Release`, then copy `ActiveLayerScanner.dll` to `%APPDATA%\MscrmTools\XrmToolBox\Plugins`.
Pack with `nuget pack ActiveLayerScanner.nuspec`.

## License
MIT
