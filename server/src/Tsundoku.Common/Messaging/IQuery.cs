namespace Tsundoku.Common.Messaging;

/// <summary>A request that only reads. It never goes through the unit of work.</summary>
public interface IQuery<TResult> where TResult : notnull;
