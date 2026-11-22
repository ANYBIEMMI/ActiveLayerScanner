using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace ActiveLayerScanner
{
    [Export(typeof(IXrmToolBoxPlugin)),
        ExportMetadata("Name", "Active Layer Scanner"),
        ExportMetadata("Description", "Scan a solution for components with an unmanaged Active layer, review the diff and remove active customizations in bulk."),
        ExportMetadata("SmallImageBase64", Icons.Small),
        ExportMetadata("BigImageBase64", Icons.Big),
        ExportMetadata("BackgroundColor", "Lavender"),
        ExportMetadata("PrimaryFontColor", "Black"),
        ExportMetadata("SecondaryFontColor", "DarkGray")]
    public class ActiveLayerScannerPlugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl() => new ActiveLayerScannerControl();
    }
}
