using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.ViewModels;

public partial class FlyoutViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly LaunchService _launch;
    private readonly IconService _icon;
    private readonly ILogger<FlyoutViewModel> _logger;
    private readonly string _groupId;

    public FlyoutViewModel(
        AppGroup group,
        ConfigService config,
        LaunchService launch,
        IconService icon,
        ILogger<FlyoutViewModel> logger)
    {
        GroupName = group.Name;
        _config = config;
        _launch = launch;
        _icon = icon;
        _logger = logger;
        _groupId = group.Id;

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

    public async Task<bool> RenameItemAsync(ItemViewModel item, string newName)
    {
        var trimmed = newName.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, item.Name, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            if (!await _config.RenameItemAsync(_groupId, item.Id, trimmed))
            {
                return false;
            }

            item.Name = trimmed;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename flyout item {Name}", item.Name);
            return false;
        }
    }

    public async Task<bool> MoveItemAsync(ItemViewModel item, int targetIndex)
    {
        var oldIndex = Items.IndexOf(item);
        if (oldIndex < 0)
        {
            return false;
        }

        var clamped = Math.Clamp(targetIndex, 0, Items.Count - 1);
        if (clamped == oldIndex)
        {
            return true;
        }

        Items.Move(oldIndex, clamped);
        var orderedIds = Items.Select(candidate => candidate.Id).ToArray();

        try
        {
            if (await _config.ReorderItemsAsync(_groupId, orderedIds))
            {
                return true;
            }

            if (Items.IndexOf(item) == clamped)
            {
                Items.Move(clamped, oldIndex);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder flyout item {Name}", item.Name);
            if (Items.IndexOf(item) == clamped)
            {
                Items.Move(clamped, oldIndex);
            }

            return false;
        }
    }
}
