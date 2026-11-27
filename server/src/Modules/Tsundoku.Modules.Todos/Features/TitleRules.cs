using FluentValidation;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features;

/// <summary>The field rules of a title, for each command that takes one. The <see cref="Title"/> value object has the same limits.</summary>
internal static class TitleRules
{
    public static IRuleBuilderOptions<T, string> ValidTitle<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("Give a title.")
        .MaximumLength(Title.MaxLength).WithMessage($"Keep the title to {Title.MaxLength} characters.");
}
