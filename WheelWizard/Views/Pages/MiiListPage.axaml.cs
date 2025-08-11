using System.IO.Abstractions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Testably.Abstractions;
using WheelWizard.CustomCharacters;
using WheelWizard.Helpers;
using WheelWizard.Resources.Languages;
using WheelWizard.Services;
using WheelWizard.Services.Settings;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Shared.MessageTranslations;
using WheelWizard.Views.Components;
using WheelWizard.Views.Popups.Generic;
using WheelWizard.Views.Popups.MiiManagement;
using WheelWizard.WiiManagement;
using WheelWizard.WiiManagement.MiiManagement;
using WheelWizard.WiiManagement.MiiManagement.Domain.Mii;

namespace WheelWizard.Views.Pages;

public partial class MiiListPage : UserControlBase
{
    [Inject]
    private ICustomCharactersService CustomCharactersService { get; set; } = null!;

    [Inject]
    private IMiiDbService MiiDbService { get; set; } = null!;

    [Inject]
    private IMiiRepositoryService MiiRepositoryService { get; set; } = null!;

    [Inject]
    private IFileSystem FileSystem { get; set; } = null!;

    [Inject]
    private IRandomSystem Random { get; set; } = null!;

    public MiiListPage()
    {
        InitializeComponent();
        DataContext = this;

        var miiDbExists = MiiDbService.Exists();
        if (!miiDbExists)
        {
            if (SettingsHelper.PathsSetupCorrectly())
            {
                var creationResult = MiiRepositoryService.ForceCreateDatabase();
                if (creationResult.IsFailure)
                {
                    MessageTranslationHelper.ShowMessage(creationResult.Error);
                    VisibleWhenNoDb.IsVisible = !miiDbExists;
                }
            }
            else
            {
                VisibleWhenDb.IsVisible = false;
                VisibleWhenNoDb.IsVisible = true;
            }
        }

        miiDbExists = MiiDbService.Exists();
        if (!miiDbExists)
            return;

        VisibleWhenDb.IsVisible = true;
        VisibleWhenNoDb.IsVisible = false;
        ReloadMiiList();
    }

    #region Multi and Single select

    private bool _isShiftPressed;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Subscribe to keyboard events to track Shift key state
        KeyDown += MiiListPage_KeyDown;
        KeyUp += MiiListPage_KeyUp;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Unsubscribe from keyboard events when control is detached
        KeyDown -= MiiListPage_KeyDown;
        KeyUp -= MiiListPage_KeyUp;

        base.OnDetachedFromVisualTree(e);
    }

    private void MiiListPage_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl))
            return;

        if (!_isShiftPressed)
            ChangeBlockSelectionType(true);
        _isShiftPressed = true;
    }

    private void MiiListPage_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl))
            return;

        if (_isShiftPressed)
            ChangeBlockSelectionType(false);
        _isShiftPressed = false;
    }

    private void ChangeBlockSelectionType(bool multiSelect)
    {
        foreach (var miiBlock in MiiList.Children.OfType<MiiBlock>())
        {
            var groupName = multiSelect ? Guid.NewGuid().ToString() : "MiiListSingleSelect";
            miiBlock.GroupName = groupName;
        }
    }

    #endregion

    private void ReloadMiiList()
    {
        var size = 90;
        var margin = new Thickness(8, 10);

        MiiList.Children.Clear();
        foreach (var mii in MiiDbService.GetAllMiis().OrderByDescending(m => m.IsFavorite).ToList())
        {
            var miiBlock = new MiiBlock
            {
                Mii = mii,
                Width = size,
                Height = size,
                Margin = margin,
            };
            miiBlock.Click += (_, _) => ChangeTopButtons();

            miiBlock.ContextMenu = new ContextMenu();
            var favHeader = mii.IsFavorite ? Common.Action_Unfavorite : Common.Action_Favorite;
            miiBlock.ContextMenu.Items.Add(
                new MenuItem { Header = favHeader, Command = new MyCommand(() => ContextAction(mii, ToggleFavorite)) }
            );
            miiBlock.ContextMenu.Items.Add(new MenuItem { Header = Common.Action_Edit, Command = new MyCommand(() => EditMii(mii)) });
            miiBlock.ContextMenu.Items.Add(
                new MenuItem { Header = "Duplicate", Command = new MyCommand(() => ContextAction(mii, DuplicateMii)) }
            );
            miiBlock.ContextMenu.Items.Add(
                new MenuItem { Header = Common.Action_Export, Command = new MyCommand(() => ContextAction(mii, ExportMultipleMiiFiles)) }
            );
            miiBlock.ContextMenu.Items.Add(
                new MenuItem { Header = Common.Action_Delete, Command = new MyCommand(() => ContextAction(mii, DeleteMii)) }
            );

            MiiList.Children.Add(miiBlock);
        }

        var count = MiiList.Children.Count;
        ListItemCount.Text = count.ToString();

        ChangeTopButtons();
        if (count >= 100)
            return;

        var addBlock = new MiiBlock
        {
            Width = size,
            Height = size,
            Margin = margin,
        };
        addBlock.Click += (_, _) =>
        {
            foreach (var miiBlock in MiiList.Children.OfType<MiiBlock>())
            {
                miiBlock.IsChecked = false;
            }

            ChangeTopButtons();
            CreateNewMii();
        };

        MiiList.Children.Add(addBlock);
    }

    private async void DeleteMii_OnClick(object? sender, RoutedEventArgs e) => DeleteMii(GetSelectedMiis());

    private async void EditMii_OnClick(object? sender, RoutedEventArgs e) => EditMii(GetSelectedMiis()[0]);

    private async void FavMii_OnClick(object? sender, RoutedEventArgs e) => ToggleFavorite(GetSelectedMiis());

    private async void ExportMii_OnClick(object? sender, RoutedEventArgs e) => ExportMultipleMiiFiles(GetSelectedMiis());

    private async void DuplicateMii_OnClick(object? sender, RoutedEventArgs e) => DuplicateMii(GetSelectedMiis());

    private async void ImportMii_OnClick(object? sender, RoutedEventArgs e)
    {
        var miiFiles = await FilePickerHelper.OpenFilePickerAsync(
            fileType: CustomFilePickerFileType.Miis,
            allowMultiple: true,
            title: "Select Mii file(s)"
        );
        if (miiFiles.Count == 0)
            return;
        foreach (var file in miiFiles)
        {
            FileSystem.File.Exists(file);
            //get raw bytes from file
            var stream = FileSystem.File.OpenRead(file);
            using var reader = new BinaryReader(stream);
            var miiData = reader.ReadBytes((int)stream.Length);
            stream.Close();
            var result = MiiSerializer.Deserialize(miiData);
            if (result.IsFailure)
            {
                ViewUtils.ShowSnackbar(
                    Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureDeserialize, result.Error.Message)!,
                    ViewUtils.SnackbarType.Danger
                );
                return;
            }

            var mii = result.Value;

            //We duplicate to make sure it does not actually have the original MiiId
            var macAddress = "02:11:11:11:11:11";
            var saveResult = MiiDbService.AddToDatabase(mii, macAddress);
            if (saveResult.IsFailure)
            {
                ViewUtils.ShowSnackbar(
                    Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureSave, saveResult.Error.Message)!,
                    ViewUtils.SnackbarType.Danger
                );
                return;
            }
        }

        ReloadMiiList();
    }

    private async void ToggleFavorite(Mii[] miis)
    {
        var allFavorite = miis.All(m => m.IsFavorite);

        foreach (var mii in miis)
        {
            mii.IsFavorite = !allFavorite;
            var result = MiiDbService.Update(mii);
            if (result.IsFailure)
            {
                ViewUtils.ShowSnackbar(
                    Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureUpdate, result.Error.Message)!,
                    ViewUtils.SnackbarType.Danger
                );
                return;
            }
        }

        ReloadMiiList();
    }

    private async void ExportMultipleMiiFiles(Mii[] miis)
    {
        if (miis.Length == 0)
        {
            ViewUtils.ShowSnackbar(Phrases.SnackbarWarning_NoMiiExport, ViewUtils.SnackbarType.Warning);
            return;
        }

        foreach (var mii in miis)
        {
            ExportMiiAsFile(mii);
        }
    }

    public static string ReplaceInvalidFileNameChars(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private async void ExportMiiAsFile(Mii mii)
    {
        var exportName = ReplaceInvalidFileNameChars(CustomCharactersService.NormalizeToAscii(mii.Name.ToString()));
        var diaglog = await FilePickerHelper.SaveFileAsync(
            title: "Save Mii as file",
            fileTypes: [CustomFilePickerFileType.Miis],
            defaultFileName: $"{exportName}"
        );
        if (diaglog == null)
            return;
        var result = MiiDbService.GetByAvatarId(mii.MiiId);
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(
                Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureGet, result.Error.Message)!,
                ViewUtils.SnackbarType.Danger
            );
            return;
        }

        var miiToExport = result.Value;
        var saveResult = SaveMiiToDisk(miiToExport, diaglog);
        if (saveResult.IsFailure)
        {
            ViewUtils.ShowSnackbar(
                Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureSave, saveResult.Error.Message)!,
                ViewUtils.SnackbarType.Danger
            );
            return;
        }

        ViewUtils.ShowSnackbar(
            Humanizer.ReplaceDynamic(Phrases.SnackbarSuccess_SavedMii, miiToExport.Name, diaglog) ?? "Saved Mii successfully"
        );
    }

    private OperationResult SaveMiiToDisk(Mii mii, string path)
    {
        var miiData = MiiSerializer.Serialize(mii);
        if (miiData.IsFailure)
        {
            ViewUtils.ShowSnackbar(
                Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureSerialize, miiData.Error.Message)!,
                ViewUtils.SnackbarType.Danger
            );
            return miiData;
        }

        var file = FileSystem.FileInfo.New(path);
        using var stream = file.Open(FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write(miiData.Value);
        writer.Flush();
        writer.Close();
        stream.Close();
        ViewUtils.ShowSnackbar(
            Humanizer.ReplaceDynamic(Phrases.SnackbarSuccess_SavedMii, mii.Name, file.FullName) ?? "Saved Mii successfully"
        );
        return Ok();
    }

    private async void DeleteMii(Mii[] miis)
    {
        if (miis.Length == 0)
        {
            ViewUtils.ShowSnackbar(Phrases.SnackbarWarning_NoMiiDelete, ViewUtils.SnackbarType.Warning);
            return;
        }

        // TODO: add a check that you cant remove a Mii that is in use by a licence,
        // I have no idea how tho

        if (miis.Any(mii => mii.IsFavorite))
        {
            await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Warning_CantDeleteFavMii);
            return;
        }

        var mainText = Humanizer.ReplaceDynamic(Phrases.Question_SureDelete_Title_Miis, miis.Length) ?? $"Delete {miis.Length}?";
        var successMessage = Humanizer.ReplaceDynamic(Phrases.SnackbarSuccess_Deleted_Miis, miis.Length) ?? $"Deleted {miis.Length}";
        if (miis.Length == 1)
        {
            mainText = Humanizer.ReplaceDynamic(Phrases.Question_SureDelete_Title, miis[0].Name) ?? $"Delete {miis[0].Name}?";
            successMessage = Humanizer.ReplaceDynamic(Phrases.SnackbarSuccess_Deleted, miis[0].Name) ?? $"Deleted {miis[0].Name}";
        }

        var result = await new YesNoWindow().SetMainText(mainText).SetExtraText(Phrases.Question_SureDelete_Extra).AwaitAnswer();
        if (!result)
            return;

        foreach (var mii in miis)
        {
            MiiDbService.Remove(mii.MiiId);
        }

        ReloadMiiList();
        ViewUtils.ShowSnackbar(successMessage);
    }

    private async void EditMii(Mii mii)
    {
        var window = new MiiEditorWindow().SetMii(mii);
        var save = await window.AwaitAnswer();
        if (!save)
            return;

        var result = MiiDbService.Update(window.Mii);
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(
                Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureUpdate, result.Error.Message)!,
                ViewUtils.SnackbarType.Danger
            );
            return;
        }

        ReloadMiiList();
    }

    private async void CreateNewMii()
    {
        Mii? mii = null;
        await new OptionsWindow()
            .AddOption("Dice", Common.Action_Randomize, () => mii = MiiFactory.CreateRandomMii(Random.Random.Shared))
            .AddOption("PersonMale", Common.Attribute_Mii_Gender_Male, () => mii = MiiFactory.CreateDefaultMale())
            .AddOption("PersonFemale", Common.Attribute_Mii_Gender_Female, () => mii = MiiFactory.CreateDefaultFemale())
            .AwaitAnswer();
        if (mii == null)
            return;

        var window = new MiiEditorWindow().SetMii(mii);
        var save = await window.AwaitAnswer();
        if (!save)
            return;

        var result = MiiDbService.AddToDatabase(window.Mii, (string)SettingsManager.MACADDRESS.Get());
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(
                Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureCreate, result.Error.Message)!,
                ViewUtils.SnackbarType.Danger
            );
            return;
        }

        ReloadMiiList();
    }

    private void DuplicateMii(Mii[] miis)
    {
        //assuming the mac address is already set correctly
        var macAddress = (string)SettingsManager.MACADDRESS.Get();
        foreach (var mii in miis)
        {
            var result = MiiDbService.AddToDatabase(mii, macAddress);
            if (!result.IsFailure)
                continue;

            ViewUtils.ShowSnackbar(
                Humanizer.ReplaceDynamic(Phrases.SnackbarError_MiiFailureDuplicate, result.Error.Message)!,
                ViewUtils.SnackbarType.Danger
            );
            return;
        }

        var successMessage = Humanizer.ReplaceDynamic(Phrases.SnackbarSuccess_CreatedDuplicatesMiis, miis.Length)!;
        if (miis.Length == 1)
            successMessage = Humanizer.ReplaceDynamic(Phrases.SnackbarSuccess_CreatedDuplicate, miis[0].Name)!;

        ReloadMiiList();
        ViewUtils.ShowSnackbar(successMessage);
    }

    private Mii[] GetSelectedMiis()
    {
        var selected = MiiList
            .Children.OfType<MiiBlock>()
            .Where(block => block is { IsChecked: true, Mii: not null })
            .Select(block => block.Mii!);
        return selected.ToArray();
    }

    private void ChangeTopButtons()
    {
        var selectedMiis = GetSelectedMiis();

        if (selectedMiis.Length == 0)
        {
            DeleteMiisButton.IsVisible = false;
            ExportMiisButton.IsVisible = false;
            EditMiisButton.IsVisible = false;
            DuplicateMiisButton.IsVisible = false;
            ImportMiiButton.IsVisible = true;
            FavoriteMiiButton.IsVisible = false;
            return;
        }

        FavoriteMiiButton.IsVisible = true;
        EditMiisButton.IsVisible = selectedMiis.Length == 1;
        ImportMiiButton.IsVisible = false;
        DeleteMiisButton.IsVisible = true;
        ExportMiisButton.IsVisible = true;
        DuplicateMiisButton.IsVisible = true;

        FavoriteMiiButton.Classes.Remove("UnFav");
        if (selectedMiis.All(mii => mii.IsFavorite))
            FavoriteMiiButton.Classes.Add("UnFav");
    }

    #region Command

    private void ContextAction(Mii mii, Action<Mii[]> command)
    {
        var selectedMiis = GetSelectedMiis();
        // If the user right clicks and perform action on a selected Mii, that action applies for all the selected Miis
        // But if the user right clicks and perform actions on a mii that is not selected, than it only happens for that specific mii.
        command.Invoke(selectedMiis.Contains(mii) ? selectedMiis : [mii]);
    }

    // There must be a better way to do this. Since this seems absurd
    private class MyCommand(Action command) : ICommand
    {
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => command.Invoke();

        public event EventHandler? CanExecuteChanged;
    }

    #endregion
}
