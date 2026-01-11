using System;
using Foodbook.Models;
using Xunit;

namespace FoodbookApp.Tests
{
    public class ArchiveTests
    {
        [Fact]
        public void Placeholder_ArchiveTests_Compiles()
        {
            var plan = new Plan();
            Assert.False(plan.IsArchived);
        }
    }
}
