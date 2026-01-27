using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;

namespace VoxTether.ViewModels;

/// <summary>
/// ViewModel for models page.
/// </summary>
public partial class ModelsViewModel : ObservableObject
{
    public ObservableCollection<ModelItemViewModel> Models { get; } = new();

    private string _currentModel;

    public ModelsViewModel(VoxTetherSettings settings)
    {
        _currentModel = settings.ModelName;
    }

    public void UpdateModels(IReadOnlyList<ModelInfo> models, string currentModel)
    {
        _currentModel = currentModel;
        Models.Clear();
        
        foreach (var model in models)
        {
            Models.Add(new ModelItemViewModel(model, model.Name == currentModel));
        }
    }
}

/// <summary>
/// ViewModel for a single model item.
/// </summary>
public partial class ModelItemViewModel : ObservableObject
{
    public string Name { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int SizeMb { get; }
    
    [ObservableProperty]
    private bool _downloaded;
    
    [ObservableProperty]
    private bool _isActive;
    
    [ObservableProperty]
    private bool _isDownloading;
    
    [ObservableProperty]
    private bool _isActionEnabled = true;
    
    [ObservableProperty]
    private double _downloadProgress;

    public string SizeText => $"{SizeMb} MB";
    
    public string ActionText => Downloaded ? (IsActive ? "Active" : "Load") : "Download";
    
    public string StatusIcon => Downloaded 
        ? (IsActive ? "\uE73E" : "\uE8FB") // Checkmark or Download complete
        : "\uE896"; // Download
    
    public SolidColorBrush StatusColor => Downloaded
        ? (IsActive ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Gray))
        : new SolidColorBrush(Colors.Gray);

    public ModelItemViewModel(ModelInfo model, bool isActive)
    {
        Name = model.Name;
        DisplayName = model.DisplayName;
        Description = model.Description;
        SizeMb = model.SizeMb;
        Downloaded = model.Downloaded;
        IsActive = isActive;
        IsActionEnabled = !isActive; // Can't click if already active
    }
}
