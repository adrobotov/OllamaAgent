using OllamaAgent.ViewModels;

namespace OllamaAgent.Tests.ViewModels;

public class ChatMessageViewModelTests
{
    [Fact]
    public void Constructor_UserMessage_SetsCorrectProperties()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("Hello", ChatMessageViewModel.MessageType.User);

        // Assert
        Assert.Equal("Hello", vm.Content);
        Assert.Equal(ChatMessageViewModel.MessageType.User, vm.Type);
        Assert.Equal("Вы", vm.Sender);
    }

    [Fact]
    public void Constructor_AssistantMessage_SetsCorrectProperties()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("Hi there!", ChatMessageViewModel.MessageType.Assistant);

        // Assert
        Assert.Equal("Hi there!", vm.Content);
        Assert.Equal(ChatMessageViewModel.MessageType.Assistant, vm.Type);
        Assert.Equal("Ассистор", vm.Sender);
    }

    [Fact]
    public void Constructor_ToolResultMessage_SetsCorrectProperties()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("Tool output", ChatMessageViewModel.MessageType.ToolResult);

        // Assert
        Assert.Equal("Tool output", vm.Content);
        Assert.Equal(ChatMessageViewModel.MessageType.ToolResult, vm.Type);
        Assert.Equal("Инструмент", vm.Sender);
    }

    [Fact]
    public void Constructor_ErrorMessage_SetsCorrectProperties()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("Something went wrong", ChatMessageViewModel.MessageType.Error);

        // Assert
        Assert.Equal("Something went wrong", vm.Content);
        Assert.Equal(ChatMessageViewModel.MessageType.Error, vm.Type);
        Assert.Equal("Ошибка", vm.Sender);
    }

    [Fact]
    public void Constructor_UserMessage_SetsBlueBackground()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("test", ChatMessageViewModel.MessageType.User);

        // Assert
        Assert.NotNull(vm.BackgroundColor);
        var solidColorBrush = vm.BackgroundColor as Avalonia.Media.SolidColorBrush;
        Assert.NotNull(solidColorBrush);
        var color = solidColorBrush!.Color;
        Assert.Equal(219, color.R);
        Assert.Equal(232, color.G);
        Assert.Equal(241, color.B);
    }

    [Fact]
    public void Constructor_AssistantMessage_SetsGreenBackground()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("test", ChatMessageViewModel.MessageType.Assistant);

        // Assert
        Assert.NotNull(vm.BackgroundColor);
        var solidColorBrush = vm.BackgroundColor as Avalonia.Media.SolidColorBrush;
        Assert.NotNull(solidColorBrush);
        var color = solidColorBrush!.Color;
        Assert.Equal(230, color.R);
        Assert.Equal(244, color.G);
        Assert.Equal(230, color.B);
    }

    [Fact]
    public void Constructor_ToolResultMessage_SetsYellowBackground()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("test", ChatMessageViewModel.MessageType.ToolResult);

        // Assert
        Assert.NotNull(vm.BackgroundColor);
        var solidColorBrush = vm.BackgroundColor as Avalonia.Media.SolidColorBrush;
        Assert.NotNull(solidColorBrush);
        var color = solidColorBrush!.Color;
        Assert.Equal(255, color.R);
        Assert.Equal(248, color.G);
        Assert.Equal(220, color.B);
    }

    [Fact]
    public void Constructor_ErrorMessage_SetsRedBackground()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("test", ChatMessageViewModel.MessageType.Error);

        // Assert
        Assert.NotNull(vm.BackgroundColor);
        var solidColorBrush = vm.BackgroundColor as Avalonia.Media.SolidColorBrush;
        Assert.NotNull(solidColorBrush);
        var color = solidColorBrush!.Color;
        Assert.Equal(255, color.R);
        Assert.Equal(220, color.G);
        Assert.Equal(220, color.B);
    }

    [Fact]
    public void Constructor_UserMessage_SetsBlueSenderColor()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("test", ChatMessageViewModel.MessageType.User);

        // Assert
        Assert.NotNull(vm.SenderColor);
        var solidColorBrush = vm.SenderColor as Avalonia.Media.SolidColorBrush;
        Assert.NotNull(solidColorBrush);
        var color = solidColorBrush!.Color;
        Assert.Equal(0, color.R);
        Assert.Equal(100, color.G);
        Assert.Equal(200, color.B);
    }

    [Fact]
    public void Constructor_EmptyContent_SetsEmptyString()
    {
        // Arrange & Act
        var vm = new ChatMessageViewModel("", ChatMessageViewModel.MessageType.User);

        // Assert
        Assert.Equal(string.Empty, vm.Content);
    }
}
