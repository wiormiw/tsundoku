using FluentValidation;

namespace Tsundoku.Modules.Todos.Features.ListTodoLists;

internal sealed class ListTodoListsQueryValidator : AbstractValidator<ListTodoListsQuery>
{
    public const int MaxPageSize = 100;

    public ListTodoListsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Search).MaximumLength(100);
    }
}
