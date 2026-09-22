using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using RetroTank1985.Client.Components.Coop;
using RetroTank1985.Client.Helpers;
using RetroTank1985.Client.Services;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Client.Pages;

public partial class Coop : IAsyncDisposable
{
    [Inject] private CoopLobbyClientService LobbyService { get; set; } = default!;
    [Inject] private GameStorageService StorageService { get; set; } = default!;
    [Inject] private GameEngineService EngineService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "room")]
    public string? QueryRoomCode { get; set; }

    private string PlayerName { get; set; } = "Player";
    private string InputRoomCode { get; set; } = string.Empty;
    private int SelectedInitialStage { get; set; } = 1;
    private bool IsLoading { get; set; }
    private GameSettings _settings = new();

    protected override async Task OnInitializedAsync()
    {
        _settings = await StorageService.GetGameSettingsAsync();
        LobbyService.OnRoomStateChanged += OnRoomUpdated;
        LobbyService.OnErrorOccurred += OnError;
        LobbyService.OnGameStartingEvent += OnGameStarting;

        // Random default player name
        var randomId = new Random().Next(100, 999);
        PlayerName = $"Tank_{randomId}";

        if (!string.IsNullOrWhiteSpace(QueryRoomCode))
        {
            InputRoomCode = QueryRoomCode.Trim().ToUpperInvariant();
        }
    }

    private async void OnRoomUpdated(CoopRoomInfo room)
    {
        // หากเป็น Guest หรือมีการอัปเดต Settings จาก Host ให้ sync settings กับ Engine และ UI
        if (room.Settings != null)
        {
            _settings = room.Settings;
            if (LobbyService.IsGuest)
            {
                await EngineService.ApplySettingsAsync(room.Settings);
            }
        }
        await InvokeAsync(StateHasChanged);
    }

    private void OnError(string message)
    {
        Snackbar.Add(message, Severity.Error);
        IsLoading = false;
        InvokeAsync(StateHasChanged);
    }

    private void OnGameStarting(CoopRoomInfo room)
    {
        Snackbar.Add("Mission starting! Loading arena...", Severity.Success);
        // Navigate to arcade coop session
        NavigationManager.NavigateTo($"/arcade?coop=1&stage={room.SelectedStage}&role={(LobbyService.IsHost ? "p1" : "p2")}&room={room.RoomCode}");
    }

    private async Task HandleCreateRoom()
    {
        IsLoading = true;
        try
        {
            var result = await LobbyService.CreateRoomAsync(PlayerName, SelectedInitialStage, _settings.Preset.ToString(), _settings);
            if (!result.Success)
            {
                if (result.Message == "SERVER_FULL")
                {
                    var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall };
                    await DialogService.ShowAsync<ServerFullDialog>("Server Full", options);
                }
                else
                {
                    Snackbar.Add(result.Message ?? "Failed to create room", Severity.Error);
                }
            }
            else
            {
                Snackbar.Add($"Room {result.Room?.RoomCode} created successfully!", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Connection error: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task HandleJoinRoom()
    {
        if (string.IsNullOrWhiteSpace(InputRoomCode)) return;

        IsLoading = true;
        try
        {
            var result = await LobbyService.JoinRoomAsync(InputRoomCode, PlayerName);
            if (!result.Success)
            {
                Snackbar.Add(result.Message ?? "Failed to join room", Severity.Error);
            }
            else
            {
                Snackbar.Add($"Joined room {result.Room?.RoomCode} as Player 2!", Severity.Success);
                if (result.Room != null && result.Room.State == RetroTank1985.Shared.Enums.CoopRoomState.InGame)
                {
                    Snackbar.Add("Resuming ongoing mission...", Severity.Info);
                    NavigationManager.NavigateTo($"/arcade?coop=1&stage={result.Room.SelectedStage}&role=p2&room={result.Room.RoomCode}");
                }
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Connection error: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CopyRoomCode()
    {
        if (LobbyService.CurrentRoom == null) return;
        var code = LobbyService.CurrentRoom.RoomCode;
        try
        {
            var success = await JS.InvokeAsync<bool>("GameBridge.copyText", code);
            if (success)
            {
                Snackbar.Add($"Room Code '{code}' copied to clipboard!", Severity.Info);
            }
            else
            {
                Snackbar.Add($"Room Code: {code}", Severity.Info);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CopyRoomCode] Interop error: {ex.Message}");
            Snackbar.Add($"Room Code: {code}", Severity.Info);
        }
    }

    private class ShareResult
    {
        public bool Success { get; set; }
        public string? Method { get; set; }
    }

    private async Task ShareInviteLink()
    {
        if (LobbyService.CurrentRoom == null) return;
        var inviteUrl = NavigationManager.ToAbsoluteUri($"/coop?room={LobbyService.CurrentRoom.RoomCode}").ToString();
        try
        {
            var result = await JS.InvokeAsync<ShareResult>("GameBridge.shareLink", "RetroTank 1985 Co-Op", $"Join my room: {LobbyService.CurrentRoom.RoomCode}", inviteUrl);
            if (result != null && result.Success)
            {
                if (result.Method == "share")
                {
                    Snackbar.Add("Invite link shared!", Severity.Success);
                }
                else if (result.Method == "copy")
                {
                    Snackbar.Add("Invite URL copied to clipboard! Share it with your teammate.", Severity.Success);
                }
            }
            else
            {
                Snackbar.Add($"Invite URL: {inviteUrl}", Severity.Info);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShareInviteLink] Interop error: {ex.Message}");
            Snackbar.Add("Invite URL ready. You can also share the QR Code!", Severity.Info);
        }
    }

    private async Task ShowQrDialog()
    {
        if (LobbyService.CurrentRoom == null) return;
        var inviteUrl = NavigationManager.ToAbsoluteUri($"/coop?room={LobbyService.CurrentRoom.RoomCode}").ToString();
        var parameters = new DialogParameters<QrCodeDialog>
        {
            { x => x.RoomCode, LobbyService.CurrentRoom.RoomCode },
            { x => x.InviteUrl, inviteUrl }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall };
        await DialogService.ShowAsync<QrCodeDialog>("Share QR Code", parameters, options);
    }

    private async Task ChangeStage(int stage)
    {
        await LobbyService.ChangeStageAsync(stage);
    }

    private async Task ToggleReady(bool isReady)
    {
        await LobbyService.SetReadyAsync(isReady);
    }

    private async Task StartGame()
    {
        var result = await LobbyService.StartGameAsync();
        if (!result.Success)
        {
            Snackbar.Add(result.Message ?? "Cannot start game", Severity.Warning);
        }
    }

    private void ResumeGame()
    {
        if (LobbyService.CurrentRoom != null)
        {
            var room = LobbyService.CurrentRoom;
            Snackbar.Add("Returning to game...", Severity.Info);
            NavigationManager.NavigateTo($"/arcade?coop=1&stage={room.SelectedStage}&role={(LobbyService.IsHost ? "p1" : "p2")}&room={room.RoomCode}");
        }
    }

    private async Task OpenOptionsDialog()
    {
        var parameters = new DialogParameters<RetroTank1985.Client.Components.Arcade.GameOptionsDialog>
        {
            { x => x.InitialSettings, _settings }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<RetroTank1985.Client.Components.Arcade.GameOptionsDialog>("CO-OP GAME OPTIONS", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data is GameSettings newSettings)
        {
            _settings = newSettings;
            await StorageService.SaveGameSettingsAsync(newSettings);
            await EngineService.ApplySettingsAsync(newSettings);

            if (LobbyService.IsHost && LobbyService.CurrentRoom != null)
            {
                await LobbyService.ChangeGameSettingsAsync(newSettings);
            }

            Snackbar.Add($"Difficulty preset updated: {GameUiHelper.GetDifficultyLabel(newSettings.Preset)}", Severity.Info);
            StateHasChanged();
        }
    }

    private async Task LeaveRoom()
    {
        await LobbyService.LeaveRoomAsync();
        Snackbar.Add("Left the room", Severity.Normal);
    }

    public ValueTask DisposeAsync()
    {
        LobbyService.OnRoomStateChanged -= OnRoomUpdated;
        LobbyService.OnErrorOccurred -= OnError;
        LobbyService.OnGameStartingEvent -= OnGameStarting;
        return ValueTask.CompletedTask;
    }
}
