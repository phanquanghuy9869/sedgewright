using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Full-page editor for agent definitions.
/// </summary>
public partial class AgentDefinitionEditorView : UserControl
{
    private AgentDefinitionEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the editor page.
    /// </summary>
    public AgentDefinitionEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        _viewModel = DataContext as AgentDefinitionEditorViewModel;
        SkillsSection.SkillDiscoveryService = _viewModel?.SkillDiscoveryService;
    }
}
