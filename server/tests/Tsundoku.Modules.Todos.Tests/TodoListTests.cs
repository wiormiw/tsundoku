using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Tests;

public sealed class TodoListTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly UserId Owner = new("user-1");

    private static Title TitleOf(string text) => Title.Create(text) switch
    {
        Title title => title,
        Error error => throw new InvalidOperationException(error.Code),
    };

    private static TodoItem Add(TodoList list, string title) => list.AddItem(TitleOf(title)) switch
    {
        TodoItem item => item,
        Error error => throw new InvalidOperationException(error.Code),
    };

    [Fact]
    public void A_title_is_trimmed() => TitleOf("  Groceries  ").Value.ShouldBe("Groceries");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_title_is_refused(string? text) =>
        Title.Create(text).Value.ShouldBeOfType<ValidationError>().Errors.Keys.ShouldBe(["title"]);

    [Fact]
    public void A_title_over_the_limit_is_refused() =>
        Title.Create(new string('x', Title.MaxLength + 1)).Value.ShouldBeOfType<ValidationError>();

    [Fact]
    public void Two_titles_with_the_same_text_are_equal() => TitleOf("Milk").ShouldBe(TitleOf(" Milk "));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_bad_user_id_is_a_bug(string value) =>
        Should.Throw<DomainException>(() => new UserId(value)).Code.ShouldBe("user.id_invalid");

    [Fact]
    public void A_new_list_belongs_to_its_owner_and_has_no_items()
    {
        var list = TodoList.Create(Owner, TitleOf("Groceries"));

        list.IsOwnedBy(Owner).ShouldBeTrue();
        list.IsOwnedBy(new UserId("user-2")).ShouldBeFalse();
        list.Items.ShouldBeEmpty();
        list.IsDone.ShouldBeFalse();
        list.Version.ShouldBe(0);
    }

    [Fact]
    public void Each_change_of_the_aggregate_raises_the_version()
    {
        var list = TodoList.Create(Owner, TitleOf("Groceries"));

        list.Rename(TitleOf("Food"));
        var item = Add(list, "Milk");
        list.CompleteItem(item.Id, Now);

        list.Version.ShouldBe(3);
    }

    [Fact]
    public void A_rename_to_the_same_title_is_no_change()
    {
        var list = TodoList.Create(Owner, TitleOf("Groceries"));

        list.Rename(TitleOf("Groceries"));

        list.Version.ShouldBe(0);
    }

    [Fact]
    public void Completing_the_last_open_item_completes_the_list()
    {
        var list = TodoList.Create(Owner, TitleOf("Groceries"));
        var milk = Add(list, "Milk");
        var bread = Add(list, "Bread");

        list.CompleteItem(milk.Id, Now).Value.ShouldBe(Success.Value);
        list.CompleteItem(bread.Id, Now).Value.ShouldBe(Success.Value);

        list.IsDone.ShouldBeTrue();
        list.DomainEvents.ShouldBe([
            new TodoItemCompleted(list.Id.Value, milk.Id.Value, Now),
            new TodoItemCompleted(list.Id.Value, bread.Id.Value, Now),
            new TodoListCompleted(list.Id.Value, Owner.Value, 2),
        ]);
    }

    [Fact]
    public void Completing_an_item_two_times_changes_nothing_the_second_time()
    {
        var list = TodoList.Create(Owner, TitleOf("Groceries"));
        var milk = Add(list, "Milk");
        list.CompleteItem(milk.Id, Now);
        list.ClearDomainEvents();

        list.CompleteItem(milk.Id, Now.AddHours(1)).Value.ShouldBe(Success.Value);

        milk.CompletedAt.ShouldBe(Now);
        list.DomainEvents.ShouldBeEmpty();
        list.Version.ShouldBe(2);
    }

    [Fact]
    public void An_unknown_item_is_not_found() =>
        TodoList.Create(Owner, TitleOf("Groceries")).CompleteItem(TodoItemId.New(), Now).Value
            .ShouldBeOfType<NotFoundError>().Code.ShouldBe("todo_item.not_found");

    [Fact]
    public void A_full_list_refuses_one_more_item()
    {
        var list = TodoList.Create(Owner, TitleOf("Groceries"));
        for (var i = 0; i < TodoList.MaxItems; i++) Add(list, $"Item {i}");

        list.AddItem(TitleOf("One more")).Value.ShouldBeOfType<ValidationError>();
        list.Items.Count.ShouldBe(TodoList.MaxItems);
    }
}
