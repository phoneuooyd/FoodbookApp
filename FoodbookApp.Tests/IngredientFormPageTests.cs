using FluentAssertions;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Moq;
using System.Globalization;

namespace FoodbookApp.Tests
{
    public class IngredientFormPageTests
    {
        private readonly Mock<IIngredientService> _mockIngredientService;
        private readonly IngredientFormViewModel _viewModel;

        public IngredientFormPageTests()
        {
            _mockIngredientService = new Mock<IIngredientService>();
            _viewModel = new IngredientFormViewModel(_mockIngredientService.Object);
        }
    }
}
