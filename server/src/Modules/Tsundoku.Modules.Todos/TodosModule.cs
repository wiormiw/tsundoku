using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Events;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Features;
using Tsundoku.Modules.Todos.Features.AddTodoItem;
using Tsundoku.Modules.Todos.Features.CompleteTodoItem;
using Tsundoku.Modules.Todos.Features.CreateTodoList;
using Tsundoku.Modules.Todos.Features.DeleteTodoList;
using Tsundoku.Modules.Todos.Features.GetTodoList;
using Tsundoku.Modules.Todos.Features.ListTodoLists;
using Tsundoku.Modules.Todos.Features.OnTodoListCompleted;
using Tsundoku.Modules.Todos.Features.RenameTodoList;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos;

/// <summary>
/// The todo lists module: each user has their own lists. This is the only public type of the module.
/// A new use case needs a folder under Features, one line in <see cref="AddTodosModule"/> and one in <see cref="MapTodosEndpoints"/>.
/// </summary>
public static class TodosModule
{
    internal const string Schema = "todos";

    public static IHostApplicationBuilder AddTodosModule(this IHostApplicationBuilder builder)
    {
        builder.AddModuleDatabase<TodosDbContext>(Schema);
        var services = builder.Services;
        services.AddSingleton<TodosMetrics>();

        services.AddValidator<CreateTodoListCommand, CreateTodoListCommandValidator>();
        services.AddValidator<RenameTodoListCommand, RenameTodoListCommandValidator>();
        services.AddValidator<AddTodoItemCommand, AddTodoItemCommandValidator>();
        services.AddValidator<ListTodoListsQuery, ListTodoListsQueryValidator>();

        services.AddCommand<CreateTodoListCommand, IdResponse, CreateTodoListHandler, TodosDbContext>();
        services.AddQuery<ListTodoListsQuery, PagedResponse<TodoListSummaryResponse>, ListTodoListsHandler>();
        services.AddQuery<GetTodoListQuery, TodoListResponse, GetTodoListHandler>();
        services.AddCommand<RenameTodoListCommand, Success, RenameTodoListHandler, TodosDbContext>();
        services.AddCommand<AddTodoItemCommand, IdResponse, AddTodoItemHandler, TodosDbContext>();
        services.AddCommand<CompleteTodoItemCommand, Success, CompleteTodoItemHandler, TodosDbContext>();
        services.AddCommand<DeleteTodoListCommand, Success, DeleteTodoListHandler, TodosDbContext>();

        services.AddDomainEvent<TodoItemCompleted>();
        services.AddDomainEventHandler<TodoListCompleted, TodoListCompletedHandler>();
        return builder;
    }

    /// <summary>Maps the endpoints of the module under <paramref name="api"/>.</summary>
    public static IEndpointRouteBuilder MapTodosEndpoints(this IEndpointRouteBuilder api)
    {
        var todoLists = api.MapGroup("/todo-lists").WithTags("Todo lists");
        CreateTodoListEndpoint.Map(todoLists);
        ListTodoListsEndpoint.Map(todoLists);
        GetTodoListEndpoint.Map(todoLists);
        RenameTodoListEndpoint.Map(todoLists);
        DeleteTodoListEndpoint.Map(todoLists);
        AddTodoItemEndpoint.Map(todoLists);
        CompleteTodoItemEndpoint.Map(todoLists);
        return api;
    }

    public static Task MigrateTodosAsync(this IHost host) => host.MigrateAsync<TodosDbContext>();
}
