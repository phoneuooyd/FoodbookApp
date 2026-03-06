/*
 
 using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Responses;
using Supabase.Gotrue.Models;
using Foodbook.Data;
using Foodbook.Services.Auth;
using FoodbookApp.Interfaces;
using Xunit;

namespace FoodbookApp.Tests
{
    public class SupabaseAuthServiceTests
    {
        private static AppDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task SignIn_EnsuresClientInitialized()
        {
            var mockClient = new Mock<Client>("https://example.test", "key", new SupabaseOptions());
            mockClient.Setup(c => c.InitializeAsync()).Returns(Task.CompletedTask).Verifiable();

            var mockAuth = new Mock<GotrueClient>("https://example.test", "key");
            // return null session for sign in
            mockAuth.Setup(a => a.SignIn(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((Session?)null);
            mockClient.SetupGet(c => c.Auth).Returns(mockAuth.Object);

            var db = CreateInMemoryContext("auth_init_db");
            var mockTokenStore = new Mock<IAuthTokenStore>();
            var sp = new ServiceCollection().BuildServiceProvider();

            var svc = new SupabaseAuthService(mockClient.Object, mockTokenStore.Object, db, sp);

            await svc.SignInAsync("noone@example.test", "wrongpass");

            mockClient.Verify(c => c.InitializeAsync(), Times.Once);
        }

        [Fact]
        public async Task SignIn_WithNonExistingCredentials_ReturnsNullSession()
        {
            var mockClient = new Mock<Client>("https://example.test", "key", new SupabaseOptions());
            mockClient.Setup(c => c.InitializeAsync()).Returns(Task.CompletedTask);

            var mockAuth = new Mock<GotrueClient>("https://example.test", "key");
            mockAuth.Setup(a => a.SignIn(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((Session?)null);
            mockClient.SetupGet(c => c.Auth).Returns(mockAuth.Object);

            var db = CreateInMemoryContext("auth_signin_db");
            var mockTokenStore = new Mock<IAuthTokenStore>();
            var sp = new ServiceCollection().BuildServiceProvider();

            var svc = new SupabaseAuthService(mockClient.Object, mockTokenStore.Object, db, sp);

            var result = await svc.SignInAsync("doesnotexist@example.test", "badpassword");

            Assert.Null(result);
        }
    }
}

 
 
 */