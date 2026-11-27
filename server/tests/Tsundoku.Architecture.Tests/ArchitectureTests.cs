using System.Reflection;
using System.Text.RegularExpressions;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos;

namespace Tsundoku.Architecture.Tests;

/// <summary>
/// The rules of the vertical-slice modular monolith, checked in the compiled IL and in the source files (tip 25.06).
/// Common is the shared kernel and knows no module. A module owns its feature folders, its domain and its schema,
/// shows one public type, and never references another module. The Api is only the host.
/// </summary>
public sealed partial class ArchitectureTests
{
    private static readonly Assembly Common = typeof(ResultHttp).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly Engine = typeof(Tsundoku.Engine.Rules).Assembly;

    /// <summary>Add each new module here: each rule below then covers it.</summary>
    private static readonly Assembly[] Modules = [typeof(TodosModule).Assembly];

    private static readonly Assembly[] Source = [Common, .. Modules, Api, Engine];

    public static TheoryData<Assembly> EachModule => [.. Modules];

    private static void Holds(NetArchTest.Rules.TestResult result, string rule) =>
        result.IsSuccessful.ShouldBeTrue($"{rule}: {string.Join(", ", result.FailingTypeNames ?? [])}");

    private static IEnumerable<string> ReferenceNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name ?? "");

    private static bool IsOurModule(string assemblyName) => assemblyName.StartsWith("Tsundoku.Modules.", StringComparison.Ordinal);

    private static string DomainNamespace(Assembly module) => $"{module.GetName().Name}.Domain";

    // Boundaries.

    [Fact]
    public void Common_domain_is_plain_csharp() => Holds(
        Types.InAssembly(Common).That().ResideInNamespaceStartingWith("Tsundoku.Common.Domain").ShouldNot().HaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions", "Npgsql", "System.Text.Json", "FluentValidation").GetResult(),
        "the shared domain types use only the BCL");

    [Fact]
    public void Common_references_no_module_and_not_the_host() =>
        ReferenceNames(Common).ShouldNotContain(name => IsOurModule(name) || name == "Tsundoku.Api");

    [Theory]
    [MemberData(nameof(EachModule))]
    public void A_module_references_no_other_module_and_not_the_host(Assembly module) =>
        ReferenceNames(module).ShouldNotContain(
            name => (IsOurModule(name) && name != module.GetName().Name) || name == "Tsundoku.Api",
            "modules talk through a future *.Contracts project, never directly");

    [Theory]
    [MemberData(nameof(EachModule))]
    public void A_module_shows_exactly_one_public_type_its_module_class(Assembly module) =>
        Types.InAssembly(module).That().ArePublic().GetTypes()
            .Where(type => type.Namespace?.Contains(".Migrations", StringComparison.Ordinal) != true)
            .Select(type => type.Name)
            .ShouldBe([module.GetName().Name!.Split('.').Last() + "Module"]);

    [Theory]
    [MemberData(nameof(EachModule))]
    public void A_module_domain_knows_no_framework_and_no_other_part_of_its_module(Assembly module) => Holds(
        Types.InAssembly(module).That().ResideInNamespace(DomainNamespace(module)).ShouldNot().HaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "FluentValidation",
            $"{module.GetName().Name}.Features", $"{module.GetName().Name}.Persistence").GetResult(),
        "the domain of a module is plain C# on top of Common");

    [Fact]
    public void The_api_is_only_the_host_it_does_not_touch_persistence() => Holds(
        Types.InAssembly(Api).ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore").GetResult(),
        "endpoints and persistence live in the modules");

    // The engine: a pure function, so the Bun engine and this one agree on every fixture, on any machine, at any hour.

    [Fact]
    public void Engine_references_no_assemblies_of_ours_or_any_package() =>
        ReferenceNames(Engine)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal) && name != "netstandard")
            .ShouldBeEmpty();

    [Fact]
    public void Engine_does_no_io_and_draws_no_randomness() => Holds(
        Types.InAssembly(Engine).ShouldNot().HaveDependencyOnAny(
            "System.IO", "System.Net", "System.Random", "System.Security.Cryptography", "System.TimeProvider",
            "System.Threading.Tasks", "System.Threading.Thread", "System.Threading.Timer",
            "System.Diagnostics.Stopwatch", "System.Diagnostics.Process", "System.Environment", "System.Console", "System.Text.Json.JsonSerializer").GetResult(),
        "the engine reads only its inputs; System.Text.Json appears only as attributes and converters of the wire shape");

    [Fact]
    public void Engine_reads_no_clock()
    {
        var clockReads = Directory.EnumerateFiles(Path.Combine(SourceFolder(), "Tsundoku.Engine"), "*.cs", SearchOption.AllDirectories)
            .Where(path => ClockRead().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetFileName(path));
        clockReads.ShouldBeEmpty("today is an input of the fold, never read from a clock");
    }

    [GeneratedRegex(@"\b(?:DateTime|DateTimeOffset|DateOnly|TimeOnly)\.(?:Now|UtcNow|Today)\b|\bTimeProvider\b|\bStopwatch\b|\bGuid\.(?:NewGuid|CreateVersion7)\b")]
    private static partial Regex ClockRead();

    // The domain model.

    [Theory]
    [MemberData(nameof(EachModule))]
    public void Domain_state_changes_only_through_methods(Assembly module)
    {
        var publicSetters = module.GetTypes()
            .Where(type => type.IsClass && type.Namespace == DomainNamespace(module))
            .Where(type => !type.IsAssignableTo(typeof(IDomainEvent)) && !type.IsAssignableTo(typeof(Error)))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod is { IsPublic: true })
                .Select(property => $"{type.Name}.{property.Name}"));
        publicSetters.ShouldBeEmpty("entities and value objects change only through their methods (tip 25.05)");
    }

    [Theory]
    [MemberData(nameof(EachModule))]
    public void Domain_events_are_sealed_records(Assembly module) => Holds(
        Types.InAssembly(module).That().ImplementInterface(typeof(IDomainEvent)).Should().BeSealed().GetResult(),
        "an event is a fact: one type, no subclasses");

    [Fact]
    public void Each_kind_of_error_is_sealed() => Holds(
        Types.InAssembly(Common).That().Inherit(typeof(Error)).Should().BeSealed().GetResult(),
        "Error is closed: its direct subtypes are the full list, so they must be sealed");

    // Use cases.

    [Theory]
    [InlineData(typeof(ICommandHandler<,>))]
    [InlineData(typeof(IQueryHandler<,>))]
    public void Handlers_are_internal_sealed_and_named_after_their_use_case(Type handlerInterface)
    {
        var handlers = Types.InAssemblies(Modules).That().ImplementInterface(handlerInterface);
        handlers.GetTypes().ShouldNotBeEmpty();

        Holds(handlers.Should().BeSealed().And().NotBePublic().And().HaveNameEndingWith("Handler").GetResult(),
            "handlers are details of their slice");
    }

    [Fact]
    public void Handlers_never_save_the_unit_of_work_decorator_does()
    {
        var saving = Directory.EnumerateFiles(Path.Combine(SourceFolder(), "Modules"), "*Handler.cs", SearchOption.AllDirectories)
            .Where(path => SavesChanges().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetFileName(path));
        saving.ShouldBeEmpty("a handler that saves by itself can save after a refusal");
    }

    [GeneratedRegex(@"\bSaveChanges(?:Async)?\b|\bIUnitOfWork\b")]
    private static partial Regex SavesChanges();

    // Row-level security: one place sets the user, and only the pipeline reaches a context.

    [Fact]
    public void Only_RowLevelSecurity_sets_the_user()
    {
        var allowed = Path.Combine(SourceFolder(), "Tsundoku.Common", "Persistence", "RowLevelSecurity.cs");
        var setters = SourceFiles().Where(path => path != allowed && SetsTheUser().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(SourceFolder(), path));
        setters.ShouldBeEmpty("the user of a transaction is set in RowLevelSecurity.BeginTransactionAsync only");
    }

    [GeneratedRegex(@"set_config|app\.user_id")]
    private static partial Regex SetsTheUser();

    [Fact]
    public void Only_Common_opens_transactions()
    {
        var opening = SourceFiles().Where(path => path.StartsWith(Path.Combine(SourceFolder(), "Modules"), StringComparison.Ordinal)
                && File.ReadAllText(path).Contains("BeginTransaction", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(SourceFolder(), path));
        opening.ShouldBeEmpty("the pipeline opens the transaction and sets its user; a module's own would have no user");
    }

    [Theory]
    [MemberData(nameof(EachModule))]
    public void Endpoints_do_not_touch_persistence(Assembly module) => Holds(
        Types.InAssembly(module).That().HaveNameEndingWith("Endpoint").ShouldNot().HaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore", $"{module.GetName().Name}.Persistence").GetResult(),
        "an endpoint reaches a context only through the pipeline, which sets the user");

    // Code style.

    [Fact]
    public void Logging_goes_through_source_generated_LoggerMessage_methods_only() => Holds(
        Types.InAssemblies(Source).ShouldNot().HaveDependencyOn("Microsoft.Extensions.Logging.LoggerExtensions").GetResult(),
        "no logger.LogInformation(...) calls; declare a [LoggerMessage] partial method instead");

    [Theory]
    [InlineData("MediatR")]
    [InlineData("AutoMapper")]
    [InlineData("Scrutor")]
    public void Banned_libraries_are_not_referenced(string library)
    {
        foreach (var assembly in Source) ReferenceNames(assembly).ShouldNotContain(library);
    }

    [Fact]
    public void Each_source_file_holds_one_top_level_type()
    {
        var crowded = SourceFiles().Where(path => TopLevelType().Count(File.ReadAllText(path)) > 1)
            .Select(path => Path.GetRelativePath(SourceFolder(), path));
        crowded.ShouldBeEmpty("move each extra type to a file with its own name");
    }

    private static IEnumerable<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(SourceFolder(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && !path.Contains($"{separator}Migrations{separator}", StringComparison.Ordinal));
    }

    /// <summary>A type declaration at column 0. File-scoped namespaces put top-level types there. <c>file</c> types do not count.</summary>
    [GeneratedRegex(@"^(?:public |internal )?(?:sealed |static |abstract |partial |readonly |closed )*(?:class|record|struct|interface|enum|union) ", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    /// <summary>The <c>src</c> folder, found from the folder of the test assembly.</summary>
    private static string SourceFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Tsundoku.slnx"))) return Path.Combine(folder.FullName, "src");
        }
        throw new InvalidOperationException("Tsundoku.slnx not found above " + AppContext.BaseDirectory);
    }
}
