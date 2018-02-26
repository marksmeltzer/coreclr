// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if !netstandard
using Internal.Runtime.CompilerServices;
#endif

namespace System.Threading.Tasks
{
    /// <summary>Provides a value type that can represent a task object or a synchronously completed success result.</summary>
    /// <remarks>
    /// <see cref="ValueTask"/>s are meant to be directly awaited.  To do more complicated operations with them, a <see cref="Task"/>
    /// should be extracted using <see cref="AsTask"/>.  Such operations might include caching an instance to be awaited later,
    /// registering multiple continuations with a single operation, awaiting the same task multiple times, and using combinators
    /// over multiple operations.
    /// </remarks>
    [AsyncMethodBuilder(typeof(AsyncValueTaskMethodBuilder))]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueTask : IEquatable<ValueTask>
    {
#if netstandard
        /// <summary>A successfully completed task.</summary>
        private static readonly Task s_completedTask = Task.Delay(0);
#endif

        /// <summary>null if representing a successful synchronous completion, otherwise a <see cref="Task"/> or a <see cref="IValueTaskObject"/>.</summary>
        internal readonly object _obj;
        /// <summary>Flags providing additional details about the ValueTask's contents and behavior.</summary>
        internal readonly ValueTaskFlags _flags;

        // An instance created with the default ctor (a zero init'd struct) represents a synchronously, successfully completed operation.

        /// <summary>Initialize the <see cref="ValueTask"/> with a <see cref="Task"/> that represents the operation.</summary>
        /// <param name="task">The task.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask(Task task)
        {
            if (task == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _obj = task;
            _flags = ValueTaskFlags.ObjectIsTask;
        }

        /// <summary>Initialize the <see cref="ValueTask"/> with a <see cref="IValueTaskObject"/> object that represents the operation.</summary>
        /// <param name="obj">The object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask(IValueTaskObject obj)
        {
            if (obj == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _obj = obj;
            _flags = 0;
        }

        /// <summary>Non-verified initialization of the struct to the specified values.</summary>
        /// <param name="obj">The object.</param>
        /// <param name="flags">The flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask(object obj, ValueTaskFlags flags)
        {
            _obj = obj;
            _flags = flags;
        }

        /// <summary>Gets whether the contination should be scheduled to the current context.</summary>
        internal bool ContinueOnCapturedContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & ValueTaskFlags.AvoidCapturedContext) == 0;
        }

        /// <summary>Gets whether the object in the <see cref="_obj"/> field is a <see cref="Task"/>.</summary>
        internal bool ObjectIsTask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & ValueTaskFlags.ObjectIsTask) != 0;
        }

        /// <summary>Returns the <see cref="Task"/> stored in <see cref="_obj"/>.  This uses <see cref="Unsafe"/>.</summary>
        internal Task UnsafeTask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(ObjectIsTask);
                Debug.Assert(_obj is Task);
                return Unsafe.As<Task>(_obj);
            }
        }

        /// <summary>Returns the <see cref="IValueTaskObject"/> stored in <see cref="_obj"/>.  This uses <see cref="Unsafe"/>.</summary>
        internal IValueTaskObject UnsafeValueTaskObject
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(!ObjectIsTask);
                Debug.Assert(_obj is IValueTaskObject);
                return Unsafe.As<IValueTaskObject>(_obj);
            }
        }

        /// <summary>Returns the hash code for this instance.</summary>
        public override int GetHashCode() => _obj?.GetHashCode() ?? 0;

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="object"/>.</summary>
        public override bool Equals(object obj) =>
            obj is ValueTask &&
            Equals((ValueTask)obj);

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="ValueTask"/> value.</summary>
        public bool Equals(ValueTask other) => _obj == other._obj;

        /// <summary>Returns a value indicating whether two <see cref="ValueTask"/> values are equal.</summary>
        public static bool operator ==(ValueTask left, ValueTask right) =>
            left.Equals(right);

        /// <summary>Returns a value indicating whether two <see cref="ValueTask"/> values are not equal.</summary>
        public static bool operator !=(ValueTask left, ValueTask right) =>
            !left.Equals(right);

        /// <summary>
        /// Gets a <see cref="Task"/> object to represent this ValueTask.
        /// </summary>
        /// <remarks>
        /// It will either return the wrapped task object if one exists, or it'll
        /// manufacture a new task object to represent the result.
        /// </remarks>
        public Task AsTask() =>
            _obj == null ?
#if netstandard
                s_completedTask :
#else
                Task.CompletedTask :
#endif
            ObjectIsTask ? UnsafeTask :
            GetTaskForValueTaskObject();

        /// <summary>Creates a <see cref="Task"/> to represent the <see cref="IValueTaskObject"/>.</summary>
        private Task GetTaskForValueTaskObject()
        {
            IValueTaskObject t = UnsafeValueTaskObject;
            if (t.IsCompleted)
            {
                try
                {
                    // Propagate any exceptions that may have occurred, then return
                    // an already successfully completed task.
                    t.GetResult();
                    return
#if netstandard
                        s_completedTask;
#else
                        Task.CompletedTask;
#endif
                }
                catch (Exception exc)
                {
#if netstandard
                    var tcs = new TaskCompletionSource<bool>();
                    tcs.TrySetException(exc);
                    return tcs.Task;
#else
                    return Task.FromException(exc);
#endif
                }
            }

            var m = new ValueTaskObjectMarshaler(t);
            return
#if netstandard
                m.Task;
#else
                m;
#endif
        }

        /// <summary>Type used to create a <see cref="Task"/> to represent a <see cref="IValueTaskObject"/>.</summary>
        private sealed class ValueTaskObjectMarshaler :
#if netstandard
            TaskCompletionSource<bool>
#else
            Task<VoidTaskResult>
#endif
        {
            private readonly IValueTaskObject _task;

            public ValueTaskObjectMarshaler(IValueTaskObject task)
            {
                _task = task;
                task.UnsafeOnCompleted(new Action(HasCompleted), continueOnCapturedContext: false);
            }

            private void HasCompleted()
            {
                try
                {
                    _task.GetResult();
                    TrySetResult(default);
                }
                catch (Exception exc)
                {
                    TrySetException(exc);
                }
            }
        }

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a completed operation.</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _obj == null || (ObjectIsTask ? UnsafeTask.IsCompleted : UnsafeValueTaskObject.IsCompleted);
        }

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a successfully completed operation.</summary>
        public bool IsCompletedSuccessfully
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                _obj == null ||
                (ObjectIsTask ?
#if netstandard
                    UnsafeTask.Status == TaskStatus.RanToCompletion :
#else
                    UnsafeTask.IsCompletedSuccessfully :
#endif
                    UnsafeValueTaskObject.IsCompletedSuccessfully);
        }

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a failed operation.</summary>
        public bool IsFaulted
        {
            get
            {
                if (_obj == null)
                {
                    return false;
                }

                if (ObjectIsTask)
                {
                    return UnsafeTask.IsFaulted;
                }

                IValueTaskObject vt = UnsafeValueTaskObject;
                return vt.IsCompleted && !vt.IsCompletedSuccessfully;
            }
        }

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a canceled operation.</summary>
        /// <remarks>
        /// If the <see cref="ValueTask"/> is backed by a result or by a <see cref="IValueTaskObject"/>,
        /// this will always return false.  If it's backed by a <see cref="Task"/>, it'll return the
        /// value of the task's <see cref="Task.IsCanceled"/> property.
        /// </remarks>
        public bool IsCanceled => ObjectIsTask && UnsafeTask.IsCanceled;

        /// <summary>Gets an awaiter for this <see cref="ValueTask"/>.</summary>
        public ValueTaskAwaiter GetAwaiter() => new ValueTaskAwaiter(this);

        /// <summary>Configures an awaiter for this <see cref="ValueTask"/>.</summary>
        /// <param name="continueOnCapturedContext">
        /// true to attempt to marshal the continuation back to the captured context; otherwise, false.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredValueTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        {
            bool avoidCapture = !continueOnCapturedContext;
            return new ConfiguredValueTaskAwaitable(new ValueTask(_obj, _flags | Unsafe.As<bool, ValueTaskFlags>(ref avoidCapture)));
        }
    }

    /// <summary>Provides a value type that can represent a synchronously available value or a task object.</summary>
    /// <typeparam name="TResult">Specifies the type of the result.</typeparam>
    /// <remarks>
    /// <see cref="ValueTask{TResult}"/>s are meant to be directly awaited.  To do more complicated operations with them, a <see cref="Task"/>
    /// should be extracted using <see cref="AsTask"/>.  Such operations might include caching an instance to be awaited later,
    /// registering multiple continuations with a single operation, awaiting the same task multiple times, and using combinators
    /// over multiple operations.
    /// </remarks>
    [AsyncMethodBuilder(typeof(AsyncValueTaskMethodBuilder<>))]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueTask<TResult> : IEquatable<ValueTask<TResult>>
    {
        /// <summary>null if <see cref="_result"/> has the result, otherwise a <see cref="Task{TResult}"/> or a <see cref="IValueTaskObject{TResult}"/>.</summary>
        internal readonly object _obj;
        /// <summary>The result to be used if the operation completed successfully synchronously.</summary>
        internal readonly TResult _result;
        /// <summary>Flags providing additional details about the ValueTask's contents and behavior.</summary>
        internal readonly ValueTaskFlags _flags;

        // An instance created with the default ctor (a zero init'd struct) represents a synchronously, successfully completed operation
        // with a result of default(TResult).

        /// <summary>Initialize the <see cref="ValueTask{TResult}"/> with a <see cref="TResult"/> result value.</summary>
        /// <param name="result">The result.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask(TResult result)
        {
            _obj = null;
            _result = result;
            _flags = 0;
        }

        /// <summary>Initialize the <see cref="ValueTask{TResult}"/> with a <see cref="Task{TResult}"/> that represents the operation.</summary>
        /// <param name="task">The task.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask(Task<TResult> task)
        {
            if (task == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _obj = task;
            _result = default;
            _flags = ValueTaskFlags.ObjectIsTask;
        }

        /// <summary>Initialize the <see cref="ValueTask{TResult}"/> with a <see cref="IValueTaskObject{TResult}"/> object that represents the operation.</summary>
        /// <param name="obj">The object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask(IValueTaskObject<TResult> obj)
        {
            if (obj == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _obj = obj;
            _result = default;
            _flags = 0;
        }

        /// <summary>Non-verified initialization of the struct to the specified values.</summary>
        /// <param name="obj">The object.</param>
        /// <param name="result">The result.</param>
        /// <param name="flags">The flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask(object obj, TResult result, ValueTaskFlags flags)
        {
            _obj = obj;
            _result = result;
            _flags = flags;
        }

        /// <summary>Gets whether the contination should be scheduled to the current context.</summary>
        internal bool ContinueOnCapturedContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & ValueTaskFlags.AvoidCapturedContext) == 0;
        }

        /// <summary>Gets whether the object in the <see cref="_obj"/> field is a <see cref="Task{TResult}"/>.</summary>
        internal bool ObjectIsTask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_flags & ValueTaskFlags.ObjectIsTask) != 0;
        }

        /// <summary>Returns the <see cref="Task{TResult}"/> stored in <see cref="_obj"/>.  This uses <see cref="Unsafe"/>.</summary>
        internal Task<TResult> UnsafeTask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(ObjectIsTask);
                Debug.Assert(_obj is Task<TResult>);
                return Unsafe.As<Task<TResult>>(_obj);
            }
        }

        /// <summary>Returns the <see cref="IValueTaskObject<typeparamref name="TResult"/>>"/> stored in <see cref="_obj"/>.  This uses <see cref="Unsafe"/>.</summary>
        internal IValueTaskObject<TResult> UnsafeValueTaskObject
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(!ObjectIsTask);
                Debug.Assert(_obj is IValueTaskObject<TResult>);
                return Unsafe.As<IValueTaskObject<TResult>>(_obj);
            }
        }

        /// <summary>Returns the hash code for this instance.</summary>
        public override int GetHashCode() =>
            _obj != null ? _obj.GetHashCode() :
            _result != null ? _result.GetHashCode() :
            0;

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="object"/>.</summary>
        public override bool Equals(object obj) =>
            obj is ValueTask<TResult> &&
            Equals((ValueTask<TResult>)obj);

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="ValueTask{TResult}"/> value.</summary>
        public bool Equals(ValueTask<TResult> other) =>
            _obj != null || other._obj != null ?
                _obj == other._obj :
                EqualityComparer<TResult>.Default.Equals(_result, other._result);

        /// <summary>Returns a value indicating whether two <see cref="ValueTask{TResult}"/> values are equal.</summary>
        public static bool operator ==(ValueTask<TResult> left, ValueTask<TResult> right) =>
            left.Equals(right);

        /// <summary>Returns a value indicating whether two <see cref="ValueTask{TResult}"/> values are not equal.</summary>
        public static bool operator !=(ValueTask<TResult> left, ValueTask<TResult> right) =>
            !left.Equals(right);

        /// <summary>
        /// Gets a <see cref="Task{TResult}"/> object to represent this ValueTask.
        /// </summary>
        /// <remarks>
        /// It will either return the wrapped task object if one exists, or it'll
        /// manufacture a new task object to represent the result.
        /// </remarks>
        public Task<TResult> AsTask() =>
            _obj == null ?
#if netstandard
                Task.FromResult(_result) :
#else
                AsyncTaskMethodBuilder<TResult>.GetTaskForResult(_result) :
#endif
            ObjectIsTask ? UnsafeTask :
            GetTaskForValueTaskObject();

        /// <summary>Creates a <see cref="Task{TResult}"/> to represent the <see cref="IValueTaskObject{TResult}"/>.</summary>
        private Task<TResult> GetTaskForValueTaskObject()
        {
            IValueTaskObject<TResult> t = UnsafeValueTaskObject;
            if (t.IsCompleted)
            {
                try
                {
                    // Get the result of the operation and return a task for it.
                    // If any exception occurred, propagate it.
                    return
#if netstandard
                        Task.FromResult(t.GetResult()) :
#else
                        AsyncTaskMethodBuilder<TResult>.GetTaskForResult(t.GetResult());
#endif
                }
                catch (Exception exc)
                {
#if netstandard
                    var tcs = new TaskCompletionSource<TResult>();
                    tcs.TrySetException(exc);
                    return tcs.Task;
#else
                    return Task.FromException<TResult>(exc);
#endif
                }
            }

            var m = new ValueTaskObjectMarshaler(t);
            return
#if netstandard
                m.Task;
#else
                m;
#endif
        }

        /// <summary>Type used to create a <see cref="Task{TResult}"/> to represent a <see cref="IValueTaskObject{TResult}"/>.</summary>
        private sealed class ValueTaskObjectMarshaler :
#if netstandard
            TaskCompletionSource<TResult>
#else
            Task<TResult>
#endif
        {
            private readonly IValueTaskObject<TResult> _obj;

            public ValueTaskObjectMarshaler(IValueTaskObject<TResult> task)
            {
                _obj = task;
                task.UnsafeOnCompleted(new Action(HasCompleted), continueOnCapturedContext: false);
            }

            private void HasCompleted()
            {
                try
                {
                    TrySetResult(_obj.GetResult());
                }
                catch (Exception exc)
                {
                    TrySetException(exc);
                }
            }
        }

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a completed operation.</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _obj == null || (ObjectIsTask ? UnsafeTask.IsCompleted : UnsafeValueTaskObject.IsCompleted);
        }

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a successfully completed operation.</summary>
        public bool IsCompletedSuccessfully
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                _obj == null ||
                (ObjectIsTask ?
#if netstandard
                    UnsafeTask.Status == TaskStatus.RanToCompletion :
#else
                    UnsafeTask.IsCompletedSuccessfully :
#endif
                    UnsafeValueTaskObject.IsCompletedSuccessfully);
        }

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a failed operation.</summary>
        public bool IsFaulted
        {
            get
            {
                if (_obj == null)
                {
                    return false;
                }

                if (ObjectIsTask)
                {
                    return UnsafeTask.IsFaulted;
                }

                IValueTaskObject<TResult> vt = UnsafeValueTaskObject;
                return vt.IsCompleted && !vt.IsCompletedSuccessfully;
            }
        }

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a canceled operation.</summary>
        /// <remarks>
        /// If the <see cref="ValueTask{TResult}"/> is backed by a result or by a <see cref="IValueTaskObject{TResult}"/>,
        /// this will always return false.  If it's backed by a <see cref="Task"/>, it'll return the
        /// value of the task's <see cref="Task{TResult}.IsCanceled"/> property.
        public bool IsCanceled => ObjectIsTask && UnsafeTask.IsCanceled;

        /// <summary>Gets the result.</summary>
        public TResult Result =>
            _obj == null ? _result :
            ObjectIsTask ? UnsafeTask.GetAwaiter().GetResult() :
            UnsafeValueTaskObject.GetResult();

        /// <summary>Gets an awaiter for this <see cref="ValueTask{TResult}"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTaskAwaiter<TResult> GetAwaiter() => new ValueTaskAwaiter<TResult>(this);

        /// <summary>Configures an awaiter for this <see cref="ValueTask{TResult}"/>.</summary>
        /// <param name="continueOnCapturedContext">
        /// true to attempt to marshal the continuation back to the captured context; otherwise, false.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredValueTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext)
        {
            bool avoidCapture = !continueOnCapturedContext;
            return new ConfiguredValueTaskAwaitable<TResult>(new ValueTask<TResult>(_obj, _result, _flags | Unsafe.As<bool, ValueTaskFlags>(ref avoidCapture)));
        }

        /// <summary>Gets a string-representation of this <see cref="ValueTask{TResult}"/>.</summary>
        public override string ToString()
        {
            if (IsCompletedSuccessfully)
            {
                TResult result = Result;
                if (result != null)
                {
                    return result.ToString();
                }
            }

            return string.Empty;
        }
    }

    /// <summary>Internal flags used in the implementation of <see cref="ValueTask"/> and <see cref="ValueTask{TResult}"/>.</summary>
    [Flags]
    internal enum ValueTaskFlags : byte
    {
        /// <summary>
        /// Indicates that context (e.g. SynchronizationContext) should not be captured when adding
        /// a continuation.
        /// </summary>
        /// <remarks>
        /// The value here must be 0x1, to match the value of a true Boolean reinterpreted as a byte.
        /// This only has meaning when awaiting a ValueTask, with ConfigureAwait creating a new
        /// ValueTask setting or not setting this flag appropriately.
        /// </remarks>
        AvoidCapturedContext = 0x1,

        /// <summary>
        /// Indicates that the ValueTask's object field stores a Task.  This is used to avoid
        /// a type check on whatever is stored in the object field.
        /// </summary>
        ObjectIsTask = 0x2
    }
}
