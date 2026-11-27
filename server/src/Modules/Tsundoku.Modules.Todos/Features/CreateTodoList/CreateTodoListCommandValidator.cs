using FluentValidation;

namespace Tsundoku.Modules.Todos.Features.CreateTodoList;

internal sealed class CreateTodoListCommandValidator : AbstractValidator<CreateTodoListCommand>
{
    public CreateTodoListCommandValidator() => RuleFor(command => command.Title).ValidTitle();
}
