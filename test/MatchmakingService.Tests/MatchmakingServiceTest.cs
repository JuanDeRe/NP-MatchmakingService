using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.Contracts;
using MatchmakingService.Models;
using MatchmakingService.Services;
using MatchmakingService.Stores;
using Microsoft.Extensions.Options;
using Xunit;

namespace MatchmakingService.Tests;

public class MatchmakingServiceTests
{
    private static Services.MatchmakingService CreateService(
        IGameServiceClient? gameServiceClient = null)
    {
        var options = Options.Create(new MatchmakingOptions
        {
            MinPlayersPerMatch = 2,
            MaxPlayersPerMatch = 5
        });

        return new Services.MatchmakingService(
            new InMemoryMatchmakingStore(),
            gameServiceClient ?? new FakeGameServiceClient(),
            options
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
    public async Task JoinQueue_Should_Create_Match_When_Enough_Players()
    {
        var service = CreateService();

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        var response = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });

        Assert.Equal(QueueStatus.Assigned.ToString(), response.Status);
        Assert.Equal("match-test", response.MatchId);
        Assert.Equal("game-service-local", response.GameServiceId);
        Assert.Equal(PlayerRole.Animatronic.ToString(), response.Role);
    }

    [Fact]
    public async Task GetStatus_Should_Return_Assignment_For_First_Player()
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

        var queue = service.GetQueue();

        Assert.Equal(QueueStatus.Queued.ToString(), response.Status);
        Assert.Single(queue);
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

        var response = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        Assert.Equal(QueueStatus.Assigned.ToString(), response.Status);
        Assert.Equal("match-test", response.MatchId);
        Assert.Equal(PlayerRole.Guard.ToString(), response.Role);
    }

    [Fact]
    public async Task JoinQueue_Should_Return_Players_To_Queue_When_GameService_Fails()
    {
        var service = CreateService(new FakeGameServiceClient(shouldFail: true));

        await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-001"
        });

        var response = await service.JoinQueueAsync(new JoinQueueRequest
        {
            PlayerId = "player-002"
        });

        var queue = service.GetQueue();

        Assert.Equal(QueueStatus.Queued.ToString(), response.Status);
        Assert.Equal(2, queue.Count);
    }
}