using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Persistence;

/// <summary>
/// Conversions for the value objects of this module. Add one line for each new id or value type.
/// The database holds only values that the domain accepted, so a value that the domain refuses on read is a bug.
/// </summary>
internal static class TodosConverters
{
    public static void Register(ModelConfigurationBuilder builder)
    {
        builder.Properties<TodoListId>().HaveConversion<TodoListIdConverter>();
        builder.Properties<TodoItemId>().HaveConversion<TodoItemIdConverter>();
    }

    private static Title TitleFrom(string value) => Title.Create(value) switch
    {
        Title title => title,
        Error error => throw new InvalidOperationException($"The database holds a title that is not valid: {error.Code}."),
    };

    private sealed class TodoListIdConverter() : ValueConverter<TodoListId, Guid>(id => id.Value, value => new TodoListId(value));

    private sealed class TodoItemIdConverter() : ValueConverter<TodoItemId, Guid>(id => id.Value, value => new TodoItemId(value));

    internal sealed class TitleConverter() : ValueConverter<Title, string>(title => title.Value, value => TitleFrom(value));
}
