using FluentValidation;

namespace Tsundoku.Modules.Todos.Features.AddTodoItem;

internal sealed class AddTodoItemCommandValidator : AbstractValidator<AddTodoItemCommand>
{
    public AddTodoItemCommandValidator() => RuleFor(command => command.Title).ValidTitle();
}
