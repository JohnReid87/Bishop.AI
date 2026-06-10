using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.ViewModels.Shared;

public sealed partial class AddWorkspaceDialogViewModel : ObservableObject
{
    private readonly Func<string, bool> _directoryExists;

    public AddWorkspaceDialogViewModel() : this(Directory.Exists) { }

    public AddWorkspaceDialogViewModel(Func<string, bool> directoryExists)
    {
        _directoryExists = directoryExists;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(EffectivePath))]
    [NotifyPropertyChangedFor(nameof(CollisionError))]
    [NotifyPropertyChangedFor(nameof(HasCollision))]
    public partial string ParentFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(EffectivePath))]
    [NotifyPropertyChangedFor(nameof(CollisionError))]
    [NotifyPropertyChangedFor(nameof(HasCollision))]
    public partial string NewFolderName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(EffectivePath))]
    [NotifyPropertyChangedFor(nameof(CollisionError))]
    [NotifyPropertyChangedFor(nameof(HasCollision))]
    public partial bool IsPickExisting { get; set; } = true;

    public string EffectivePath =>
        IsPickExisting
            ? FolderPath
            : (string.IsNullOrWhiteSpace(ParentFolderPath) || string.IsNullOrWhiteSpace(NewFolderName)
                ? string.Empty
                : Path.Combine(ParentFolderPath, NewFolderName));

    public bool HasCollision =>
        !IsPickExisting
        && !string.IsNullOrWhiteSpace(ParentFolderPath)
        && !string.IsNullOrWhiteSpace(NewFolderName)
        && _directoryExists(EffectivePath);

    public string? CollisionError =>
        HasCollision ? $"A folder named '{NewFolderName}' already exists at this location." : null;

    public bool CanConfirm
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return false;
            return IsPickExisting
                ? !string.IsNullOrWhiteSpace(FolderPath)
                : !string.IsNullOrWhiteSpace(ParentFolderPath)
                    && !string.IsNullOrWhiteSpace(NewFolderName)
                    && !HasCollision;
        }
    }
}
