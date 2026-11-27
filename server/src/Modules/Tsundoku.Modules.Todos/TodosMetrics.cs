using System.Diagnostics.Metrics;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Todos;

/// <summary>The metrics of this module, on the shared app meter. Dashboards use the names: do not rename them.</summary>
internal sealed class TodosMetrics(IMeterFactory meters)
{
    private readonly Counter<long> _listsCompleted =
        meters.Create(AppMetrics.MeterName).CreateCounter<long>("app.todo_lists.completed", "{list}", "Lists whose last open item was done.");

    public void TodoListCompleted() => _listsCompleted.Add(1);
}
