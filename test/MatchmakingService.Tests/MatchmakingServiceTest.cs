using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.Contracts;
using MatchmakingService.Exceptions;
using MatchmakingService.Models;
using MatchmakingService.Registry;
using MatchmakingService.Services;
using MatchmakingService.Stores;
using Microsoft.Extensions.Options;
using Xunit;

namespace MatchmakingService.Tests;

public class MatchmakingServiceTests
{
    private static Services.MatchmakingService CreateService(
        IGameServiceClient? gameServiceClient = null,
        IGameServiceRegistry? gameServiceRegistry = null)
    {
        var options = Options.Create(
            new MatchmakingOptions
            {
                MinPlayersPerMatch = 2,
                MaxPlayersPerMatch = 5,
                CountdownSeconds = 0
            }
        );

        return new Services.MatchmakingService(
            new InMemoryMatchmakingStore(),

            gameServiceClient ??
            new FakeGameServiceClient(),

            gameServiceRegistry ??
            new FakeGameServiceRegistry(),

            options,
            
            new FakeMatchmakingMetrics()
        );
    }
    [Fact]
    public async Task ProcessWaitingRooms_Should_Keep_Players_Waiting_When_No_GameService_Is_Available()
    {
        var registry =
            new FakeGameServiceRegistry(
                hasAvailableInstance: false
            );

        var service = CreateService(
            gameServiceRegistry: registry
        );

        await service.JoinQueueAsync(
            new JoinQueueRequest
            {
                PlayerId = "player-001"
            }
        );

        await service.JoinQueueAsync(
            new JoinQueueRequest
            {
                PlayerId = "player-002"
            }
        );

        var processResponse =
            await service.ProcessWaitingRoomsAsync();

        var rooms = service.GetWaitingRooms();

        Assert.Equal(
            0,
            processResponse.MatchesCreated
        );

        Assert.Single(rooms);

        Assert.Equal(
            2,
            rooms.First().CurrentPlayers
        );

        Assert.Equal(
            WaitingRoomStatus.CountdownStarted
                .ToString(),

            rooms.First().Status
        );
    }
    
    [Fact]
    public async Task ProcessWaitingRooms_Should_Save_Match_Route()
    {
        var registry =
            new FakeGameServiceRegistry();

        var service = CreateService(
            gameServiceRegistry: registry
        );

        await service.JoinQueueAsync(
            new JoinQueueRequest
            {
                PlayerId = "player-001"
            }
        );

        await service.JoinQueueAsync(
            new JoinQueueRequest
            {
                PlayerId = "player-002"
            }
        );

        await service.ProcessWaitingRoomsAsync();

        var route = registry.GetMatchRoute(
            "match-test"
        );

        Assert.NotNull(route);

        Assert.Equal(
            "match-test",
            route.MatchId
        );

        Assert.Equal(
            "game-service-local",
            route.GameServiceId
        );

        Assert.Equal(
            "http://localhost:5002",
            route.InternalUrl
        );
    }

    [Fact]
    public async Task JoinQueue_Should_Add_Player_To_Queue_When_Not_Enough_Players()
    {
        var service = CreateService();

        var response = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        Assert.Equal(QueueStatus.Queued.ToString(), response.Status);
        Assert.Null(response.MatchId);
    }

    [Fact]
    public async Task ProcessWaitingRooms_Should_Create_Match_When_Countdown_Has_Expired()
    {
        var service = CreateService();
    
        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });
    
        var secondJoinResponse = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });
    
        Assert.Equal(QueueStatus.Queued.ToString(), secondJoinResponse.Status);
    
        var processResponse = await service.ProcessWaitingRoomsAsync();
    
        var player002Status = service.GetStatus("player-002");
    
        Assert.Equal(1, processResponse.MatchesCreated);
        Assert.Equal(QueueStatus.Assigned.ToString(), player002Status.Status);
        Assert.Equal("match-test", player002Status.MatchId);
        Assert.Equal("game-service-local", player002Status.GameServiceId);
        Assert.Equal(PlayerRole.Animatronic.ToString(), player002Status.Role);
    }

    [Fact]
    public async Task GetStatus_Should_Return_Assignment_For_First_Player_After_Room_Is_Processed()
    {
        var service = CreateService();

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });

        await service.ProcessWaitingRoomsAsync();

        var status = service.GetStatus("player-001");

        Assert.Equal(QueueStatus.Assigned.ToString(), status.Status);
        Assert.Equal("match-test", status.MatchId);
        Assert.Equal(PlayerRole.Guard.ToString(), status.Role);
    }

    [Fact]
    public async Task JoinQueue_Should_Not_Add_Same_Player_Twice()
    {
        var service = CreateService();

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        var response = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        var rooms = service.GetWaitingRooms();

        Assert.Equal(QueueStatus.Queued.ToString(), response.Status);
        Assert.Single(rooms);
        Assert.Equal(1, rooms.First().CurrentPlayers);
    }

    [Fact]
    public async Task JoinQueue_Should_Return_Assignment_If_Player_Is_Already_Assigned()
    {
        var service = CreateService();

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });

        await service.ProcessWaitingRoomsAsync();

        var response = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        Assert.Equal(QueueStatus.Assigned.ToString(), response.Status);
        Assert.Equal("match-test", response.MatchId);
        Assert.Equal(PlayerRole.Guard.ToString(), response.Role);
    }

    [Fact]
    public async Task ProcessWaitingRooms_Should_Keep_Players_Waiting_When_GameService_Fails()
    {
        var service = CreateService(new FakeGameServiceClient(shouldFail: true));

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });

        var processResponse = await service.ProcessWaitingRoomsAsync();

        var rooms = service.GetWaitingRooms();

        Assert.Equal(0, processResponse.MatchesCreated);
        Assert.Single(rooms);
        Assert.Equal(2, rooms.First().CurrentPlayers);
        Assert.Equal(WaitingRoomStatus.CountdownStarted.ToString(), rooms.First().Status);
    }
    [Fact]
    public async Task JoinQueue_Should_Add_Third_Player_To_Same_Waiting_Room_Before_Match_Is_Created()
    {
        var service = CreateService();

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });

        var thirdResponse = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-003"
        });

        var rooms = service.GetWaitingRooms();

        Assert.Equal(QueueStatus.Queued.ToString(), thirdResponse.Status);
        Assert.Single(rooms);
        Assert.Equal(3, rooms.First().CurrentPlayers);
        Assert.Equal(WaitingRoomStatus.CountdownStarted.ToString(), rooms.First().Status);
    }
    [Fact]
    public async Task JoinRoomByCode_Should_Add_Player_To_Existing_Room()
    {
        var service = CreateService();

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        var room = Assert.Single(service.GetWaitingRooms());

        var response = await service.JoinRoomByCodeAsync(
            new JoinRoomByCodeRequest
            {
                PlayerId = "player-002",
                InviteCode = room.InviteCode
            }
        );

        var updatedRoom = Assert.Single(service.GetWaitingRooms());

        Assert.Equal(QueueStatus.Queued.ToString(), response.Status);
        Assert.Equal(2, updatedRoom.CurrentPlayers);
        Assert.Equal(
            WaitingRoomStatus.CountdownStarted.ToString(),
            updatedRoom.Status
        );
    }
    [Fact]
    public async Task JoinRoomByCode_Should_Throw_When_Code_Does_Not_Exist()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<WaitingRoomNotFoundException>(() =>
            service.JoinRoomByCodeAsync(
                new JoinRoomByCodeRequest
                {
                    PlayerId = "player-002",
                    InviteCode = "INVALID"
                }
            )
        );
    }
}