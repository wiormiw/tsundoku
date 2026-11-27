namespace Tsundoku.Common.Messaging;

/// <summary>A request that changes one aggregate. The unit of work saves it when the handler succeeds.</summary>
public interface ICommand<TResult> where TResult : notnull;
