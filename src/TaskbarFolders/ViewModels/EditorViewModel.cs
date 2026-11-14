using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mark.TaskbarFolders.Helpers;
using Mark.TaskbarFolders.Models;
using Mark.TaskbarFolders.Resources;
using Mark.TaskbarFolders.Services;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.ViewModels;

public partial class EditorViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly DiscoveryService _discovery;
    private readonly IconService _icon;
    private readonly ShortcutService _shortcut;
    private readonly GroupTransferService _transfer;
    private readonly IDialogService _dialogs;
    private readonly ThemeService _theme;
    private readonly ILogger<EditorViewModel> _logger;
    private readonly DispatcherTimer _statusTimer;
    private AppSettings _settings = new();
    private bool _suppressTheme;
    private bool _transferring;

    public EditorViewModel(
        ConfigService config,
        DiscoveryService discovery,
        IconService icon,
        ShortcutService shortcut,
        GroupTransferService transfer,
        IDialogService dialogs,
        ThemeService theme,
        ILogger<EditorViewModel> logger)
    {
        _config = config;
        _discovery = discovery;
        _icon = icon;
        _shortcut = shortcut;
        _transfer = transfer;
        _dialogs = dialogs;
        _theme = theme;
        _logger = logger;

        foreach (var iconTheme in BuiltInIconCatalog.Themes)
        {
            var options = BuiltInIconCatalog.All
                .Where(definition => string.Equals(
                    definition.ThemeId,
                    iconTheme.Id,
                    StringComparison.OrdinalIgnoreCase))
                .Select(definition => new BuiltInIconOption(definition))
                .ToList();

            foreach (var option in options)
            {
                BuiltInIcons.Add(option);
            }

            IconThemeGroups.Add(new BuiltInIconThemeGroup(iconTheme, options));
        }

        ThemeColorOptions.Add(new GroupThemeOption(
            GroupThemeCatalog.NoneId,
            Strings.Get("ThemeColorNone"),
            null,
            isNone: true));

        foreach (var definition in GroupThemeCatalog.All)
        {
            ThemeColorOptions.Add(new GroupThemeOption(
                definition.Id,
                Strings.Get(definition.NameResourceKey),
                GroupThemeCatalog.CreateSwatch(definition.Id)));
        }

        _statusTimer = new DispatcherTimer();
        _statusTimer.Tick += OnStatusTimerTick;
        ShowStatus(Strings.Get("StatusReady"), StatusSeverity.Info);
    }

    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    public ObservableCollection<BuiltInIconOption> BuiltInIcons { get; } = [];

    public ObservableCollection<BuiltInIconThemeGroup> IconThemeGroups { get; } = [];

    public ObservableCollection<GroupThemeOption> ThemeColorOptions { get; } = [];

    public string AppVersion => AboutInfo.Version;

    public string AppFileVersion => AboutInfo.FileVersion;

    public string AppCopyright => AboutInfo.Copyright;

    [ObservableProperty]
    private GroupViewModel? selectedGroup;

    [ObservableProperty]
    private ItemViewModel? selectedItem;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private StatusSeverity statusKind = StatusSeverity.Info;

    [ObservableProperty]
    private bool isStatusVisible;

    [ObservableProperty]
    private string themeMode = "System";

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private bool hasUnsavedSettingsChanges;

    [ObservableProperty]
    private bool hasMissingIcons;

    [ObservableProperty]
    private bool isIconPickerOpen;

    [ObservableProperty]
    private bool isThemePickerOpen;

    public Task InitializeAsync(AppSettings settings)
    {
        _settings = settings;

        _suppressTheme = true;
        ThemeMode = NormalizeTheme(settings.Theme);
        _suppressTheme = false;
        settings.Theme = ThemeMode;
        _theme.Apply(ThemeMode);

        var requiresSave = false;
        Groups.Clear();
        foreach (var group in settings.Groups.OrderBy(group => group.Order))
        {
            var viewModel = new GroupViewModel(group);
            var normalizedIcon = BuiltInIconCatalog.NormalizeReference(viewModel.IconPath);
            if (!string.Equals(normalizedIcon, viewModel.IconPath, StringComparison.OrdinalIgnoreCase))
            {
                viewModel.IconPath = normalizedIcon;
                requiresSave = true;
            }

            AttachGroup(viewModel);
            Groups.Add(viewModel);
        }

        if (Groups.Count == 0)
        {
            var viewModel = new GroupViewModel(new AppGroup
            {
                Name = Strings.Get("DefaultGroupName"),
                IconPath = BuiltInIconCatalog.DefaultIconReference
            });
            AttachGroup(viewModel);
            Groups.Add(viewModel);
            requiresSave = true;
        }

        HasUnsavedChanges = requiresSave;
        SelectedGroup = Groups[0];
        UpdateValidation();
        SyncBuiltInIconSelection();
        SyncThemeSelection();
        return Task.CompletedTask;
    }

    partial void OnThemeModeChanged(string value)
    {
        if (!_suppressTheme)
        {
            _theme.Apply(value);
            HasUnsavedSettingsChanges = !string.Equals(
                NormalizeTheme(value),
                NormalizeTheme(_settings.Theme),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public void RevertThemeChanges()
    {
        if (!HasUnsavedSettingsChanges)
        {
            return;
        }

        var persisted = NormalizeTheme(_settings.Theme);
        _suppressTheme = true;
        ThemeMode = persisted;
        _suppressTheme = false;
        _theme.Apply(persisted);
        HasUnsavedSettingsChanges = false;
    }

    partial void OnHasUnsavedChangesChanged(bool value) =>
        SaveCommand.NotifyCanExecuteChanged();

    private bool CanSave() => HasUnsavedChanges;

    partial void OnSelectedGroupChanged(GroupViewModel? value)
    {
        SelectedItem = null;
        IsIconPickerOpen = false;
        IsThemePickerOpen = false;
        SyncBuiltInIconSelection();
        SyncThemeSelection();
    }

    [RelayCommand]
    private void OpenIconPicker() => IsIconPickerOpen = true;

    [RelayCommand]
    private void OpenThemePicker() => IsThemePickerOpen = true;

    [RelayCommand]
    private void SelectThemeColor(GroupThemeOption? option)
    {
        if (SelectedGroup is null || option is null)
        {
            return;
        }

        SelectedGroup.ThemeColor = option.Id;
        IsThemePickerOpen = false;
    }

    [RelayCommand]
    private void AddGroup()
    {
        var group = new GroupViewModel(new AppGroup
        {
            Name = Strings.Get("NewGroupName"),
            IconPath = BuiltInIconCatalog.DefaultIconReference
        });
        AttachGroup(group);
        Groups.Add(group);
        SelectedGroup = group;
        HasUnsavedChanges = true;
        UpdateValidation();
        _ = LoadGroupIconAsync(group);
    }

    [RelayCommand]
    private void DeleteGroup()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        if (!_dialogs.Confirm(Strings.Format("ConfirmDeleteGroup", SelectedGroup.Name)))
        {
            return;
        }

        var index = Groups.IndexOf(SelectedGroup);
        DetachGroup(SelectedGroup);
        Groups.Remove(SelectedGroup);
        SelectedGroup = Groups.Count == 0 ? null : Groups[Math.Clamp(index, 0, Groups.Count - 1)];
        HasUnsavedChanges = true;
        UpdateValidation();
    }

    public void MoveGroupTo(GroupViewModel group, int targetIndex)
    {
        var oldIndex = Groups.IndexOf(group);
        if (oldIndex < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, Groups.Count - 1);
        if (targetIndex == oldIndex)
        {
            return;
        }

        Groups.Move(oldIndex, targetIndex);
        SelectedGroup = group;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private async Task AddFileAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        var path = _dialogs.PickFile(
            Strings.Get("AddFile"),
            Strings.Get("AddFileFilter"));

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var extension = Path.GetExtension(path);
        var kind = extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
            ? AppItemKind.Url
            : extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                ? AppItemKind.Executable
                : AppItemKind.Shortcut;

        var item = new AppItem
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Target = path,
            Kind = kind,
            IconPath = path,
            WorkingDirectory = Path.GetDirectoryName(path)
        };

        var viewModel = new ItemViewModel(item);
        SelectedGroup.Items.Add(viewModel);
        SelectedItem = viewModel;
        await LoadItemIconAsync(viewModel);
    }

    [RelayCommand]
    private async Task AddUrlAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        var input = _dialogs.Prompt(Strings.Get("UrlPromptTitle"), Strings.Get("UrlPromptLabel"), "https://");
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        input = input.Trim();
        if (!input.Contains("://", StringComparison.Ordinal))
        {
            input = "https://" + input;
        }

        var name = Uri.TryCreate(input, UriKind.Absolute, out var uri)
            ? uri.Host
            : input;

        var item = new AppItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? input : name,
            Target = input,
            Kind = AppItemKind.Url
        };

        var viewModel = new ItemViewModel(item);
        SelectedGroup.Items.Add(viewModel);
        SelectedItem = viewModel;
        await LoadItemIconAsync(viewModel);
    }

    [RelayCommand]
    private async Task ImportStartMenuAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        ShowStatus(Strings.Get("StatusScanning"), StatusSeverity.Info);

        try
        {
            var apps = await _discovery.DiscoverAsync();
            var selected = _dialogs.PickApps(apps);
            var existing = SelectedGroup.Items
                .Select(item => BuildKey(item.Kind, item.Target, item.Arguments, item.Aumid))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var added = new List<ItemViewModel>();
            foreach (var app in selected)
            {
                var key = app.DeduplicationKey;
                if (!existing.Add(key))
                {
                    continue;
                }

                var viewModel = new ItemViewModel(app.ToAppItem());
                SelectedGroup.Items.Add(viewModel);
                added.Add(viewModel);
            }

            await Task.WhenAll(added.Select(LoadItemIconAsync));
            ShowStatus(Strings.Format("StatusImported", added.Count), StatusSeverity.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import start menu apps.");
            ShowStatus(Strings.Format("ErrorImport", ex.Message), StatusSeverity.Error);
            _dialogs.ShowError(StatusMessage);
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (SelectedGroup is null || SelectedItem is null)
        {
            return;
        }

        var index = SelectedGroup.Items.IndexOf(SelectedItem);
        SelectedGroup.Items.Remove(SelectedItem);
        SelectedItem = SelectedGroup.Items.Count == 0
            ? null
            : SelectedGroup.Items[Math.Clamp(index, 0, SelectedGroup.Items.Count - 1)];
    }

    [RelayCommand]
    private void MoveItemUp() => MoveItem(-1);

    [RelayCommand]
    private void MoveItemDown() => MoveItem(1);

    [RelayCommand]
    private void RenameItem()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var name = _dialogs.Prompt(Strings.Get("RenamePromptTitle"), Strings.Get("RenamePromptLabel"), SelectedItem.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            SelectedItem.Name = name.Trim();
        }
    }

    [RelayCommand]
    private async Task SelectBuiltInIconAsync(BuiltInIconOption? option)
    {
        if (SelectedGroup is null || option is null)
        {
            return;
        }

        SelectedGroup.IconPath = BuiltInIconCatalog.CreateReference(option.Id);
        IsIconPickerOpen = false;
        await LoadGroupIconAsync(SelectedGroup);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        await SaveCoreAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new AppSettings
            {
                Version = _settings.Version,
                Theme = NormalizeTheme(ThemeMode),
                Groups = _settings.Groups
            };

            await _config.SaveAsync(settings);

            _settings = settings;
            HasUnsavedSettingsChanges = false;
            ShowStatus(Strings.Get("SettingsSaved"), StatusSeverity.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings.");
            ShowStatus(Strings.Format("ErrorSave", ex.Message), StatusSeverity.Error);
            _dialogs.ShowError(StatusMessage);
        }
    }

    [RelayCommand]
    private async Task GenerateShortcutAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        if (!await SaveCoreAsync())
        {
            return;
        }

        try
        {
            var index = Groups.IndexOf(SelectedGroup);
            var models = Groups.Select((group, order) => group.ToModel(order)).ToList();
            var model = models[index];
            var path = _shortcut.GetShortcutPath(model, models);

            if (!File.Exists(path))
            {
                path = await _shortcut.CreateShortcutAsync(model, models);
            }

            ShowStatus(Strings.Format("StatusShortcutCreated", path), StatusSeverity.Success);
            _dialogs.ShowPinGuide(SelectedGroup.Name, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create pin shortcut.");
            ShowStatus(Strings.Format("ErrorShortcut", ex.Message), StatusSeverity.Error);
            _dialogs.ShowError(StatusMessage);
        }
    }

    [RelayCommand]
    private async Task ExportGroupsAsync()
    {
        if (_transferring)
        {
            return;
        }

        var path = _dialogs.PickSaveFile(
            Strings.Get("ExportGroupsTitle"),
            Strings.Get("GroupsFileFilter"),
            $"TaskbarFolders-groups-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _transferring = true;
        try
        {
            var groups = Groups
                .Select((group, index) => group.ToModel(index))
                .ToList();

            await _transfer.ExportAsync(path, groups);
            _dialogs.ShowMessage(Strings.Format("GroupsExportedMessage", groups.Count, path));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export groups.");
            _dialogs.ShowError(Strings.Format("ErrorExportGroups", ex.Message));
        }
        finally
        {
            _transferring = false;
        }
    }

    [RelayCommand]
    private async Task ImportGroupsAsync()
    {
        if (_transferring)
        {
            return;
        }

        var path = _dialogs.PickFile(
            Strings.Get("ImportGroupsTitle"),
            Strings.Get("GroupsFileFilter"));

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _transferring = true;
        try
        {
            IReadOnlyList<AppGroup> imported;
            try
            {
                imported = await _transfer.ImportAsync(path);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(ex, "Rejected invalid group export file {Path}", path);
                _dialogs.ShowError(Strings.Get("ErrorImportGroupsInvalid"));
                return;
            }

            if (!_dialogs.Confirm(Strings.Format("ConfirmImportGroups", imported.Count)))
            {
                return;
            }

            ApplyImportedGroups(imported);

            if (!await SaveCoreAsync())
            {
                return;
            }

            _ = LoadAllIconsAsync();
            _dialogs.ShowMessage(Strings.Format("GroupsImportedMessage", imported.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import groups.");
            _dialogs.ShowError(Strings.Format("ErrorImportGroups", ex.Message));
        }
        finally
        {
            _transferring = false;
        }
    }

    private void ApplyImportedGroups(IReadOnlyList<AppGroup> groups)
    {
        foreach (var group in Groups)
        {
            DetachGroup(group);
        }

        Groups.Clear();

        foreach (var model in groups)
        {
            var viewModel = new GroupViewModel(model);
            var normalizedIcon = BuiltInIconCatalog.NormalizeReference(viewModel.IconPath);
            if (!string.Equals(normalizedIcon, viewModel.IconPath, StringComparison.OrdinalIgnoreCase))
            {
                viewModel.IconPath = normalizedIcon;
            }

            AttachGroup(viewModel);
            Groups.Add(viewModel);
        }

        SelectedGroup = Groups.Count == 0 ? null : Groups[0];
        HasUnsavedChanges = true;
        UpdateValidation();
        SyncBuiltInIconSelection();
    }

    private async Task<bool> SaveCoreAsync()
    {
        try
        {
            ApplyDefaultIcons();

            var groups = Groups
                .Select((group, index) => group.ToModel(index))
                .ToList();

            var settings = new AppSettings
            {
                Version = _settings.Version,
                Theme = NormalizeTheme(_settings.Theme),
                Groups = groups
            };

            await _config.SaveAsync(settings);
            await _shortcut.SyncAllAsync(groups);

            _settings = settings;
            HasUnsavedChanges = false;
            ShowStatus(Strings.Get("StatusSaved"), StatusSeverity.Success);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration.");
            ShowStatus(Strings.Format("ErrorSave", ex.Message), StatusSeverity.Error);
            _dialogs.ShowError(StatusMessage);
            return false;
        }
    }

    private void MoveItem(int delta)
    {
        if (SelectedGroup is null || SelectedItem is null)
        {
            return;
        }

        var index = SelectedGroup.Items.IndexOf(SelectedItem);
        var target = index + delta;
        if (target < 0 || target >= SelectedGroup.Items.Count)
        {
            return;
        }

        SelectedGroup.Items.Move(index, target);
        SelectedItem = SelectedGroup.Items[target];
    }

    public async Task LoadAllIconsAsync()
    {
        foreach (var group in Groups)
        {
            await LoadGroupIconAsync(group);
        }

        var items = Groups
            .OrderByDescending(group => ReferenceEquals(group, SelectedGroup))
            .SelectMany(group => group.Items)
            .ToList();
        await Task.WhenAll(items.Select(LoadItemIconAsync));
    }

    private Task LoadGroupIconAsync(GroupViewModel group)
    {
        var reference = BuiltInIconCatalog.NormalizeReference(group.IconPath);
        var definition = BuiltInIconCatalog.Find(reference);

        group.IconSource = definition is null
            ? null
            : BuiltInIconCatalog.CreateImage(
                definition.Id,
                GroupThemeCatalog.ResolveIconBackground(group.ThemeColor));
        return Task.CompletedTask;
    }

    private async Task LoadItemIconAsync(ItemViewModel item)
    {
        try
        {
            item.IconSource = await _icon.GetIconAsync(item.ToModel(), 32);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load icon for {Name}", item.Name);
        }
    }

    private void AttachGroup(GroupViewModel group)
    {
        group.PropertyChanged += OnGroupPropertyChanged;
        group.Items.CollectionChanged += OnItemsCollectionChanged;
        foreach (var item in group.Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void DetachGroup(GroupViewModel group)
    {
        group.PropertyChanged -= OnGroupPropertyChanged;
        group.Items.CollectionChanged -= OnItemsCollectionChanged;
        foreach (var item in group.Items)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GroupViewModel.Name) or nameof(GroupViewModel.IconPath) or nameof(GroupViewModel.ThemeColor))
        {
            HasUnsavedChanges = true;
        }

        if (e.PropertyName is nameof(GroupViewModel.IconPath))
        {
            SyncBuiltInIconSelection();
        }

        if ((e.PropertyName is nameof(GroupViewModel.IconPath) or nameof(GroupViewModel.ThemeColor))
            && sender is GroupViewModel group)
        {
            _ = LoadGroupIconAsync(group);
        }

        if (e.PropertyName is nameof(GroupViewModel.ThemeColor))
        {
            SyncThemeSelection();
        }

        if (e.PropertyName is nameof(GroupViewModel.Name) or nameof(GroupViewModel.IconPath))
        {
            UpdateValidation();
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItemViewModel.Name))
        {
            HasUnsavedChanges = true;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ItemViewModel>())
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ItemViewModel>())
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        HasUnsavedChanges = true;
    }

    private void UpdateValidation()
    {
        HasMissingIcons = Groups.Count == 0 || Groups.Any(group => !BuiltInIconCatalog.IsKnown(group.IconPath));
    }

    private void ApplyDefaultIcons()
    {
        foreach (var group in Groups.Where(group => !BuiltInIconCatalog.IsKnown(group.IconPath)))
        {
            group.IconPath = BuiltInIconCatalog.DefaultIconReference;
        }
    }

    private void SyncBuiltInIconSelection()
    {
        var current = SelectedGroup?.IconPath;
        var selectedReference = BuiltInIconCatalog.IsKnown(current) ? current : null;

        foreach (var option in BuiltInIcons)
        {
            option.IsSelected = string.Equals(
                selectedReference,
                BuiltInIconCatalog.CreateReference(option.Id),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SyncThemeSelection()
    {
        var current = GroupThemeCatalog.Normalize(SelectedGroup?.ThemeColor);

        foreach (var option in ThemeColorOptions)
        {
            option.IsSelected = string.Equals(
                current,
                option.Id,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ShowStatus(string message, StatusSeverity severity)
    {
        StatusMessage = message;
        StatusKind = severity;
        IsStatusVisible = true;

        _statusTimer.Stop();
        if (severity == StatusSeverity.Error)
        {
            return;
        }

        _statusTimer.Interval = severity == StatusSeverity.Success
            ? TimeSpan.FromSeconds(4)
            : TimeSpan.FromSeconds(2.5);
        _statusTimer.Start();
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        _statusTimer.Stop();
        IsStatusVisible = false;
    }

    private static string NormalizeTheme(string? theme) =>
        string.IsNullOrWhiteSpace(theme) ? "System" : theme;

    private static string BuildKey(AppItemKind kind, string target, string? arguments, string? aumid) =>
        kind == AppItemKind.Uwp && !string.IsNullOrWhiteSpace(aumid)
            ? $"uwp:{aumid}"
            : $"path:{target.ToLowerInvariant()}|{arguments}";
}
