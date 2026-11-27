using FluentValidation;

namespace Tsundoku.Modules.Todos.Features.RenameTodoList;

internal sealed class RenameTodoListCommandValidator : AbstractValidator<RenameTodoListCommand>
{
    public RenameTodoListCommandValidator() => RuleFor(command => command.Title).ValidTitle();
}
