using System.Text.Json;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using FoodbookApp.Services.Supabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FoodbookApp.Tests;

public class SupabaseSyncServiceTests
{
    [Fact]
    public async Task StartSmartMergeInitialSyncAsync_ShouldRespectTimestampRules_WhenUpdatedAtIsNull()
    {
        var accountId = Guid.NewGuid();
        var folderToUpdateId = Guid.NewGuid();
        var folderWithNullCloudTimestampId = Guid.NewGuid();
        var folderWithOlderCloudTimestampId = Guid.NewGuid();

        var cloudNewer = DateTime.UtcNow;
        var localNewer = cloudNewer.AddHours(1);

        var (provider, tokenStoreMock, crudMock) = CreateProvider(accountId);

        crudMock
            .Setup(x => x.FetchAllCloudDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudDataSnapshot(
                Folders:
                [
                    new Folder { Id = folderToUpdateId, Name = "Cloud-Newer", UpdatedAt = cloudNewer, CreatedAt = cloudNewer.AddDays(-1) },
                    new Folder { Id = folderWithNullCloudTimestampId, Name = "Cloud-Null", UpdatedAt = null, CreatedAt = cloudNewer.AddDays(-1) },
                    new Folder { Id = folderWithOlderCloudTimestampId, Name = "Cloud-Older", UpdatedAt = cloudNewer.AddHours(-2), CreatedAt = cloudNewer.AddDays(-1) }
                ],
                Recipes: [],
                Ingredients: [],
                RecipeLabels: [],
                Plans: [],
                PlannedMeals: [],
                ShoppingListItems: [],
                UserPreferences: null));

        await SeedSyncStateAsync(provider, accountId, isCloudSyncEnabled: true, initialSyncCompleted: false);

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Folders.AddRange(
                new Folder { Id = folderToUpdateId, Name = "Local-Older", UpdatedAt = cloudNewer.AddHours(-3), CreatedAt = cloudNewer.AddDays(-2) },
                new Folder { Id = folderWithNullCloudTimestampId, Name = "Local-HasTimestamp", UpdatedAt = localNewer, CreatedAt = cloudNewer.AddDays(-2) },
                new Folder { Id = folderWithOlderCloudTimestampId, Name = "Local-Newer", UpdatedAt = localNewer, CreatedAt = cloudNewer.AddDays(-2) });
            await db.SaveChangesAsync();
        }

        var sut = new SupabaseSyncService(provider, tokenStoreMock.Object);

        var result = await sut.StartSmartMergeInitialSyncAsync();

        Assert.True(result);

        using (var assertScope = provider.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var folderToUpdate = await db.Folders.FindAsync(folderToUpdateId);
            var folderWithNullCloudTimestamp = await db.Folders.FindAsync(folderWithNullCloudTimestampId);
            var folderWithOlderCloudTimestamp = await db.Folders.FindAsync(folderWithOlderCloudTimestampId);
            var state = await db.SyncStates.FirstAsync(s => s.AccountId == accountId);

            Assert.NotNull(folderToUpdate);
            Assert.NotNull(folderWithNullCloudTimestamp);
            Assert.NotNull(folderWithOlderCloudTimestamp);

            Assert.Equal("Cloud-Newer", folderToUpdate!.Name);
            Assert.Equal("Local-HasTimestamp", folderWithNullCloudTimestamp!.Name);
            Assert.Equal("Local-Newer", folderWithOlderCloudTimestamp!.Name);
            Assert.True(state.InitialSyncCompleted);
            Assert.Equal(SyncStatus.Idle, state.Status);
        }

        crudMock.Verify(x => x.FetchAllCloudDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessQueueAsync_ShouldUseUpdateAndDeleteSemantics()
    {
        var accountId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var (provider, tokenStoreMock, crudMock) = CreateProvider(accountId);
        await SeedSyncStateAsync(provider, accountId, isCloudSyncEnabled: true, initialSyncCompleted: true);

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.SyncQueue.Add(new SyncQueueEntry
            {
                AccountId = accountId,
                EntityType = nameof(Recipe),
                EntityId = recipeId,
                OperationType = SyncOperationType.Update,
                Payload = JsonSerializer.Serialize(new Recipe { Id = recipeId, Name = "Updated recipe" }),
                Priority = 1
            });

            db.SyncQueue.Add(new SyncQueueEntry
            {
                AccountId = accountId,
                EntityType = nameof(Ingredient),
                EntityId = ingredientId,
                OperationType = SyncOperationType.Delete,
                Payload = null,
                Priority = 2
            });

            await db.SaveChangesAsync();
        }

        crudMock.Setup(x => x.UpdateRecipeAsync(It.IsAny<Recipe>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        crudMock.Setup(x => x.DeleteIngredientAsync(ingredientId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new SupabaseSyncService(provider, tokenStoreMock.Object);

        Assert.True(await sut.IsCloudSyncEnabledAsync(), "Precondition failed: sync should be enabled for this test.");

        var result = await sut.ProcessQueueAsync();

        Assert.True(result.Success, result.ErrorMessage ?? "ProcessQueueAsync failed without error message.");
        Assert.Equal(2, result.ItemsProcessed);

        crudMock.Verify(x => x.UpdateRecipeAsync(It.Is<Recipe>(r => r.Id == recipeId), It.IsAny<CancellationToken>()), Times.Once);
        crudMock.Verify(x => x.DeleteIngredientAsync(ingredientId, It.IsAny<CancellationToken>()), Times.Once);
        crudMock.Verify(x => x.UpsertRecipesAsync(It.IsAny<IEnumerable<Recipe>>(), It.IsAny<CancellationToken>()), Times.Never);

        using var assertScope = provider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var statuses = await assertDb.SyncQueue.Where(q => q.AccountId == accountId).Select(q => q.Status).ToListAsync();
        Assert.All(statuses, s => Assert.Equal(SyncEntryStatus.Completed, s));
    }

    [Fact]
    public async Task ProcessQueueAsync_ShouldStopOnFatalError_AndRequeueUnprocessedEntries()
    {
        var accountId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var (provider, tokenStoreMock, crudMock) = CreateProvider(accountId);
        await SeedSyncStateAsync(provider, accountId, isCloudSyncEnabled: true, initialSyncCompleted: true);

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.SyncQueue.Add(new SyncQueueEntry
            {
                AccountId = accountId,
                EntityType = nameof(Recipe),
                EntityId = recipeId,
                OperationType = SyncOperationType.Update,
                Payload = JsonSerializer.Serialize(new Recipe { Id = recipeId, Name = "Will fail" }),
                Priority = 1
            });

            db.SyncQueue.Add(new SyncQueueEntry
            {
                AccountId = accountId,
                EntityType = nameof(Ingredient),
                EntityId = ingredientId,
                OperationType = SyncOperationType.Delete,
                Payload = null,
                Priority = 2
            });

            await db.SaveChangesAsync();
        }

        crudMock.Setup(x => x.UpdateRecipeAsync(It.IsAny<Recipe>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("401 unauthorized"));

        var sut = new SupabaseSyncService(provider, tokenStoreMock.Object);

        var result = await sut.ProcessQueueAsync();

        Assert.False(result.Success);
        Assert.Contains("401", result.ErrorMessage);

        crudMock.Verify(x => x.UpdateRecipeAsync(It.IsAny<Recipe>(), It.IsAny<CancellationToken>()), Times.Once);
        crudMock.Verify(x => x.DeleteIngredientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        using var assertScope = provider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updateEntry = await assertDb.SyncQueue.FirstAsync(q => q.EntityId == recipeId);
        var deleteEntry = await assertDb.SyncQueue.FirstAsync(q => q.EntityId == ingredientId);
        var state = await assertDb.SyncStates.FirstAsync(s => s.AccountId == accountId);

        Assert.Equal(SyncEntryStatus.Failed, updateEntry.Status);
        Assert.Equal(SyncEntryStatus.Pending, deleteEntry.Status);
        Assert.Equal(SyncStatus.Error, state.Status);
        Assert.Contains("401", state.LastSyncError);
    }

    private static (ServiceProvider Provider, Mock<IAuthTokenStore> TokenStoreMock, Mock<ISupabaseCrudService> CrudMock) CreateProvider(Guid accountId)
    {
        var services = new ServiceCollection();

        var dbName = $"supabase-sync-tests-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName, dbRoot));

        var tokenStoreMock = new Mock<IAuthTokenStore>(MockBehavior.Strict);
        tokenStoreMock.Setup(x => x.GetActiveAccountIdAsync()).ReturnsAsync(accountId);

        var preferencesMock = new Mock<IPreferencesService>(MockBehavior.Loose);
        var themeMock = new Mock<IThemeService>(MockBehavior.Loose);
        var crudMock = new Mock<ISupabaseCrudService>(MockBehavior.Loose);

        services.AddSingleton(tokenStoreMock.Object);
        services.AddSingleton(preferencesMock.Object);
        services.AddSingleton(themeMock.Object);
        services.AddSingleton(crudMock.Object);

        return (services.BuildServiceProvider(), tokenStoreMock, crudMock);
    }

    private static async Task SeedSyncStateAsync(
        ServiceProvider provider,
        Guid accountId,
        bool isCloudSyncEnabled,
        bool initialSyncCompleted)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var account = await db.AuthAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
        {
            db.AuthAccounts.Add(new AuthAccount
            {
                Id = accountId,
                SupabaseUserId = accountId.ToString(),
                CreatedUtc = DateTime.UtcNow
            });
        }

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId);
        if (state == null)
        {
            db.SyncStates.Add(new SyncState
            {
                AccountId = accountId,
                IsCloudSyncEnabled = isCloudSyncEnabled,
                InitialSyncCompleted = initialSyncCompleted,
                Status = SyncStatus.Idle,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }
        else
        {
            state.IsCloudSyncEnabled = isCloudSyncEnabled;
            state.InitialSyncCompleted = initialSyncCompleted;
            state.Status = SyncStatus.Idle;
            state.UpdatedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}
