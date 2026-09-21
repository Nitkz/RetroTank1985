using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using RetroTank1985.Client.Components.Coop;
using RetroTank1985.Client.Services;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Client.Pages;

public partial class Coop : IAsyncDisposable
{
    [Inject] private CoopLobbyClientService LobbyService { get; set; } = default!;
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

    protected override void OnInitialized()
    {
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

    private void OnRoomUpdated(CoopRoomInfo room)
    {
        InvokeAsync(StateHasChanged);
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
            var result = await LobbyService.CreateRoomAsync(PlayerName, SelectedInitialStage);
            if (!result.Success)
            {
                Snackbar.Add(result.Message ?? "Failed to create room", Severity.Error);
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
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", LobbyService.CurrentRoom.RoomCode);
        Snackbar.Add($"Room Code '{LobbyService.CurrentRoom.RoomCode}' copied to clipboard!", Severity.Info);
    }

    private async Task ShareInviteLink()
    {
        if (LobbyService.CurrentRoom == null) return;
        var inviteUrl = NavigationManager.ToAbsoluteUri($"/coop?room={LobbyService.CurrentRoom.RoomCode}").ToString();
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", inviteUrl);
        Snackbar.Add("Invite URL copied to clipboard! Share it with your teammate.", Severity.Success);
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
