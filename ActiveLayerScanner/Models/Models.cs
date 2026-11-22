using System;

namespace ActiveLayerScanner.Models
{
    public class SolutionItem
    {
        public Guid Id { get; set; }
        public string UniqueName { get; set; }
        public string FriendlyName { get; set; }
        public string Version { get; set; }
        public bool IsManaged { get; set; }
        public override string ToString() => $"{FriendlyName} ({UniqueName}) v{Version}";
    }

    public class SolutionComponentRef
    {
        public Guid ObjectId { get; set; }
        public int ComponentType { get; set; }
        /// <summary>Logical name used by msdyn_componentlayer / RemoveActiveCustomizations (e.g. "Entity", "SystemForm").</summary>
        public string SolutionComponentName { get; set; }
        public string DisplayName { get; set; }
    }

    public class ComponentRow : System.ComponentModel.INotifyPropertyChanged
    {
        private bool selected;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public bool Selected
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Selected)));
            }
        }
        public string ComponentType { get; set; }
        public string Name { get; set; }
        public string BaseLayer { get; set; }
        public int LayerCount { get; set; }
        public DateTime? ActiveModifiedOn { get; set; }
        public Guid ComponentId { get; set; }
        public string SolutionComponentName { get; set; }
        public string Changes { get; set; }
    }

    public class LayerInfo
    {
        public string SolutionName { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public string Publisher { get; set; }
        public DateTime? OverwriteTime { get; set; }
        public string Changes { get; set; }
        public string Json { get; set; }
        public bool IsActive => string.Equals(SolutionName, "Active", StringComparison.OrdinalIgnoreCase);
    }

    public class CleanupResult
    {
        public ComponentRow Row { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
