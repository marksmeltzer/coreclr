// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Threading.Tasks
{
    /// <summary>
    /// Flags passed from <see cref="ValueTask"/> and <see cref="ValueTask{TResult}"/> to
    /// <see cref="IValueTaskObject.OnCompleted"/> and <see cref="IValueTaskObject{TResult}.OnCompleted"/>
    /// to control behavior.
    /// </summary>
    [Flags]
    public enum ValueTaskObjectOnCompletedFlags
    {
        /// <summary>
        /// No requirements are placed on how the continuation is invoked.
        /// </summary>
        None,
        /// <summary>
        /// Set if OnCompleted should capture the current scheduling context (e.g. SynchronizationContext)
        /// and use it when queueing the continuation for execution.  If this is not set, the implementation
        /// may choose to execute the continuation in an arbitrary location.
        /// </summary>
        UseSchedulingContext = 0x1,
        /// <summary>
        /// Set if OnCompleted should capture the current <see cref="ExecutionContext"/> and use it to
        /// <see cref="ExecutionContext.Run"/> the continuation.
        /// </summary>
        FlowExecutionContext = 0x2,
    }

    /// <summary>Represents a <see cref="Task"/>-like object that can be wrapped by a <see cref="ValueTask"/>.</summary>
    public interface IValueTaskObject
    {
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a completed operation.</summary>
        bool IsCompleted { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a successfully completed operation.</summary>
        bool IsCompletedSuccessfully { get; }
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject"/>.</summary>
        void OnCompleted(Action continuation, ValueTaskObjectOnCompletedFlags flags);
        /// <summary>Gets the result of the <see cref="IValueTaskObject"/>.</summary>
        void GetResult();
    }

    /// <summary>Represents a <see cref="Task{TResult}"/>-like object that can be wrapped by a <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResult">Specifies the type of data returned from the object.</typeparam>
    public interface IValueTaskObject<out TResult>
    {
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a completed operation.</summary>
        bool IsCompleted { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a successfully completed operation.</summary>
        bool IsCompletedSuccessfully { get; }
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject{TResult}"/>.</summary>
        void OnCompleted(Action continuation, ValueTaskObjectOnCompletedFlags flags);
        /// <summary>Gets the result of the <see cref="IValueTaskObject{TResult}"/>.</summary>
        TResult GetResult();
    }
}
