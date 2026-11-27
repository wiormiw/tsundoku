using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Persistence;

/// <summary>The mapping of the aggregate. The domain has no EF attributes; all mapping is here (tip 25.02).</summary>
internal sealed class TodoListConfiguration : IEntityTypeConfiguration<TodoList>
{
    public void Configure(EntityTypeBuilder<TodoList> builder)
    {
        builder.HasKey(list => list.Id);
        builder.Property(list => list.Id).ValueGeneratedNever();
        // A complex property, not a converter, so queries can search in the text (title ILIKE ...).
        builder.ComplexProperty(list => list.Title, title =>
            title.Property(value => value.Value).HasColumnName("title").HasMaxLength(Title.MaxLength));
        // Each change of the aggregate raises the version, so a parallel change fails (tip 10.23).
        builder.Property(list => list.Version).IsConcurrencyToken();
        builder.Ignore(list => list.IsDone);
        builder.Ignore(list => list.DomainEvents);

        // The list pages of one user, newest first.
        builder.HasIndex(list => new { list.OwnerId, list.CreatedAt });

        // The items are part of the aggregate: they load with it and they go with it.
        builder.OwnsMany(list => list.Items, items =>
        {
            items.ToTable("todo_items");
            items.WithOwner().HasForeignKey("TodoListId");
            items.HasKey(item => item.Id);
            items.Property(item => item.Id).ValueGeneratedNever();
            items.Property(item => item.Title).HasConversion<TodosConverters.TitleConverter>().HasMaxLength(Title.MaxLength);
            items.Ignore(item => item.IsDone);
        });
        builder.Navigation(list => list.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
