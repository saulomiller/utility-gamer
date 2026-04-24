using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.UI;
using UtilityGamer.WinUI.Views.Controls;

namespace UtilityGamer.WinUI.Views;

public sealed partial class MainPage : Page
{
    // Resolution constraints.
    private const int MinResolutionWidth  = 640;
    private const int MaxResolutionWidth  = 7680;
    private const int MinResolutionHeight = 480;
    private const int MaxResolutionHeight = 4320;
    private const int MinRefreshRateHz     = 30;
    private const int MaxRefreshRateHz     = 360;

    // Startup registry paths.
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyName = "UtilityGamer";
    private const string DefaultLanguageCode = "pt-BR";
    private static readonly LanguageOption[] SupportedLanguages =
    [
        new("pt-BR", "Português (Brasil)"),
        new("en-US", "English (United States)")
    ];

    // Runtime state.
    private readonly ObservableCollection<GameProfile> _games = new();
    private readonly ProcessWatcher _watcher = new();
    private IReadOnlyList<DisplayHelper.MonitorInfo> _monitors = Array.Empty<DisplayHelper.MonitorInfo>();
    private IReadOnlyList<PowerPlanHelper.PowerPlan> _powerPlans = Array.Empty<PowerPlanHelper.PowerPlan>();

    private readonly string _settingsPath = AppSettings.DefaultPath;

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    // Prevents feedback loops while the UI is updated programmatically.
    private bool _isLoading;
    private bool _isChangingSelection;
    private bool _isInitialized;
    private bool _isShuttingDown;
    private CancellationTokenSource? _testCts;
    private CancellationTokenSource? _saveCts;
    private bool _repairRunning;
    private bool _settingsCorrupted;

    // Default resolutions used for new games and when no game is selected.
    private int _defaultGameWidth   = 1440;
    private int _defaultGameHeight  = 1080;
    private int _defaultGameHz      = 144;
    private int _defaultReturnWidth  = 1920;
    private int _defaultReturnHeight = 1080;
    private int _defaultReturnHz     = 144;
    private bool _defaultResolutionSwitchEnabled = false;

    // Cached tweak state used to rebuild the summary without probing the OS again.
    private bool?           _lastGlobalTimerState;
    private GameBarDvrState _lastGameBarState  = GameBarDvrState.Unknown;
    private bool?           _lastOverlayRemovedState;
    private bool?           _lastStickyKeysDisabledState;
    private bool?           _lastMouseAccelDisabledState;

    private readonly record struct LanguageOption(string Code, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly record struct LocalizedText(string PtBr, string LegacyEs = "");

    private static readonly IReadOnlyDictionary<string, LocalizedText> UiTexts =
        new Dictionary<string, LocalizedText>(StringComparer.Ordinal)
        {
            ["Status"] = new("Estado", "Estado"),
            ["Ready."] = new("Pronto.", "Listo."),
            ["Resolutions"] = new("Resoluções", "Resoluciones"),
            ["Tweaks"] = new("Ajustes", "Ajustes"),
            ["Settings"] = new("Configurações", "Configuración"),
            ["MONITORED GAMES"] = new("APLICATIVOS", "JUEGOS MONITOREADOS"),
            ["Name"] = new("Nome", "Nombre"),
            ["Process"] = new("Processo", "Proceso"),
            ["Monitor"] = new("Monitor", "Monitor"),
            ["Power"] = new("Energia", "Energia"),
            ["Executable"] = new("Executável", "Ejecutable"),
            ["MONITOR & POWER PLAN"] = new("MONITOR E PLANO DE ENERGIA", "MONITOR Y PLAN DE ENERGIA"),
            ["Monitor for this app"] = new("Monitor deste aplicativo", "Monitor para esta app"),
            ["Power plan when the app starts"] = new("Plano de energia ao iniciar o aplicativo", "Plan de energía al iniciar la app"),
            ["Primary"] = new("Principal", "Primario"),
            ["Primary monitor"] = new("Monitor principal", "Monitor primario"),
            ["Do not change"] = new("Não alterar", "No cambiar"),
            ["Add game"] = new("Adicionar aplicativo", "Agregar juego"),
            ["Remove selected"] = new("Remover selecionado", "Quitar seleccionado"),
            ["DESKTOP RESOLUTION PER APP"] = new("RESOLUÇÃO", "RESOLUCIÓN DE ESCRITORIO POR APP"),
            ["Selected app:"] = new("Aplicativo selecionado:", "App seleccionada:"),
            ["Selected app: none"] = new("Aplicativo selecionado: nenhum", "App seleccionada: ninguna"),
            ["Selected app: none  (values below are defaults for new apps)"] =
                new("Aplicativo selecionado: nenhum  (os valores abaixo são os padrões para novos aplicativos)",
                    "App seleccionada: ninguna  (los valores de abajo son predeterminados para apps nuevas)"),
            ["Applying to:"] = new("Aplicando em:", "Aplicando a:"),
            ["Applying to: no app selected"] =
                new("Aplicando em: nenhum aplicativo selecionado", "Aplicando a: ninguna app seleccionada"),
            ["On app start (desktop)"] = new("Ao iniciar o aplicativo (área de trabalho)", "Al iniciar la app (escritorio)"),
            ["On app close (desktop)"] = new("Ao fechar o aplicativo (área de trabalho)", "Al cerrar la app (escritorio)"),
            ["Switch desktop resolution automatically"] = new("Alterar automaticamente a resolução da área de trabalho", "Cambiar automáticamente la resolución del escritorio"),
            ["MONITORING"] = new("MONITORAMENTO", "MONITOREO"),
            ["WIDTH"] = new("LARGURA", "ANCHO"),
            ["HEIGHT"] = new("ALTURA", "ALTO"),
            ["HZ"] = new("HZ", "HZ"),
            ["Start monitoring"] = new("Iniciar monitoramento", "Iniciar monitoreo"),
            ["Stop monitoring"] = new("Parar monitoramento", "Detener monitoreo"),
            ["SYSTEM TWEAKS"] = new("AJUSTES DO SISTEMA", "AJUSTES DEL SISTEMA"),
            ["Enable GlobalTimerResolutionRequests = 1"] = new("Ativar GlobalTimerResolutionRequests = 1", "Activar GlobalTimerResolutionRequests = 1"),
            ["Disable Game Bar / DVR"] = new("Desativar Game Bar / DVR", "Desactivar Game Bar / DVR"),
            ["Remove Xbox Gaming Overlay"] = new("Remover Xbox Gaming Overlay", "Quitar Xbox Gaming Overlay"),
            ["INPUT"] = new("ENTRADA", "ENTRADA"),
            ["Disable Sticky Keys shortcut (Shift x5)"] = new("Desativar o atalho das Sticky Keys (Shift x5)", "Desactivar atajo de Sticky Keys (Shift x5)"),
            ["Disable mouse acceleration (Enhance Pointer Precision)"] = new("Desativar a aceleração do mouse (Aprimorar precisão do ponteiro)", "Desactivar aceleración del mouse (Mejorar precisión del puntero)"),
            ["APPLICATION"] = new("APLICATIVO", "APLICACIÓN"),
            ["STARTUP"] = new("INICIALIZAÇÃO", "INICIO"),
            ["Launch with Windows"] = new("Iniciar com o Windows", "Iniciar con Windows"),
            ["Start minimized in the tray"] = new("Iniciar minimizado na área de notificação", "Iniciar minimizado en la bandeja"),
            ["APPEARANCE"] = new("APARÊNCIA", "APARIENCIA"),
            ["Dark mode is always enabled."] = new("O modo escuro está sempre ativo.", "El modo oscuro siempre está activado."),
            ["LANGUAGE"] = new("IDIOMA", "IDIOMA"),
            ["Language"] = new("Idioma", "Idioma"),
            ["NOTIFICATIONS"] = new("NOTIFICAÇÕES", "NOTIFICACIONES"),
            ["Notify when the resolution changes"] = new("Notificar quando a resolução mudar", "Notificar cuando cambie la resolución"),
            ["Notify when the power plan changes"] = new("Notificar quando o plano de energia mudar", "Notificar cuando cambie el plan de energía"),
            ["Apply tweaks"] = new("Aplicar", "Aplicar cambios"),
            ["Check current state"] = new("Verificar status", "Revisar estado actual"),
            ["TWEAKS STATE"] = new("STATUS DOS AJUSTES", "ESTADO DE AJUSTES"),
            ["SYSTEM REPAIR"] = new("REPARO DO SISTEMA", "REPARACIÓN DEL SISTEMA"),
            ["REQUIRES ADMIN"] = new("REQUER ADMINISTRADOR", "REQUIERE ADMIN"),
            ["Run SFC"] = new("Executar SFC", "Ejecutar SFC"),
            ["Run DISM"] = new("Executar DISM", "Ejecutar DISM"),
            ["Test (10s)"] = new("Testar (10s)", "Probar (10s)"),
            ["Reverting in 10s..."] = new("Restaurando em 10s...", "Revirtiendo en 10s..."),
            ["Scans and repairs protected Windows files."] = new("Analisa e repara arquivos protegidos do Windows.", "Escanea y repara archivos protegidos de Windows."),
            ["Repairs the Windows image used by SFC."] = new("Repara a imagem do Windows usada pelo SFC.", "Repara la imagen de Windows usada por SFC."),
            ["ABOUT"] = new("SOBRE", "ACERCA DE"),
            ["Automatically switches desktop resolution and power plan when your games start and exit."] =
                new("Altera automaticamente a resolução da área de trabalho e o plano de energia quando seus jogos são iniciados e fechados.",
                    "Cambia automáticamente la resolución del escritorio y el plan de energía cuando tus juegos se inician y se cierran."),
            ["WHAT IT DOES"] = new("O QUE FAZ", "LO QUE HACE"),
            ["- Monitors game processes in the background"] = new("- Monitora os processos dos jogos em segundo plano", "- Supervisa procesos de juegos en segundo plano"),
            ["- Changes resolution when a game starts"] = new("- Altera a resolução quando um jogo inicia", "- Cambia la resolución cuando se inicia un juego"),
            ["- Restores resolution when the game exits"] = new("- Restaura a resolução quando o jogo termina", "- Restaura la resolución cuando el juego se cierra"),
            ["- Activates your preferred power plan per game"] = new("- Ativa o plano de energia preferido para cada jogo", "- Activa tu plan de energía preferido por juego"),
            ["- Restores the previous power plan on exit"] = new("- Restaura o plano de energia anterior ao sair", "- Restaura el plan de energía anterior al salir"),
            ["Sticky Keys shortcut"] = new("Atalho das Sticky Keys", "Atajo de Sticky Keys"),
            ["Mouse acceleration"] = new("Aceleração do mouse", "Aceleración del mouse"),
            ["Active / Disabled"] = new("Ativo / Desativado", "Activo / Desactivado"),
            ["Inactive / Enabled"] = new("Inativo / Ativado", "Inactivo / Activado"),
            ["Partial / Mixed"] = new("Parcial / Misto", "Parcial / Mixto"),
            ["Could not verify"] = new("Não foi possível verificar", "No se pudo verificar"),
            ["Status: Ready."] = new("Estado: Pronto.", "Estado: Listo."),
            ["Selected game removed."] = new("O jogo selecionado foi removido.", "Juego seleccionado quitado."),
            ["Monitoring stopped."] = new("O monitoramento foi parado.", "Monitoreo detenido."),
            ["Tweaks applied."] = new("Os ajustes foram aplicados.", "Ajustes aplicados."),
            ["Tweaks check completed."] = new("A verificação dos ajustes foi concluída.", "Revisión de ajustes completada."),
            ["Tweaks state"] = new("Status dos ajustes", "Estado de ajustes"),
            ["Test completed - resolution restored."] = new("Teste concluído - resolução restaurada.", "Prueba completada: resolución restaurada."),
            ["Automatically changes the desktop resolution when the selected app starts and restores the previous values when it closes."] =
                new("Altera automaticamente a resolução da área de trabalho quando o aplicativo selecionado é iniciado e restaura os valores anteriores ao ser fechado."),
            ["Choose which monitor should receive the desktop resolution profile for this app."] =
                new("Escolha o monitor que deve receber o perfil de resolução da área de trabalho deste aplicativo."),
            ["Select the power plan to activate when this app starts."] =
                new("Selecione o plano de energia que será ativado quando este aplicativo iniciar."),
            ["Enables Windows to honour finer timer resolution requests made by games and low-latency apps."] =
                new("Permite que o Windows respeite solicitações de resolução de timer mais fina feitas por jogos e aplicativos de baixa latência."),
            ["Disables Game Bar capture and background DVR services that can interfere with performance."] =
                new("Desativa a captura da Game Bar e os serviços DVR em segundo plano que podem interferir com o desempenho."),
            ["Removes the Xbox Gaming Overlay package from Windows for a cleaner gaming session."] =
                new("Remove o pacote Xbox Gaming Overlay do Windows para uma sessão de jogo mais limpa."),
            ["Prevents the Sticky Keys shortcut from appearing when you press Shift five times."] =
                new("Impede que o atalho das Sticky Keys apareça ao pressionar Shift cinco vezes."),
            ["Turns off Enhance Pointer Precision so mouse movement stays raw and predictable in games."] =
                new("Desativa a opção Aprimorar precisão do ponteiro para manter o movimento do mouse direto e previsível nos jogos."),
            ["Applies every selected tweak immediately. Administrator rights may be required."] =
                new("Aplica imediatamente todos os ajustes selecionados. Podem ser necessários privilégios de administrador."),
            ["Reads the current Windows state again and refreshes the status list on the right."] =
                new("Lê novamente o estado atual do Windows e atualiza a lista de status à direita."),
            ["Runs the System File Checker to repair corrupted protected Windows files."] =
                new("Executa o System File Checker para reparar arquivos protegidos do Windows que estejam corrompidos."),
            ["Runs DISM to repair the Windows image used by servicing and SFC."] =
                new("Executa o DISM para reparar a imagem do Windows usada pelos serviços do sistema e pelo SFC."),
            ["Adds Utility Gamer to Windows startup."] =
                new("Adiciona o Utility Gamer à inicialização do Windows."),
            ["Starts the app minimized in the notification area instead of opening the full window."] =
                new("Inicia o aplicativo minimizado na área de notificação em vez de abrir a janela completa."),
            ["Chooses the app language. Spanish has been removed; only Portuguese and English remain."] =
                new("Escolhe o idioma do aplicativo. O espanhol foi removido; agora só existem Português (Brasil) e Inglês."),
            ["Shows a notification when Utility Gamer changes the desktop resolution."] =
                new("Mostra uma notificação quando o Utility Gamer altera a resolução da área de trabalho."),
            ["Shows a notification when Utility Gamer changes the active power plan."] =
                new("Mostra uma notificação quando o Utility Gamer altera o plano de energia ativo."),
            ["Turns background monitoring on or off for the tracked games list."] =
                new("Liga ou desliga o monitoramento em segundo plano da lista de jogos monitorados.")
        };

    private static readonly IReadOnlyDictionary<string, string> UiTextToKey = BuildUiReverseMap();

    // Theme palettes.
    private static readonly IReadOnlyDictionary<string, string> DarkPalette =
        new Dictionary<string, string>
        {
            ["SurfaceBrush"]      = "#111C27",
            ["CardBrush"]         = "#162030",
            ["CardBorderBrush"]   = "#1F3045",
            ["HeaderRowBrush"]    = "#1A2D40",
            ["MutedFgBrush"]      = "#7A92A8",
            ["FgBrush"]           = "#D4E4F0",
            ["AccentBrush"]       = "#4F8EF7",
            ["AccentHoverBrush"]  = "#6BA3F8",
            ["AccentPressedBrush"]= "#3A72D6",
            ["GhostBorderBrush"]  = "#3A4A5A",
            ["GhostHoverBrush"]   = "#1E2D3D",
            ["SuccessBrush"]      = "#3CB97A",
            ["DangerBrush"]       = "#E05C5C",
            ["TabViewItemHeaderBackgroundSelected"] = "#162030",
            ["TabViewItemHeaderBackgroundPointerOver"] = "#1A2D40",
            ["TabViewItemHeaderBackgroundPressed"] = "#1A2D40",
            ["TabViewItemHeaderForegroundSelected"] = "#D4E4F0",
            ["TabViewItemHeaderForeground"] = "#7A92A8",
            ["TabViewItemHeaderForegroundPointerOver"] = "#D4E4F0",
        };

    // Constructor.
    public MainPage()
    {
        InitializeComponent();
        GamesListView.ItemsSource = _games;

        foreach (var language in SupportedLanguages)
            LanguageCombo.Items.Add(language);

        SelectLanguage(DefaultLanguageCode);

        HookSettingsEvents();
    }

    // Initialization.
    private void HookSettingsEvents()
    {
        void SaveOnChange(object? s, RoutedEventArgs e) => SaveSettingsIfReady();
        void StartupOnChange(object? s, RoutedEventArgs e) => OnStartupOptionsChanged();

        GlobalTimerCheck.Checked   += SaveOnChange;
        GlobalTimerCheck.Unchecked += SaveOnChange;
        DisableGameBarCheck.Checked   += SaveOnChange;
        DisableGameBarCheck.Unchecked += SaveOnChange;
        RemoveOverlayCheck.Checked   += SaveOnChange;
        RemoveOverlayCheck.Unchecked += SaveOnChange;
        StickyKeysCheck.Checked   += OnInputTweakChanged;
        StickyKeysCheck.Unchecked += OnInputTweakChanged;
        MouseAccelCheck.Checked   += OnInputTweakChanged;
        MouseAccelCheck.Unchecked += OnInputTweakChanged;
        NotifyOnResolutionChangeCheck.Checked   += SaveOnChange;
        NotifyOnResolutionChangeCheck.Unchecked += SaveOnChange;
        NotifyOnPowerPlanChangeCheck.Checked    += SaveOnChange;
        NotifyOnPowerPlanChangeCheck.Unchecked  += SaveOnChange;

        StartWithWindowsCheck.Checked   += StartupOnChange;
        StartWithWindowsCheck.Unchecked += StartupOnChange;
        StartMinimizedCheck.Checked     += StartupOnChange;
        StartMinimizedCheck.Unchecked   += StartupOnChange;

        LanguageCombo.SelectionChanged += (_, _) =>
        {
            if (_isLoading) return;
            ApplyLanguage();
            SaveSettingsDebounced();
        };

        MainTabView.SelectionChanged += (_, _) =>
        {
            if (!_isInitialized) return;
            DispatcherQueue.TryEnqueue(ApplyLanguage);
        };
    }

    // Page lifecycle.

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        App.MainWindow.Closed += OnMainWindowClosed;

        var monitorsTask = Task.Run(DisplayHelper.GetMonitors);
        var powerPlansTask = PowerPlanHelper.GetPlansAsync();
        await Task.WhenAll(monitorsTask, powerPlansTask);

        _monitors = monitorsTask.Result;
        _powerPlans = powerPlansTask.Result;

        PopulateMonitorCombo();
        PopulatePowerPlanCombo();


        _watcher.StatusChanged += text => DispatcherQueue.TryEnqueue(() => UpdateStatus(text));

        LoadSettings();
        RefreshGamePowerPlanDisplayNames();
        ApplyTheme();
        ApplyLanguage();
        App.SetMinimizeToTrayEnabled(StartMinimizedCheck.IsChecked == true);
        UpdateResolutionEditorFromSelection();

        await RefreshTweaksStateAsync(showDialog: false);
        await Task.Yield();
        ApplyLanguage();

        if (ShouldAutoStart())
            StartWatcher();

        UpdateMonitoringState();
        UpdateStatus("Ready.");

        if (_settingsCorrupted)
        {
            await ShowMessageAsync(
                "Settings",
                "The settings file was corrupted and could not be loaded. " +
                "Default values have been applied.\n\n" +
                $"Corrupted file: {_settingsPath}");
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args) => Shutdown();

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        Shutdown();
    }

    private void Shutdown()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        CancelAndDispose(ref _testCts);
        CancelAndDispose(ref _saveCts);
        _watcher.Dispose();
        SaveSettings();
        App.MainWindow.Closed -= OnMainWindowClosed;
    }

    // Games list.

    private async void OnAddGameClicked(object sender, RoutedEventArgs e)
    {
        string? exePath;
        try
        {
            exePath = Win32FileDialog.PickExeFile(App.MainWindow is null
                ? 0
                : WindowNative.GetWindowHandle(App.MainWindow));
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Could not open file picker: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(exePath)) return;

        if (!TryNormalizeExecutablePath(exePath, out var normalizedExePath, out var validationMessage))
        {
            await ShowMessageAsync("Warning", validationMessage);
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(normalizedExePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            await ShowMessageAsync("Warning", "Could not detect process name.");
            return;
        }

        if (_games.Any(g => string.Equals(g.ExePath, normalizedExePath, StringComparison.OrdinalIgnoreCase)))
        {
            await ShowMessageAsync("Info", $"Executable '{normalizedExePath}' is already in the list.");
            return;
        }

        if (_games.Any(g => string.Equals(g.ProcessName, processName, StringComparison.OrdinalIgnoreCase)))
        {
            await ShowMessageAsync("Info", $"Process '{processName}' is already in the list.");
            return;
        }

        var newGame = new GameProfile
        {
            Name        = processName,
            ExePath     = normalizedExePath,
            ProcessName = processName,
            GameWidth   = _defaultGameWidth,
            GameHeight  = _defaultGameHeight,
            GameHz      = _defaultGameHz,
            DefaultWidth  = _defaultReturnWidth,
            DefaultHeight = _defaultReturnHeight,
            DefaultHz     = _defaultReturnHz,
            ResolutionSwitchEnabled = false
        };

        _games.Add(newGame);
        GamesListView.SelectedItem = newGame;
        UpdateResolutionEditorFromSelection();
        UpdateGamePowerPlanDisplayName(newGame);
        SaveSettings();

        if (_games.Count == 1 && !_watcher.IsRunning)
            StartWatcher();
        else if (_watcher.IsRunning)
            StartWatcher();

        UpdateStatus($"Added: {processName}");
    }
    private void OnRemoveSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (GamesListView.SelectedItem is not GameProfile selected) return;

        _games.Remove(selected);
        GamesListView.SelectedItem = _games.Count > 0 ? _games[0] : null;
        UpdateResolutionEditorFromSelection();
        SaveSettings();

        if (_games.Count == 0)
            StopWatcher();
        else if (_watcher.IsRunning)
            StartWatcher();

        UpdateStatus("Selected game removed.");
    }

    private void OnGamesSelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateResolutionEditorFromSelection();

    private void PopulateMonitorCombo()
    {
        MonitorCombo.Items.Clear();
        MonitorCombo.Items.Add(TranslateUiText("Primary monitor"));

        foreach (var monitor in _monitors)
            MonitorCombo.Items.Add(monitor.FriendlyName);
    }

    private void PopulatePowerPlanCombo()
    {
        PowerPlanCombo.Items.Clear();
        PowerPlanCombo.Items.Add($"- {TranslateUiText("Do not change")} -");

        foreach (var plan in _powerPlans)
            PowerPlanCombo.Items.Add(plan.Name);
    }

    private void OnMonitorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _isChangingSelection) return;

        var game = GetSelectedGame();
        if (game is null) return;

        var index = MonitorCombo.SelectedIndex;
        game.MonitorDeviceName = index <= 0 || index > _monitors.Count
            ? null
            : _monitors[index - 1].DeviceName;

        SaveSettingsDebounced();
    }

    private void OnPowerPlanSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _isChangingSelection) return;

        var game = GetSelectedGame();
        if (game is null) return;

        var index = PowerPlanCombo.SelectedIndex;
        if (index <= 0)
        {
            game.PowerPlanGuid = null;
            game.PowerPlanDisplayName = null;
        }
        else
        {
            var selectedPlan = _powerPlans[index - 1];
            game.PowerPlanGuid = selectedPlan.Guid;
            game.PowerPlanDisplayName = selectedPlan.Name;
        }

        SaveSettingsDebounced();
    }

    // Resolution editor.

    private void OnResolutionValueChanged(object sender, int newValue)
    {
        if (_isLoading || _isChangingSelection) return;

        var game = GetSelectedGame();
        if (game is not null)
        {
            game.GameWidth = GameWidthBox.Value;
            game.GameHeight = GameHeightBox.Value;
            game.GameHz = GameHzBox.Value;
            game.DefaultWidth = DefaultWidthBox.Value;
            game.DefaultHeight = DefaultHeightBox.Value;
            game.DefaultHz = DefaultHzBox.Value;
            UpdateStatus($"Resolution updated for {game.ProcessName}.");
        }
        else
        {
            _defaultGameWidth = GameWidthBox.Value;
            _defaultGameHeight = GameHeightBox.Value;
            _defaultGameHz = GameHzBox.Value;
            _defaultReturnWidth = DefaultWidthBox.Value;
            _defaultReturnHeight = DefaultHeightBox.Value;
            _defaultReturnHz = DefaultHzBox.Value;
        }

        SaveSettingsDebounced();
    }

    private void OnResolutionSwitchChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _isChangingSelection) return;

        var enabled = ResolutionSwitchCheck.IsChecked == true;
        var game = GetSelectedGame();
        if (game is not null)
            game.ResolutionSwitchEnabled = enabled;
        else
            _defaultResolutionSwitchEnabled = enabled;

        UpdateResolutionControlsState(enabled);
        SaveSettingsDebounced();
    }

    private GameProfile? GetSelectedGame() => GamesListView.SelectedItem as GameProfile;

    private void UpdateResolutionEditorFromSelection()
    {
        using var _ = BeginChangingSelection();
        var game = GetSelectedGame();
        if (game is null)
        {
            SelectedGameText.Text = TranslateUiText(
                "Selected app: none  (values below are defaults for new apps)");

            SetSpinner(GameWidthBox, _defaultGameWidth);
            SetSpinner(GameHeightBox, _defaultGameHeight);
            SetSpinner(GameHzBox, _defaultGameHz);
            SetSpinner(DefaultWidthBox, _defaultReturnWidth);
            SetSpinner(DefaultHeightBox, _defaultReturnHeight);
            SetSpinner(DefaultHzBox, _defaultReturnHz);
            ResolutionSwitchCheck.IsEnabled = true;
            ResolutionSwitchCheck.IsChecked = _defaultResolutionSwitchEnabled;
            UpdateResolutionControlsState(_defaultResolutionSwitchEnabled);
            MonitorCombo.IsEnabled = false;
            PowerPlanCombo.IsEnabled = false;
            if (MonitorCombo.Items.Count > 0) MonitorCombo.SelectedIndex = 0;
            if (PowerPlanCombo.Items.Count > 0) PowerPlanCombo.SelectedIndex = 0;
            return;
        }
        NormalizeGameProfile(game);
        UpdateGamePowerPlanDisplayName(game);
        var selectedPrefix = TranslateUiText("Selected app:");
        SelectedGameText.Text = $"{selectedPrefix} {game.Name} ({game.ProcessName})";
        SetSpinner(GameWidthBox, game.GameWidth);
        SetSpinner(GameHeightBox, game.GameHeight);
        SetSpinner(GameHzBox, game.GameHz);
        SetSpinner(DefaultWidthBox, game.DefaultWidth);
        SetSpinner(DefaultHeightBox, game.DefaultHeight);
        SetSpinner(DefaultHzBox, game.DefaultHz);
        ResolutionSwitchCheck.IsEnabled = true;
        ResolutionSwitchCheck.IsChecked = game.ResolutionSwitchEnabled;
        UpdateResolutionControlsState(game.ResolutionSwitchEnabled);
        MonitorCombo.IsEnabled = true;
        PowerPlanCombo.IsEnabled = true;
        if (MonitorCombo.Items.Count > 0)
        {
            var monitorIndex = game.MonitorDeviceName is null
                ? 0
                : FindIndex(_monitors, m => string.Equals(m.DeviceName, game.MonitorDeviceName, StringComparison.OrdinalIgnoreCase)) + 1;
            MonitorCombo.SelectedIndex = monitorIndex >= 0 && monitorIndex < MonitorCombo.Items.Count ? monitorIndex : 0;
        }
        if (PowerPlanCombo.Items.Count > 0)
        {
            var planIndex = game.PowerPlanGuid is null
                ? 0
                : FindIndex(_powerPlans, p => p.Guid == game.PowerPlanGuid.Value) + 1;
            PowerPlanCombo.SelectedIndex = planIndex >= 0 && planIndex < PowerPlanCombo.Items.Count ? planIndex : 0;
        }
    }

    private void UpdateResolutionControlsState(bool enabled)
    {
        GameWidthBox.IsEnabled = enabled;
        GameHeightBox.IsEnabled = enabled;
        GameHzBox.IsEnabled = enabled;
        DefaultWidthBox.IsEnabled = enabled;
        DefaultHeightBox.IsEnabled = enabled;
        DefaultHzBox.IsEnabled = enabled;
        TestResolutionButton.IsEnabled = enabled;
    }
    private static void SetSpinner(NumericSpinner spinner, int value) => spinner.Value = value;

    private void NormalizeGameProfile(GameProfile game)
    {
        if (string.IsNullOrWhiteSpace(game.ProcessName) && !string.IsNullOrWhiteSpace(game.ExePath))
            game.ProcessName = Path.GetFileNameWithoutExtension(game.ExePath);

        if (string.IsNullOrWhiteSpace(game.Name))
        {
            game.Name = string.IsNullOrWhiteSpace(game.ProcessName)
                ? Path.GetFileNameWithoutExtension(game.ExePath)
                : game.ProcessName;
        }

        game.GameWidth = Clamp(game.GameWidth, MinResolutionWidth, MaxResolutionWidth, _defaultGameWidth);
        game.GameHeight = Clamp(game.GameHeight, MinResolutionHeight, MaxResolutionHeight, _defaultGameHeight);
        game.GameHz = Clamp(game.GameHz, MinRefreshRateHz, MaxRefreshRateHz, _defaultGameHz);
        game.DefaultWidth = Clamp(game.DefaultWidth, MinResolutionWidth, MaxResolutionWidth, _defaultReturnWidth);
        game.DefaultHeight = Clamp(game.DefaultHeight, MinResolutionHeight, MaxResolutionHeight, _defaultReturnHeight);
        game.DefaultHz = Clamp(game.DefaultHz, MinRefreshRateHz, MaxRefreshRateHz, _defaultReturnHz);
        if (string.IsNullOrWhiteSpace(game.MonitorDeviceName)
            || !_monitors.Any(m => string.Equals(m.DeviceName, game.MonitorDeviceName, StringComparison.OrdinalIgnoreCase)))
        {
            game.MonitorDeviceName = null;
        }

        if (game.PowerPlanGuid.HasValue
            && _powerPlans.Count > 0
            && !_powerPlans.Any(p => p.Guid == game.PowerPlanGuid.Value))
        {
            game.PowerPlanGuid = null;
            game.PowerPlanDisplayName = null;
        }
    }
    private static int Clamp(int value, int min, int max, int fallback)
        => value <= 0
            ? Math.Clamp(fallback, min, max)
            : Math.Clamp(value, min, max);

    private async void OnTestResolutionClicked(object sender, RoutedEventArgs e)
    {
        var game = GetSelectedGame();
        var resolutionSwitchEnabled = game?.ResolutionSwitchEnabled ?? _defaultResolutionSwitchEnabled;
        if (!resolutionSwitchEnabled)
        {
            await ShowMessageAsync("Test", "Desktop resolution switch is disabled for this app.");
            return;
        }

        var monitorName = game?.MonitorDeviceName
            ?? _monitors.FirstOrDefault(m => m.IsPrimary)?.DeviceName;

        if (monitorName is null)
        {
            await ShowMessageAsync("Test", "No monitor found.");
            return;
        }

        var saved = DisplayHelper.GetCurrentResolution(monitorName);
        if (saved is null)
        {
            await ShowMessageAsync("Test", "Could not read current resolution.");
            return;
        }

        var testW = game?.GameWidth ?? _defaultGameWidth;
        var testH = game?.GameHeight ?? _defaultGameHeight;
        var testHz = game?.GameHz ?? _defaultGameHz;

        var error = DisplayHelper.SetResolution(monitorName, testW, testH, testHz);
        if (error is not null)
        {
            await ShowMessageAsync("Test failed", error);
            return;
        }

        CancelAndDispose(ref _testCts);
        var testCts = new CancellationTokenSource();
        _testCts = testCts;
        var token = testCts.Token;

        TestResolutionButton.IsEnabled = false;
        TestCountdownText.Visibility = Visibility.Visible;

        try
        {
            for (var i = 10; i > 0; i--)
            {
                token.ThrowIfCancellationRequested();
                TestCountdownText.Text = GetRevertCountdownText(i);
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected here. Restore still runs in finally.
        }
        finally
        {
            DisplayHelper.SetResolution(monitorName, saved.Value.Width, saved.Value.Height, saved.Value.Hz);

            TestResolutionButton.IsEnabled = true;
            TestCountdownText.Visibility = Visibility.Collapsed;
            TestCountdownText.Text = GetRevertCountdownText(10);

            if (ReferenceEquals(_testCts, testCts))
            {
                _testCts = null;
                testCts.Dispose();
            }

            UpdateStatus("Test completed - resolution restored.");
        }
    }
    // Monitoring.
    private void OnMonitoringToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (MonitoringToggle.IsOn)
            StartWatcher();
        else
            StopWatcher();
    }

    private void StartWatcher()
    {
        if (_games.Count == 0)
        {
            _ = ShowMessageAsync("Info", "Add at least one game before starting monitoring.");
            return;
        }

        _watcher.NotifyResolution = NotifyOnResolutionChangeCheck.IsChecked == true;
        _watcher.NotifyPowerPlan = NotifyOnPowerPlanChangeCheck.IsChecked == true;
        _watcher.Start(_games);
        UpdateMonitoringState();
        SaveSettings();
    }

    private void StopWatcher()
    {
        _watcher.Stop();
        UpdateMonitoringState();
    }

    public void StartWatcherFromTray()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StartWatcher();
        });
    }

    public void StopWatcherFromTray()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StopWatcher();
        });
    }

    public bool StartMinimizedEnabled => StartMinimizedCheck.IsChecked == true;

    private void UpdateMonitoringState()
    {
        var active = _watcher.IsRunning;

        using var _ = BeginLoading();
        MonitoringToggle.IsOn = active;

        StatusDot.Fill = new SolidColorBrush(active
            ? ColorFromHex("#3CB97A")
            : ColorFromHex("#7A92A8"));

        App.SetTrayMonitoringState(active);
    }

    private bool ShouldAutoStart() => _games.Count > 0;

    // System tweaks.

    private async void OnApplyTweaksClicked(object sender, RoutedEventArgs e)
        => await ApplyTweaksAsync();

    private async void OnCheckTweaksClicked(object sender, RoutedEventArgs e)
        => await RefreshTweaksStateAsync(showDialog: true);

    private async void OnInputTweakChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        SaveSettingsDebounced();

        if (!_isInitialized) return;

        ApplyInputTweaks();
        await RefreshTweaksStateAsync(showDialog: false);
        UpdateStatus("Tweaks applied.");
    }

    private List<string> ApplyInputTweaks()
    {
        var messages = new List<string>();

        var desiredSticky = StickyKeysCheck.IsChecked == true;
        AccessibilityHelper.SetStickyKeysDisabled(desiredSticky);
        var stickyApplied = AccessibilityHelper.IsStickyKeysDisabled();
        messages.Add(stickyApplied == desiredSticky
            ? (desiredSticky ? "Sticky Keys shortcut disabled." : "Sticky Keys shortcut restored.")
            : "Sticky Keys shortcut could not be updated.");

        var desiredMouse = MouseAccelCheck.IsChecked == true;
        MouseHelper.SetMouseAccelDisabled(desiredMouse);
        var mouseApplied = MouseHelper.IsMouseAccelDisabled();
        messages.Add(mouseApplied == desiredMouse
            ? (desiredMouse ? "Mouse acceleration disabled." : "Mouse acceleration restored.")
            : "Mouse acceleration could not be updated.");

        return messages;
    }

    private async Task ApplyTweaksAsync()
    {
        ApplyTweaksButton.IsEnabled = false;
        try
        {
            var messages = new List<string>();

            // Global timer tweak.
            messages.Add(await ApplyGlobalTimerTweakAsync(GlobalTimerCheck.IsChecked == true));

            // Game Bar and DVR tweak.
            messages.Add(await ApplyGameBarDvrTweakAsync(DisableGameBarCheck.IsChecked == true));

            // Xbox overlay tweak.
            if (RemoveOverlayCheck.IsChecked == true)
            {
                var r = await RunAsync(GetPowerShellPath(),
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                    "\"Get-AppxPackage *Microsoft.XboxGamingOverlay* | Remove-AppxPackage\"");
                messages.Add(r.Success
                    ? "Xbox Gaming Overlay removal executed."
                    : $"Overlay removal failed: {r.Output}");
            }
            else
            {
                messages.Add("Xbox Gaming Overlay: no changes (checkbox unchecked).");
            }

            SaveSettingsDebounced();
            UpdateStatus("Tweaks applied.");
            await ShowMessageAsync("Utility Gamer", string.Join(Environment.NewLine, messages));
            await RefreshTweaksStateAsync(showDialog: false);
        }
        finally
        {
            ApplyTweaksButton.IsEnabled = true;
        }
    }

    private async Task RefreshTweaksStateAsync(bool showDialog)
    {
        CheckTweaksButton.IsEnabled = false;
        try
        {
            var globalTimer = GetGlobalTimerState();
            var gameBar = GetGameBarDvrState();
            var overlayGone = await GetXboxOverlayRemovedAsync();
            var stickyKeysDisabled = AccessibilityHelper.IsStickyKeysDisabled();
            var mouseAccelDisabled = MouseHelper.IsMouseAccelDisabled();

            _lastGlobalTimerState = globalTimer;
            _lastGameBarState = gameBar;
            _lastOverlayRemovedState = overlayGone;
            _lastStickyKeysDisabledState = stickyKeysDisabled;
            _lastMouseAccelDisabledState = mouseAccelDisabled;

            var globalTimerKind = globalTimer switch
            {
                true => StateKind.Ok,
                false => StateKind.Off,
                _ => StateKind.Unknown
            };
            var gameBarKind = gameBar switch
            {
                GameBarDvrState.Disabled => StateKind.Ok,
                GameBarDvrState.Enabled => StateKind.Off,
                GameBarDvrState.Mixed => StateKind.Warn,
                _ => StateKind.Unknown
            };
            var overlayKind = overlayGone switch
            {
                true => StateKind.Ok,
                false => StateKind.Off,
                _ => StateKind.Unknown
            };
            var stickyKeysKind = stickyKeysDisabled ? StateKind.Ok : StateKind.Off;
            var mouseAccelKind = mouseAccelDisabled ? StateKind.Ok : StateKind.Off;

            using (BeginLoading())
            {
                if (globalTimer.HasValue)
                    GlobalTimerCheck.IsChecked = globalTimer.Value;

                DisableGameBarCheck.IsChecked = gameBar switch
                {
                    GameBarDvrState.Disabled => true,
                    GameBarDvrState.Enabled => false,
                    _ => DisableGameBarCheck.IsChecked
                };

                if (overlayGone.HasValue)
                    RemoveOverlayCheck.IsChecked = overlayGone.Value;

                StickyKeysCheck.IsChecked = stickyKeysDisabled;
                MouseAccelCheck.IsChecked = mouseAccelDisabled;

                UpdateTweaksStatePanel(
                    globalTimer,
                    gameBar,
                    overlayGone,
                    stickyKeysDisabled,
                    mouseAccelDisabled);
            }

            SaveSettingsDebounced();
            UpdateStatus("Tweaks check completed.");

            if (showDialog)
            {
                var summary = string.Join(Environment.NewLine, new[]
                {
                    $"GlobalTimerResolutionRequests: {GetStateLabel(globalTimerKind)}",
                    $"Game Bar / DVR: {GetStateLabel(gameBarKind)}",
                    $"Xbox Gaming Overlay: {GetStateLabel(overlayKind)}",
                    $"Sticky Keys shortcut: {GetStateLabel(stickyKeysKind)}",
                    $"Mouse acceleration: {GetStateLabel(mouseAccelKind)}"
                });
                await ShowMessageAsync(TranslateUiText("Tweaks state"), summary);
            }
        }
        finally
        {
            CheckTweaksButton.IsEnabled = true;
        }
    }

    private async void OnRunSfcClicked(object sender, RoutedEventArgs e)
        => await RunRepairAsync(RepairKind.Sfc);

    private async void OnRunDismClicked(object sender, RoutedEventArgs e)
        => await RunRepairAsync(RepairKind.Dism);

    private async Task RunRepairAsync(RepairKind kind)
    {
        if (_repairRunning) return;

        var (fileName, arguments, label) = kind switch
        {
            RepairKind.Sfc => ("sfc.exe", "/scannow", "SFC"),
            RepairKind.Dism => ("dism.exe", "/Online /Cleanup-Image /RestoreHealth", "DISM"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        _repairRunning = true;
        RunSfcButton.IsEnabled = false;
        RunDismButton.IsEnabled = false;
        RepairProgressBar.Visibility = Visibility.Visible;
        SetRepairStatusBrush("MutedFgBrush", "#7A92A8");
        SetRepairStatusLine($"Running {label}... this may take several minutes.");
        UpdateStatus($"{label} started.");

        try
        {
            var result = await RunRepairProcessAsync(fileName, arguments, line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                DispatcherQueue.TryEnqueue(() => SetRepairStatusLine(line));
            });

            var success = result.ExitCode == 0;
            SetRepairStatusBrush(success ? "SuccessBrush" : "DangerBrush", success ? "#3CB97A" : "#E05C5C");
            SetRepairStatusLine(success
                ? $"{label} completed successfully."
                : $"{label} finished with code {result.ExitCode}.");
            UpdateStatus(success
                ? $"{label} completed successfully."
                : $"{label} finished with code {result.ExitCode}.");
        }
        catch (Exception ex)
        {
            SetRepairStatusBrush("DangerBrush", "#E05C5C");
            SetRepairStatusLine($"{label} failed: {ex.Message}");
            UpdateStatus($"{label} failed.");
        }
        finally
        {
            RepairProgressBar.Visibility = Visibility.Collapsed;
            RunSfcButton.IsEnabled = true;
            RunDismButton.IsEnabled = true;
            _repairRunning = false;
        }
    }

    private static async Task<CommandResult> RunRepairProcessAsync(
        string fileName,
        string arguments,
        Action<string>? onOutput = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetSystemToolPath(fileName),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        var output = new StringBuilder();

        void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            output.AppendLine(line);
            onOutput?.Invoke(line.Trim());
        }

        process.OutputDataReceived += (_, e) => Capture(e.Data);
        process.ErrorDataReceived += (_, e) => Capture(e.Data);

        if (!process.Start())
            return new CommandResult(-1, "Failed to start process.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        process.WaitForExit();

        return new CommandResult(process.ExitCode, output.ToString().Trim());
    }

    private static string GetSystemToolPath(string fileName)
    {
        var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            return Path.Combine(windowsPath, "Sysnative", fileName);

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), fileName);
    }

    private void SetRepairStatusLine(string text)
    {
        RepairStatusText.Text = text.Replace("\r", string.Empty)
            .Replace("\n", " ")
            .Trim();
    }

    private void SetRepairStatusBrush(string resourceKey, string fallbackHex)
    {
        RepairStatusText.Foreground = GetBrush(resourceKey, fallbackHex);
    }

    private static bool? GetGlobalTimerState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel");
            var val = GetDword(key, "GlobalTimerResolutionRequests");
            return val.HasValue ? val.Value == 1 : null;
        }
        catch { return null; }
    }

    private static GameBarDvrState GetGameBarDvrState()
    {
        try
        {
            using var cfgStore = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
            using var dvr      = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\GameDVR");
            using var policy   = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Policies\Microsoft\Windows\GameDVR");

            var dvrEnabled    = GetDword(cfgStore, "GameDVR_Enabled");
            var fseBehavior   = GetDword(cfgStore, "GameDVR_FSEBehaviorMode");
            var appCapture    = GetDword(dvr,      "AppCaptureEnabled");
            var allowGameDvr  = GetDword(policy,   "AllowGameDVR");

            if (dvrEnabled == 0 && fseBehavior == 2 && appCapture == 0 && allowGameDvr == 0)
                return GameBarDvrState.Disabled;

            if (dvrEnabled == 1 && fseBehavior == 0 && appCapture == 1
                && (!allowGameDvr.HasValue || allowGameDvr.Value != 0))
                return GameBarDvrState.Enabled;

            return GameBarDvrState.Mixed;
        }
        catch { return GameBarDvrState.Unknown; }
    }

    private async Task<bool?> GetXboxOverlayRemovedAsync()
    {
        var result = await RunAsync(GetPowerShellPath(),
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
            "\"$pkg = Get-AppxPackage *Microsoft.XboxGamingOverlay*;" +
            " if ($pkg) { 'INSTALLED' } else { 'REMOVED' }\"");

        if (!result.Success) return null;
        if (result.Output.Contains("REMOVED",    StringComparison.OrdinalIgnoreCase)) return true;
        if (result.Output.Contains("INSTALLED",  StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static int? GetDword(RegistryKey? key, string name)
    {
        if (key is null) return null;
        try
        {
            return key.GetValue(name) switch
            {
                int    i => i,
                uint   u => unchecked((int)u),
                long   l => unchecked((int)l),
                string s when int.TryParse(s, out var p) => p,
                { } v    => Convert.ToInt32(v),
                null     => null
            };
        }
        catch { return null; }
    }

    private void UpdateTweaksStatePanel(
        bool? globalTimer,
        GameBarDvrState gameBar,
        bool? overlayGone,
        bool stickyKeysDisabled,
        bool mouseAccelDisabled)
    {
        TweaksStatePanel.Children.Clear();

        AddStateRow("GlobalTimerResolutionRequests",
            globalTimer switch { true => StateKind.Ok, false => StateKind.Off, _ => StateKind.Unknown });

        AddStateRow("Game Bar / DVR",
            gameBar switch
            {
                GameBarDvrState.Disabled => StateKind.Ok,
                GameBarDvrState.Enabled => StateKind.Off,
                GameBarDvrState.Mixed => StateKind.Warn,
                _ => StateKind.Unknown
            });

        AddStateRow("Xbox Gaming Overlay",
            overlayGone switch { true => StateKind.Ok, false => StateKind.Off, _ => StateKind.Unknown });

        AddStateRow("Sticky Keys shortcut", stickyKeysDisabled ? StateKind.Ok : StateKind.Off);
        AddStateRow("Mouse acceleration", mouseAccelDisabled ? StateKind.Ok : StateKind.Off);
    }

    private void AddStateRow(string label, StateKind kind)
    {
        var (dot, text, color) = kind switch
        {
            StateKind.Ok => ("\u25CF", "Active / Disabled", "#3CB97A"),
            StateKind.Off => ("\u25CF", "Inactive / Enabled", "#E05C5C"),
            StateKind.Warn => ("\u25CF", "Partial / Mixed", "#F5A623"),
            _ => ("\u25CF", "Could not verify", "#7A92A8")
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        row.Children.Add(new TextBlock
        {
            Text = dot,
            Foreground = new SolidColorBrush(ColorFromHex(color)),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        });

        row.Children.Add(new TextBlock
        {
            Text = TranslateUiText(label),
            FontSize = 13,
            Foreground = GetBrush("FgBrush", "#D4E4F0"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 220
        });

        row.Children.Add(new TextBlock
        {
            Text = TranslateUiText(text),
            FontSize = 12,
            Foreground = new SolidColorBrush(ColorFromHex(color)),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        TweaksStatePanel.Children.Add(row);
    }

    private static string GetStateLabel(StateKind kind) => kind switch
    {
        StateKind.Ok => "Active / Disabled",
        StateKind.Off => "Inactive / Enabled",
        StateKind.Warn => "Partial / Mixed",
        _ => "Could not verify"
    };
    // Startup and theme settings.

    private void OnStartupOptionsChanged()
    {
        if (_isLoading) return;
        SaveSettingsDebounced();
        App.SetMinimizeToTrayEnabled(StartMinimizedCheck.IsChecked == true);
        ApplyStartupRegistration(showErrors: true);
    }

    private void ApplyStartupRegistration(bool showErrors)
    {
        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Could not open startup registry key.");

            if (StartWithWindowsCheck.IsChecked == true)
            {
                var exe  = ResolveStartupExePath();
                var args = StartMinimizedCheck.IsChecked == true ? " --tray" : string.Empty;
                runKey.SetValue(RunKeyName, $"\"{exe}\"{args}", RegistryValueKind.String);
            }
            else
            {
                runKey.DeleteValue(RunKeyName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            if (showErrors)
                _ = ShowMessageAsync("Warning", $"Failed to update startup: {ex.Message}");
        }
    }

    private static string ResolveStartupExePath()
    {
        var hosted = Path.Combine(AppContext.BaseDirectory, "UtilityGamer.WinUI.exe");
        if (File.Exists(hosted)) return hosted;

        var process = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(process) &&
            !string.Equals(Path.GetFileName(process), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return process;

        return process ?? "UtilityGamer.WinUI.exe";
    }

    /// <summary>
    /// Applies the dark palette to all named brushes defined in XAML resources
    /// and keeps the app in dark mode at all times.
    /// </summary>
    private void ApplyTheme()
    {
        RequestedTheme = ElementTheme.Dark;
        App.SetTitleBarTheme();

        foreach (var (key, hex) in DarkPalette)
            SetBrushColor(key, ColorFromHex(hex));
    }

    // Settings persistence.

    private void LoadSettings()
    {
        using var _ = BeginLoading();

        var settings = TryReadSettings();
        var defaults = new AppSettings();

        _defaultGameWidth    = Clamp(settings.GameWidth,    MinResolutionWidth,  MaxResolutionWidth,  defaults.GameWidth);
        _defaultGameHeight   = Clamp(settings.GameHeight,   MinResolutionHeight, MaxResolutionHeight, defaults.GameHeight);
        _defaultGameHz       = Clamp(settings.GameHz,       MinRefreshRateHz,     MaxRefreshRateHz,     defaults.GameHz);
        _defaultReturnWidth  = Clamp(settings.DefaultWidth,  MinResolutionWidth,  MaxResolutionWidth,  defaults.DefaultWidth);
        _defaultReturnHeight = Clamp(settings.DefaultHeight, MinResolutionHeight, MaxResolutionHeight, defaults.DefaultHeight);
        _defaultReturnHz     = Clamp(settings.DefaultHz,     MinRefreshRateHz,     MaxRefreshRateHz,     defaults.DefaultHz);
        _defaultResolutionSwitchEnabled = settings.DefaultResolutionSwitchEnabled;

        _games.Clear();
        var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in settings.Games ?? [])
        {
            NormalizeGameProfile(game);
            if (string.IsNullOrWhiteSpace(game.ProcessName) || !seenProcesses.Add(game.ProcessName))
                continue;

            UpdateGamePowerPlanDisplayName(game);
            _games.Add(game);
        }

        SetSpinner(GameWidthBox,    _defaultGameWidth);
        SetSpinner(GameHeightBox,   _defaultGameHeight);
        SetSpinner(GameHzBox,       _defaultGameHz);
        SetSpinner(DefaultWidthBox, _defaultReturnWidth);
        SetSpinner(DefaultHeightBox,_defaultReturnHeight);
        SetSpinner(DefaultHzBox,    _defaultReturnHz);

        GlobalTimerCheck.IsChecked       = settings.GlobalTimerEnabled;
        DisableGameBarCheck.IsChecked    = settings.GameBarDisabled;
        RemoveOverlayCheck.IsChecked     = settings.RemoveXboxOverlay;
        StickyKeysCheck.IsChecked       = settings.StickyKeysDisabled;
        MouseAccelCheck.IsChecked       = settings.MouseAccelDisabled;
        StartWithWindowsCheck.IsChecked  = settings.StartWithWindows;
        StartMinimizedCheck.IsChecked    = settings.StartMinimizedToTray;
        NotifyOnResolutionChangeCheck.IsChecked = settings.NotifyOnResolutionChange;
        NotifyOnPowerPlanChangeCheck.IsChecked = settings.NotifyOnPowerPlanChange;
        SelectLanguage(settings.Language);

        if (_games.Count > 0)
        {
            var savedGame = string.IsNullOrWhiteSpace(settings.SelectedGameName)
                ? null
                : _games.FirstOrDefault(g =>
                    string.Equals(g.ProcessName, settings.SelectedGameName, StringComparison.OrdinalIgnoreCase));

            GamesListView.SelectedItem = savedGame ?? _games[0];
        }

        if (settings.SelectedTabIndex > 0
            && settings.SelectedTabIndex < MainTabView.TabItems.Count)
        {
            MainTabView.SelectedIndex = settings.SelectedTabIndex;
        }

        ApplyStartupRegistration(showErrors: false);
        UpdateTweaksStatePanel(
            _lastGlobalTimerState,
            _lastGameBarState,
            _lastOverlayRemovedState,
            _lastStickyKeysDisabledState ?? false,
            _lastMouseAccelDisabledState ?? false);
    }

    private AppSettings TryReadSettings()
    {
        _settingsCorrupted = false;
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            _settingsCorrupted = true;
            return new AppSettings();
        }
        catch
        {
            // Return defaults on transient I/O failures.
            return new AppSettings();
        }
    }

    private void SaveSettingsIfReady()
    {
        if (!_isLoading) SaveSettingsDebounced();
    }

    private async void SaveSettingsDebounced()
    {
        CancelAndDispose(ref _saveCts);
        var saveCts = new CancellationTokenSource();
        _saveCts = saveCts;

        try
        {
            await Task.Delay(500, saveCts.Token);
            if (ReferenceEquals(_saveCts, saveCts))
                SaveSettings();
        }
        catch (OperationCanceledException)
        {
            // A newer save request superseded this one.
        }
        finally
        {
            if (ReferenceEquals(_saveCts, saveCts))
            {
                _saveCts = null;
                saveCts.Dispose();
            }
        }
    }

    private void SaveSettings()
    {
        CancelAndDispose(ref _saveCts);
        if (_isLoading) return;

        var settings = new AppSettings
        {
            Games              = CreateSanitizedGameSnapshot(),
            GameWidth          = _defaultGameWidth,
            GameHeight         = _defaultGameHeight,
            GameHz             = _defaultGameHz,
            DefaultWidth       = _defaultReturnWidth,
            DefaultHeight      = _defaultReturnHeight,
            DefaultHz          = _defaultReturnHz,
            DefaultResolutionSwitchEnabled = _defaultResolutionSwitchEnabled,
            GlobalTimerEnabled = GlobalTimerCheck.IsChecked == true,
            GameBarDisabled    = DisableGameBarCheck.IsChecked == true,
            RemoveXboxOverlay  = RemoveOverlayCheck.IsChecked == true,
            StickyKeysDisabled = StickyKeysCheck.IsChecked == true,
            MouseAccelDisabled = MouseAccelCheck.IsChecked == true,
            StartWithWindows   = StartWithWindowsCheck.IsChecked == true,
            StartMinimizedToTray = StartMinimizedCheck.IsChecked == true,
            Language           = GetSelectedLanguageCode(),
            DarkTheme          = true,
            NotifyOnResolutionChange = NotifyOnResolutionChangeCheck.IsChecked == true,
            NotifyOnPowerPlanChange = NotifyOnPowerPlanChangeCheck.IsChecked == true,
            SelectedTabIndex = MainTabView.SelectedIndex,
            SelectedGameName = (GamesListView.SelectedItem as GameProfile)?.ProcessName ?? string.Empty
        };

        var appWindow = App.MainWindow?.AppWindow;
        if (appWindow is not null)
        {
            settings.WindowWidth = appWindow.Size.Width;
            settings.WindowHeight = appWindow.Size.Height;
            settings.WindowLeft = appWindow.Position.X;
            settings.WindowTop = appWindow.Position.Y;
        }

        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(_settingsPath,
                JsonSerializer.Serialize(settings, JsonWriteOptions),
                Encoding.UTF8);
        }
        catch
        {
            // Writing settings is best-effort; ignore transient I/O failures.
        }
    }

    // Helpers.

    private List<GameProfile> CreateSanitizedGameSnapshot()
    {
        var result = new List<GameProfile>(_games.Count);
        var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in _games)
        {
            var clone = CloneGameProfile(game);
            NormalizeGameProfile(clone);
            if (string.IsNullOrWhiteSpace(clone.ProcessName) || !seenProcesses.Add(clone.ProcessName))
                continue;

            result.Add(clone);
        }

        return result;
    }

    private static GameProfile CloneGameProfile(GameProfile game) => new()
    {
        Name = game.Name,
        ExePath = game.ExePath,
        ProcessName = game.ProcessName,
        GameWidth = game.GameWidth,
        GameHeight = game.GameHeight,
        GameHz = game.GameHz,
        DefaultWidth = game.DefaultWidth,
        DefaultHeight = game.DefaultHeight,
        DefaultHz = game.DefaultHz,
        ResolutionSwitchEnabled = game.ResolutionSwitchEnabled,
        MonitorDeviceName = game.MonitorDeviceName,
        PowerPlanGuid = game.PowerPlanGuid,
        PowerPlanDisplayName = game.PowerPlanDisplayName
    };

    private static IReadOnlyDictionary<string, string> BuildUiReverseMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (english, localized) in UiTexts)
        {
            AddUiVariant(map, english, english);
            AddUiVariant(map, localized.PtBr, english);
            AddUiVariant(map, localized.LegacyEs, english);
        }

        return map;
    }

    private static void AddUiVariant(Dictionary<string, string> map, string variant, string canonicalKey)
    {
        if (string.IsNullOrWhiteSpace(variant)) return;

        map[variant] = canonicalKey;
        if (TrySplitDecoratedPrefix(variant, out _, out var bare) && !string.IsNullOrWhiteSpace(bare))
            map[bare] = canonicalKey;
    }

    private void ApplyLanguage()
    {
        using var _ = BeginLoading();

        var languageCode = GetSelectedLanguageCode();
        SelectLanguage(languageCode);

        App.SetUiLanguage(languageCode);

        ApplyLanguageRecursive(this);
        ApplyLocalizedTooltips();
        PopulateMonitorCombo();
        PopulatePowerPlanCombo();
        UpdateResolutionEditorFromSelection();
        MainTabView.UpdateLayout();
        GamesListView.UpdateLayout();
        ApplyLanguageRecursive(GamesListView);
        ApplyLanguageRecursive(this);

        var currentStatus = StatusText.Text;
        var separator = currentStatus.IndexOf(':');
        var statusBody = separator >= 0 && separator + 1 < currentStatus.Length
            ? currentStatus[(separator + 1)..].TrimStart()
            : currentStatus;
        UpdateStatus(statusBody);
    }

    private void ApplyLocalizedTooltips()
    {
        SetTooltip(ResolutionSwitchCheck,
            "Automatically changes the desktop resolution when the selected app starts and restores the previous values when it closes.");
        SetTooltip(MonitorCombo,
            "Choose which monitor should receive the desktop resolution profile for this app.");
        SetTooltip(PowerPlanCombo,
            "Select the power plan to activate when this app starts.");
        SetTooltip(GlobalTimerCheck,
            "Enables Windows to honour finer timer resolution requests made by games and low-latency apps.");
        SetTooltip(DisableGameBarCheck,
            "Disables Game Bar capture and background DVR services that can interfere with performance.");
        SetTooltip(RemoveOverlayCheck,
            "Removes the Xbox Gaming Overlay package from Windows for a cleaner gaming session.");
        SetTooltip(StickyKeysCheck,
            "Prevents the Sticky Keys shortcut from appearing when you press Shift five times.");
        SetTooltip(MouseAccelCheck,
            "Turns off Enhance Pointer Precision so mouse movement stays raw and predictable in games.");
        SetTooltip(ApplyTweaksButton,
            "Applies every selected tweak immediately. Administrator rights may be required.");
        SetTooltip(CheckTweaksButton,
            "Reads the current Windows state again and refreshes the status list on the right.");
        SetTooltip(RunSfcButton,
            "Runs the System File Checker to repair corrupted protected Windows files.");
        SetTooltip(RunDismButton,
            "Runs DISM to repair the Windows image used by servicing and SFC.");
        SetTooltip(StartWithWindowsCheck,
            "Adds Utility Gamer to Windows startup.");
        SetTooltip(StartMinimizedCheck,
            "Starts the app minimized in the notification area instead of opening the full window.");
        SetTooltip(LanguageCombo,
            "Chooses the app language. Spanish has been removed; only Portuguese and English remain.");
        SetTooltip(NotifyOnResolutionChangeCheck,
            "Shows a notification when Utility Gamer changes the desktop resolution.");
        SetTooltip(NotifyOnPowerPlanChangeCheck,
            "Shows a notification when Utility Gamer changes the active power plan.");
        SetTooltip(MonitoringToggle,
            "Turns background monitoring on or off for the tracked games list.");
    }

    private void SetTooltip(FrameworkElement element, string text)
        => ToolTipService.SetToolTip(element, TranslateUiText(text));

    private string GetSelectedLanguageCode()
    {
        if (LanguageCombo.SelectedItem is LanguageOption option)
            return NormalizeLanguageCode(option.Code);

        return DefaultLanguageCode;
    }

    private void SelectLanguage(string? languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);

        for (var i = 0; i < SupportedLanguages.Length; i++)
        {
            var option = SupportedLanguages[i];
            if (string.Equals(option.Code, normalized, StringComparison.OrdinalIgnoreCase))
            {
                LanguageCombo.SelectedIndex = i;
                return;
            }
        }

        LanguageCombo.SelectedIndex = 0;
    }

    private void ApplyLanguageRecursive(DependencyObject node)
    {
        switch (node)
        {
            case TabViewItem tab when tab.Header is string header:
                tab.Header = TranslateUiText(header);
                break;

            case Button button when button.Content is string content:
                button.Content = TranslateUiText(content);
                break;

            case CheckBox checkbox when checkbox.Content is string content:
                checkbox.Content = TranslateUiText(content);
                break;

            case TextBlock textBlock:
                if (!ReferenceEquals(textBlock, StatusText)
                    && !ReferenceEquals(textBlock, SelectedGameText))
                {
                    textBlock.Text = TranslateUiText(textBlock.Text);
                }
                break;
        }

        var children = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < children; i++)
            ApplyLanguageRecursive(VisualTreeHelper.GetChild(node, i));
    }

    private string TranslateUiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var languageCode = GetSelectedLanguageCode();

        if (UiTextToKey.TryGetValue(text, out var key))
            return TranslateCanonical(key, languageCode);

        if (TrySplitDecoratedPrefix(text, out var prefix, out var bare)
            && UiTextToKey.TryGetValue(bare, out key))
        {
            return prefix + TranslateCanonical(key, languageCode);
        }

        return text;
    }

    private static bool TrySplitDecoratedPrefix(string text, out string prefix, out string bare)
    {
        prefix = string.Empty;
        bare = text;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var index = 0;
        while (index < text.Length && !char.IsLetterOrDigit(text[index]))
            index++;

        if (index <= 0 || index >= text.Length) return false;

        prefix = text[..index];
        bare = text[index..];
        return true;
    }

    private static string TranslateCanonical(string canonicalKey, string languageCode)
    {
        if (!UiTexts.TryGetValue(canonicalKey, out var localized))
            return canonicalKey;

        if (languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return localized.PtBr;

        return canonicalKey;
    }

    private void UpdateStatus(string text)
    {
        var statusLabel = TranslateUiText("Status");
        StatusText.Text = $"{statusLabel}: {TranslateStatusMessage(text)}";
    }

    private string TranslateStatusMessage(string text)
    {
        var translated = TranslateUiText(text);
        if (!string.Equals(translated, text, StringComparison.Ordinal))
            return translated;

        if (!GetSelectedLanguageCode().StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return text;

        const string monitoringPrefix = "Monitoring started for ";
        const string monitoringSuffix = " game(s).";

        if (text.StartsWith(monitoringPrefix, StringComparison.Ordinal)
            && text.EndsWith(monitoringSuffix, StringComparison.Ordinal))
        {
            var count = text[monitoringPrefix.Length..^monitoringSuffix.Length];
            return $"Monitoramento iniciado para {count} aplicativo(s).";
        }

        return text;
    }

    private string GetRevertCountdownText(int seconds)
        => GetSelectedLanguageCode().StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? $"Restaurando em {seconds}s..."
            : $"Reverting in {seconds}s...";

    private void RefreshGamePowerPlanDisplayNames()
    {
        foreach (var game in _games)
            UpdateGamePowerPlanDisplayName(game);
    }

    private void UpdateGamePowerPlanDisplayName(GameProfile game)
    {
        if (!game.PowerPlanGuid.HasValue)
        {
            game.PowerPlanDisplayName = null;
            return;
        }

        game.PowerPlanDisplayName = _powerPlans
            .FirstOrDefault(p => p.Guid == game.PowerPlanGuid.Value)
            ?.Name;
    }

    private static int FindIndex<T>(IReadOnlyList<T> source, Func<T, bool> predicate)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (predicate(source[i]))
                return i;
        }

        return -1;
    }

    private static string GetPowerShellPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return DefaultLanguageCode;

        if (languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";

        if (languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ||
            languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        return DefaultLanguageCode;
    }

    private static bool TryNormalizeExecutablePath(
        string? path,
        out string normalizedPath,
        out string validationMessage)
    {
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            validationMessage = "Select a valid .exe file.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            validationMessage = $"The selected executable path is invalid: {ex.Message}";
            return false;
        }

        if (!File.Exists(normalizedPath))
        {
            validationMessage = "The selected executable no longer exists.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(normalizedPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            validationMessage = "Select a valid .exe file.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }

    private async Task<string> ApplyGlobalTimerTweakAsync(bool enabled)
    {
        var result = await RunAsync("reg.exe",
            "add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel\" " +
            $"/v GlobalTimerResolutionRequests /t REG_DWORD /d {(enabled ? 1 : 0)} /f");

        if (result.Success)
        {
            return enabled
                ? "GlobalTimerResolutionRequests enabled."
                : "GlobalTimerResolutionRequests set to 0.";
        }

        return enabled
            ? $"Failed to enable GlobalTimerResolutionRequests: {result.Output}"
            : $"Failed to disable GlobalTimerResolutionRequests: {result.Output}";
    }

    private async Task<string> ApplyGameBarDvrTweakAsync(bool disable)
    {
        var commands = disable
            ? new[]
            {
                "add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 0 /f",
                "add \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehaviorMode /t REG_DWORD /d 2 /f",
                "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v AppCaptureEnabled /t REG_DWORD /d 0 /f",
                "add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR\" /v AllowGameDVR /t REG_DWORD /d 0 /f"
            }
            : new[]
            {
                "add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 1 /f",
                "add \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehaviorMode /t REG_DWORD /d 0 /f",
                "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v AppCaptureEnabled /t REG_DWORD /d 1 /f",
                "delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR\" /f"
            };

        foreach (var command in commands)
        {
            var result = await RunAsync("reg.exe", command);
            if (!result.Success)
                return $"Game Bar / DVR failed: {result.Output}";
        }

        return disable ? "Game Bar / DVR disabled." : "Game Bar / DVR restored.";
    }

    private static string ResolveExecutablePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
            return fileName;

        var systemPath = GetSystemToolPath(fileName);
        return File.Exists(systemPath) ? systemPath : fileName;
    }

    private async Task<CommandResult> RunAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = ResolveExecutablePath(fileName),
                Arguments              = arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            }
        };

        if (!process.Start()) return new CommandResult(-1, "Failed to start process.");

        var output = await ProcessOutputHelper.ReadCombinedOutputAsync(process);
        return new CommandResult(process.ExitCode, output);
    }

    private async Task ShowMessageAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title           = title,
            Content         = content,
            CloseButtonText = "OK",
            XamlRoot        = XamlRoot
        };
        await dialog.ShowAsync();
    }

    // Theme utilities.

    private static Color ColorFromHex(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length != 6) return Colors.Transparent;
        return Color.FromArgb(255,
            Convert.ToByte(s[0..2], 16),
            Convert.ToByte(s[2..4], 16),
            Convert.ToByte(s[4..6], 16));
    }

    private static void SetBrushColor(string key, Color color)
    {
        // Theme brushes are declared in the app-level resource dictionary.
        if (Application.Current.Resources.TryGetValue(key, out var res) && res is SolidColorBrush b)
            b.Color = color;
    }

    private static Brush GetBrush(string resourceKey, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            && resource is SolidColorBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(ColorFromHex(fallbackHex));
    }

    // Loading scope helpers.

    /// <summary>Sets <see cref="_isLoading"/> = true and resets it on dispose.</summary>
    private LoadingScope BeginLoading()
    {
        _isLoading = true;
        return new LoadingScope(() => _isLoading = false);
    }

    /// <summary>Sets <see cref="_isChangingSelection"/> = true and resets it on dispose.</summary>
    private LoadingScope BeginChangingSelection()
    {
        _isChangingSelection = true;
        return new LoadingScope(() => _isChangingSelection = false);
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
    {
        var current = cancellationTokenSource;
        cancellationTokenSource = null;

        if (current is null)
            return;

        try
        {
            current.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        current.Dispose();
    }

    private readonly struct LoadingScope(Action reset) : IDisposable
    {
        public void Dispose() => reset();
    }

    // Nested types.

    private enum StateKind { Ok, Off, Warn, Unknown }

    private enum GameBarDvrState { Disabled, Enabled, Mixed, Unknown }

    private enum RepairKind { Sfc, Dism }

    private readonly record struct CommandResult(int ExitCode, string Output)
    {
        public bool Success => ExitCode == 0;
    }
}























