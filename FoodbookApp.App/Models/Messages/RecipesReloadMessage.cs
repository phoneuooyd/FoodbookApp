using CommunityToolkit.Mvvm.Messaging.Messages;

namespace FoodbookApp.Models.Messages;

public sealed class RecipesReloadMessage : ValueChangedMessage<bool>
{
    public RecipesReloadMessage(bool forceFullReload = true) : base(forceFullReload)
    {
    }
}
