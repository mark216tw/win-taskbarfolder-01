using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.ViewModels;

public partial class FlyoutViewModel : ObservableObject
{
    private readonly LaunchService _launch;
    private readonly IconService _icon;
    private readonly ILogger<FlyoutViewModel> _logger;

    public FlyoutViewModel(
        AppGroup group,
        LaunchService launch,
        IconService icon,
        ILogger<FlyoutViewModel> logger)
    {
        GroupName = group.Name;
        _launch = launch;
        _icon = icon;
        _logger = logger;

        foreach (var item in group.Items)
        {
            Items.Add(new ItemViewModel(item));
        }

        HasItems = Items.Count > 0;
    }

    public string GroupName { get; }

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool hasItems;

    public event EventHandler? CloseRequested;

    public event EventHandler<string>? LaunchFailed;

    public async Task LoadIconsAsync()
    {
        await Task.WhenAll(Items.Select(LoadIconAsync));
    }

    private async Task LoadIconAsync(ItemViewModel item)
    {
        try
        {
            item.IconSource = await _icon.GetIconAsync(item.ToModel(), 40);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load flyout icon for {Name}", item.Name);
        }
    }

    public void Launch(ItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            _launch.Launch(item.ToModel());
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            LaunchFailed?.Invoke(this, Strings.Format("ErrorLaunch", item.Name, ex.Message));
        }
    }
}
