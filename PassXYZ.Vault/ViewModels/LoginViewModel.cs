using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using KPCLib;
using PassXYZLib;
using PassXYZ.Vault.Properties;
using PassXYZ.Vault.Services;
using PassXYZ.Vault.Views;

namespace PassXYZ.Vault.ViewModels;

public class LoginViewModel : BaseViewModel
{
    readonly IUserService<User> userService = ServiceHelper.GetService<IUserService<User>>();
    public ObservableCollection<User>? Users => userService.Users;
    public LoginUser CurrentUser => LoginUser.Instance;
    private Action<string> _signUpAction;
    public Command LoginCommand { get; }
    public Command SignUpCommand { get; }
    public Command CancelCommand { get; }
    public Command AddUserCommand { get; }
    public Command ImportUserCommand { get; }
    public Command ExportUserCommand { get; }
    public Command CloudConfigCommand { get; }

    public LoginViewModel()
    {
        LoginCommand = new Command(OnLoginClicked, ValidateLogin);
        SignUpCommand = new Command(OnSignUpClicked, ValidateSignUp);
        CancelCommand = new Command(OnCancelClicked);
        AddUserCommand = new Command(OnAddUserClicked);
        ImportUserCommand = new Command(OnImportUserClicked);
        ExportUserCommand = new Command(OnExportUserClicked);
        CloudConfigCommand = new Command(OnCloudConfigClicked);


        CurrentUser.PropertyChanged +=
            (_, __) => LoginCommand.ChangeCanExecute();

        CurrentUser.PropertyChanged +=
            (_, __) => SignUpCommand.ChangeCanExecute();

        Debug.WriteLine($"data_path={PxDataFile.DataFilePath}");
    }

    private async void OnCloudConfigClicked()
    {
        try
        {
            // Lógica para configurar la nube
            await Shell.Current.DisplayAlert("Cloud Configuración", "La configuración de la nube se ha iniciado", "OK");
            // Aquí puedes agregar la lógica para la configuración de la nube
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoginViewModel: CloudConfig, {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    public LoginViewModel(Action<string> signUpAction) : this()
    {
        _signUpAction = signUpAction;
    }

    private async void OnExportUserClicked()
    {
        try
        {
            // Lógica para exportar un usuario
            var options = new PickOptions
            {
                PickerTitle = "Seleccione la ubicación para guardar el archivo de exportación",
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                var filePath = result.FullPath;
                // Lógica para exportar los datos del usuario al archivo seleccionado
                var userData = GetUserData();  // Implementa este método para obtener los datos del usuario
                File.WriteAllText(filePath, userData);

                await Shell.Current.DisplayAlert("Exportación Exitosa", "El usuario ha sido exportado correctamente", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "No se seleccionó ningún archivo.", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoginViewModel: ExportUser, {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnAddUserClicked()
    {
        try
        {
            await userService.AddUserAsync(CurrentUser);
            await Shell.Current.DisplayAlert("Usuario Añadido", "El usuario ha sido añadido correctamente", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnImportUserClicked()  // Método para importar usuario
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = Properties.Resources.import_message1,
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                var stream = await result.OpenReadAsync();
                var fileStream = File.Create(CurrentUser.KeyFilePath);
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
                fileStream.Close();

                await Shell.Current.DisplayAlert("Importación Exitosa", "El usuario ha sido importado correctamente", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert(Properties.Resources.action_id_import, Properties.Resources.import_error_msg, Properties.Resources.alert_id_ok);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoginViewModel: ImportUser, {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private string GetUserData()
    {
        // Ejemplo simple de cómo podrías convertir los datos del usuario a una cadena JSON
        var userData = new
        {
            CurrentUser.Username,
            CurrentUser.Password,
            CurrentUser.IsDeviceLockEnabled
            // Otros campos del CurrentUser que desees exportar
        };

        return JsonSerializer.Serialize(userData);
    }

    private bool ValidateLogin()
    {
        return !string.IsNullOrWhiteSpace(CurrentUser.Username)
#if PASSXYZ_PRIVACYNOTICE_REQUIRED
            && LoginUser.IsPrivacyNoticeAccepted
#endif
            && !string.IsNullOrWhiteSpace(CurrentUser.Password);
    }

    private bool ValidateSignUp()
    {
        return !string.IsNullOrWhiteSpace(CurrentUser.Username)
            && !string.IsNullOrWhiteSpace(CurrentUser.Password)
            && !string.IsNullOrWhiteSpace(CurrentUser.Password2)
#if PASSXYZ_PRIVACYNOTICE_REQUIRED
            && LoginUser.IsPrivacyNoticeAccepted
#endif
            && CurrentUser.Password.Equals(CurrentUser.Password2);
    }

    public void OnAppearing()
    {
        IsBusy = false;
    }

    public async void OnLoginClicked()
    {
        try
        {
            IsBusy = true;

            if (string.IsNullOrWhiteSpace(CurrentUser.Password))
            {
                await Shell.Current.DisplayAlert("", Properties.Resources.settings_empty_password, Properties.Resources.alert_id_ok);
                IsBusy = false;
                return;
            }

            bool status = await userService.LoginAsync(CurrentUser);

            if (status)
            {
                if (AppShell.CurrentAppShell != null)
                {
                    AppShell.CurrentAppShell.SetRootPageTitle(DataStore.RootGroup.Name);

                    string path = Path.Combine(PxDataFile.TmpFilePath, CurrentUser.FileName);
                    if (File.Exists(path))
                    {
                        // If there is file to merge, we merge it first.
                        bool result = await DataStore.MergeAsync(path);
                    }

                    await Shell.Current.GoToAsync($"//RootPage");
                }
                else
                {
                    throw (new NullReferenceException("CurrentAppShell is null"));
                }
            }
            IsBusy = false;
        }
        catch (Exception ex)
        {
            IsBusy = false;
            string msg = ex.Message;
            if (ex is System.IO.IOException ioException)
            {
                Debug.WriteLine("LoginViewModel: Need to recover");
                msg = Properties.Resources.message_id_recover_datafile;
            }
            await Shell.Current.DisplayAlert(Properties.Resources.LoginErrorMessage, msg, Properties.Resources.alert_id_ok);
        }
    }

    private async void OnSignUpClicked()
    {
        if (CurrentUser.IsUserExist)
        {
            await Shell.Current.DisplayAlert(Properties.Resources.SignUpPageTitle, Properties.Resources.SignUpErrorMessage1, Properties.Resources.alert_id_ok);
            return;
        }

        try
        {
            await userService.AddUserAsync(CurrentUser);
            if (_signUpAction != null)
            {
                _signUpAction?.Invoke(CurrentUser.Username);
                _ = await Shell.Current.Navigation.PopModalAsync();
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(Properties.Resources.SignUpPageTitle, ex.Message, Properties.Resources.alert_id_ok);
        }
        Debug.WriteLine($"LoginViewModel: OnSignUpClicked {CurrentUser.Username}, DeviceLock: {CurrentUser.IsDeviceLockEnabled}");
    }

    private async void OnCancelClicked()
    {
        _ = await Shell.Current.Navigation.PopModalAsync();
    }

    public async void ImportKeyFile()
    {
        var options = new PickOptions
        {
            PickerTitle = Properties.Resources.import_message1,
            //FileTypes = customFileType,
        };

        try
        {
            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                var stream = await result.OpenReadAsync();
                var fileStream = File.Create(CurrentUser.KeyFilePath);
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
                fileStream.Close();
            }
            else
            {
                await Shell.Current.DisplayAlert(Properties.Resources.action_id_import, Properties.Resources.import_error_msg, Properties.Resources.alert_id_ok);
            }
        }
        catch (Exception ex)
        {
            // The user canceled or something went wrong
            Debug.WriteLine($"LoginViewModel: ImportKeyFile, {ex}");
        }
    }

    public string GetMasterPassword()
    {
        return userService.GetMasterPassword();
    }

    public string GetDeviceLockData()
    {
        return userService.GetDeviceLockData();
    }

    public bool CreateKeyFile(string data)
    {
        return userService.CreateKeyFile(data, CurrentUser.Username);
    }

    public List<string> GetUsersList()
    {
        return userService.GetUsersList();
    }
}
