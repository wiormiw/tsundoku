using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Features;
using Tsundoku.Modules.Todos.Features.GetTodoList;
using Tsundoku.Modules.Todos.Features.ListTodoLists;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Api.Tests.Integration;

public sealed class TodoListEndpointTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    private static async Task<Guid> Create(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/todo-lists", new { title }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<IdResponse>(Json, Ct)).ShouldNotBeNull().Id;
        response.Headers.Location.ShouldBe(new Uri($"/api/todo-lists/{id}", UriKind.Relative));
        return id;
    }

    private static async Task<Guid> AddItem(HttpClient client, Guid listId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/todo-lists/{listId}/items", new { title }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdResponse>(Json, Ct)).ShouldNotBeNull().Id;
    }

    private static async Task<TodoListResponse> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<TodoListResponse>($"/api/todo-lists/{id}", Json, Ct)).ShouldNotBeNull();

    private static async Task<JsonElement> Problem(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json, Ct);

    [Fact]
    public async Task A_request_without_a_user_gets_401()
    {
        using var client = factory.CreateApiClient(user: null);

        (await client.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_health_endpoints_are_public()
    {
        using var client = factory.CreateApiClient(user: null);

        (await client.GetAsync("/health/live", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_list_is_filled_completed_renamed_and_deleted()
    {
        using var client = factory.CreateApiClient(NewUser());
        var id = await Create(client, "  Groceries ");

        var milk = await AddItem(client, id, "Milk");
        var bread = await AddItem(client, id, "Bread");
        (await client.PostAsync($"/api/todo-lists/{id}/items/{milk}/complete", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/todo-lists/{id}/items/{bread}/complete", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // A repeated request is safe.
        (await client.PostAsync($"/api/todo-lists/{id}/items/{bread}/complete", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var done = await Get(client, id);
        done.Title.ShouldBe("Groceries");
        done.IsDone.ShouldBeTrue();
        done.Items.Select(item => item.Title).ShouldBe(["Milk", "Bread"]);
        done.Version.ShouldBe(4);
        done.UpdatedAt.ShouldNotBeNull();

        (await client.PutAsJsonAsync($"/api/todo-lists/{id}", new { title = "Food" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Get(client, id)).Title.ShouldBe("Food");

        (await client.DeleteAsync($"/api/todo-lists/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/todo-lists/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_completed_list_sends_its_events_through_the_outbox_after_the_commit()
    {
        using var client = factory.CreateApiClient(NewUser());
        var id = await Create(client, "Groceries");
        var milk = await AddItem(client, id, "Milk");

        (await client.PostAsync($"/api/todo-lists/{id}/items/{milk}/complete", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodosDbContext>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (true)
        {
            // The payload is jsonb, which has no LIKE: filter the few rows in memory.
            var messages = (await db.OutboxMessages.AsNoTracking().ToListAsync(Ct))
                .Where(message => message.Payload.Contains(id.ToString(), StringComparison.Ordinal))
                .ToList();
            if (messages.Count == 2 && messages.TrueForAll(message => message.ProcessedAt is not null))
            {
                messages.Select(message => message.Type).ShouldBe(["TodoItemCompleted", "TodoListCompleted"], ignoreOrder: true);
                messages.ShouldAllBe(message => message.Attempts == 0 && message.LastError == null);
                return;
            }
            if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException($"The outbox did not process the events: {messages.Count} found.");
            await Task.Delay(50, Ct);
        }
    }

    [Fact]
    public async Task The_list_page_shows_only_the_lists_of_the_user_newest_first()
    {
        using var client = factory.CreateApiClient(NewUser());
        using var other = factory.CreateApiClient(NewUser());
        await Create(client, "First");
        await Create(client, "Second 100%");
        await Create(other, "Not mine");

        var page = (await client.GetFromJsonAsync<PagedResponse<TodoListSummaryResponse>>("/api/todo-lists?pageSize=10", Json, Ct)).ShouldNotBeNull();
        page.Items.Select(list => list.Title).ShouldBe(["Second 100%", "First"]);
        page.TotalCount.ShouldBe(2);

        var search = (await client.GetFromJsonAsync<PagedResponse<TodoListSummaryResponse>>("/api/todo-lists?search=100%25&includeTotal=false", Json, Ct)).ShouldNotBeNull();
        search.Items.Select(list => list.Title).ShouldBe(["Second 100%"]);
        search.TotalCount.ShouldBeNull();
    }

    [Fact]
    public async Task A_page_size_over_the_limit_is_a_validation_problem()
    {
        using var client = factory.CreateApiClient(NewUser());

        var response = await client.GetAsync("/api/todo-lists?pageSize=101", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Problem(response)).GetProperty("errors").TryGetProperty("pageSize", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task The_list_of_another_user_is_not_found()
    {
        using var owner = factory.CreateApiClient(NewUser());
        using var stranger = factory.CreateApiClient(NewUser());
        var id = await Create(owner, "Private");

        (await stranger.GetAsync($"/api/todo-lists/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.PutAsJsonAsync($"/api/todo-lists/{id}", new { title = "Mine now" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.PostAsJsonAsync($"/api/todo-lists/{id}/items", new { title = "Spam" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.DeleteAsync($"/api/todo-lists/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await Get(owner, id)).Title.ShouldBe("Private");
    }

    [Fact]
    public async Task A_blank_title_gets_a_validation_problem_with_the_field()
    {
        using var client = factory.CreateApiClient(NewUser());

        var response = await client.PostAsJsonAsync("/api/todo-lists", new { title = " " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await Problem(response);
        problem.GetProperty("code").GetString().ShouldBe("validation");
        problem.GetProperty("errors").TryGetProperty("title", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_property_is_refused_not_ignored()
    {
        using var client = factory.CreateApiClient(NewUser());

        var response = await client.PostAsync("/api/todo-lists",
            new StringContent("""{"title":"Groceries","titel":"typo"}""", Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Problem(response)).GetProperty("code").GetString().ShouldBe("bad_request");
    }

    [Fact]
    public async Task An_unknown_item_is_not_found()
    {
        using var client = factory.CreateApiClient(NewUser());
        var id = await Create(client, "Groceries");

        var response = await client.PostAsync($"/api/todo-lists/{id}/items/{Guid.NewGuid()}/complete", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Problem(response)).GetProperty("code").GetString().ShouldBe("todo_item.not_found");
    }

    [Fact]
    public async Task Two_parallel_changes_of_one_aggregate_cannot_both_win()
    {
        var user = NewUser();
        using var client = factory.CreateApiClient(user);
        var id = new TodoListId(await Create(client, "Groceries"));
        var owner = new UserId(user);

        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        var firstDb = first.ServiceProvider.GetRequiredService<TodosDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<TodosDbContext>();
        // Row-level security shows the list only inside a transaction of its owner, as the pipeline runs it.
        await using var firstTransaction = await RowLevelSecurity.BeginTransactionAsync(firstDb.Database, owner, Ct);
        await using var secondTransaction = await RowLevelSecurity.BeginTransactionAsync(secondDb.Database, owner, Ct);
        var firstCopy = await firstDb.TodoLists.SingleAsync(list => list.Id == id && list.OwnerId == owner, Ct);
        var secondCopy = await secondDb.TodoLists.SingleAsync(list => list.Id == id && list.OwnerId == owner, Ct);

        firstCopy.Rename(TitleOf("Food"));
        await firstDb.SaveChangesAsync(Ct);
        await firstTransaction.CommitAsync(Ct);
        // A change to an item conflicts too: the version of the whole aggregate goes up.
        secondCopy.AddItem(TitleOf("Milk"));

        // The unit-of-work decorator turns this into a ConcurrencyException, which the API returns as 409.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(Ct));
        var saved = await Get(client, id.Value);
        saved.Title.ShouldBe("Food");
        saved.Items.ShouldBeEmpty();
    }

    private static Title TitleOf(string text) => Title.Create(text) switch
    {
        Title title => title,
        Error error => throw new InvalidOperationException(error.Code),
    };
}
