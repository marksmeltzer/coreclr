// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    /// <summary>Provides an awaiter for a <see cref="ValueTask"/>.</summary>
    public readonly struct ValueTaskAwaiter : ICriticalNotifyCompletion, IValueTaskAwaiter
    {
        /// <summary>Shim used to invoke an <see cref="Action"/> passed as the state argument to a <see cref="Action{Object}"/>.</summary>
        internal static readonly Action<object> s_invokeActionDelegate = state => ((Action)state)();
        /// <summary>The value being awaited.</summary>
        private readonly ValueTask _value;

        /// <summary>Initializes the awaiter.</summary>
        /// <param name="value">The value to be awaited.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTaskAwaiter(ValueTask value) => _value = value;

        /// <summary>Gets whether the <see cref="ValueTask"/> has completed.</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value.IsCompleted;
        }

        /// <summary>Gets the result of the ValueTask.</summary>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult()
        {
            if (_value._obj != null)
            {
                if (_value.ObjectIsTask)
                {
                    _value.UnsafeTask.GetAwaiter().GetResult();
                }
                else
                {
                    _value.UnsafeValueTaskSource.GetResult();
                }
            }
        }

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void OnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().OnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskSource.OnCompleted(s_invokeActionDelegate, continuation, ValueTaskSourceOnCompletedFlags.UseSchedulingContext | ValueTaskSourceOnCompletedFlags.FlowExecutionContext);
            }
            else
            {
                Task.CompletedTask.GetAwaiter().OnCompleted(continuation);
            }
        }

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().UnsafeOnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskSource.OnCompleted(s_invokeActionDelegate, continuation, ValueTaskSourceOnCompletedFlags.UseSchedulingContext);
            }
            else
            {
                Task.CompletedTask.GetAwaiter().UnsafeOnCompleted(continuation);
            }
        }

        /// <summary>Gets the task underlying <see cref="_value"/>.</summary>
        internal Task AsTask() => _value.AsTask();

#if CORECLR
        void IValueTaskAwaiter.AwaitUnsafeOnCompleted(IAsyncStateMachineBox box)
        {
            if (_value.ObjectIsTask)
            {
                TaskAwaiter.UnsafeOnCompletedInternal(_value.UnsafeTask, box, continueOnCapturedContext: true);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskSource.OnCompleted(s_invokeAsyncStateMachineBox, box, ValueTaskSourceOnCompletedFlags.UseSchedulingContext);
            }
            else
            {
                TaskAwaiter.UnsafeOnCompletedInternal(Task.CompletedTask, box, continueOnCapturedContext: true);
            }
        }

        /// <summary>Shim used to invoke <see cref="ITaskCompletionAction.Invoke"/> of the supplied <see cref="IAsyncStateMachineBox"/>.</summary>
        internal static readonly Action<object> s_invokeAsyncStateMachineBox = state => ((IAsyncStateMachineBox)state).Invoke(null);
#endif
    }

    /// <summary>Provides an awaiter for a <see cref="ValueTask{TResult}"/>.</summary>
    public readonly struct ValueTaskAwaiter<TResult> : ICriticalNotifyCompletion, IValueTaskAwaiter
    {
        /// <summary>The value being awaited.</summary>
        private readonly ValueTask<TResult> _value;

        /// <summary>Initializes the awaiter.</summary>
        /// <param name="value">The value to be awaited.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTaskAwaiter(ValueTask<TResult> value) => _value = value;

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> has completed.</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value.IsCompleted;
        }

        /// <summary>Gets the result of the ValueTask.</summary>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetResult() =>
            _value._obj == null ? _value._result :
            _value.ObjectIsTask ? _value.UnsafeTask.GetAwaiter().GetResult() :
            _value.UnsafeValueTaskSource.GetResult();

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void OnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().OnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskSource.OnCompleted(ValueTaskAwaiter.s_invokeActionDelegate, continuation, ValueTaskSourceOnCompletedFlags.UseSchedulingContext | ValueTaskSourceOnCompletedFlags.FlowExecutionContext);
            }
            else
            {
                Task.FromResult(_value._result).GetAwaiter().OnCompleted(continuation);
            }
        }

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().UnsafeOnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskSource.OnCompleted(ValueTaskAwaiter.s_invokeActionDelegate, continuation, ValueTaskSourceOnCompletedFlags.UseSchedulingContext);
            }
            else
            {
                Task.FromResult(_value._result).GetAwaiter().UnsafeOnCompleted(continuation);
            }
        }

        /// <summary>Gets the task underlying <see cref="_value"/>.</summary>
        internal Task<TResult> AsTask() => _value.AsTask();

#if CORECLR
        void IValueTaskAwaiter.AwaitUnsafeOnCompleted(IAsyncStateMachineBox box)
        {
            if (_value.ObjectIsTask)
            {
                TaskAwaiter.UnsafeOnCompletedInternal(_value.UnsafeTask, box, continueOnCapturedContext: true);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskSource.OnCompleted(ValueTaskAwaiter.s_invokeAsyncStateMachineBox, box, ValueTaskSourceOnCompletedFlags.UseSchedulingContext);
            }
            else
            {
                TaskAwaiter.UnsafeOnCompletedInternal(Task.CompletedTask, box, continueOnCapturedContext: true);
            }
        }
#endif
    }

#if CORECLR
    /// <summary>Internal interface used to enable optimizations from <see cref="AsyncTaskMethodBuilder"/> on <see cref="ValueTask"/>.</summary>>
    internal interface IValueTaskAwaiter
    {
        /// <summary>Invoked to set <see cref="ITaskCompletionAction.Invoke"/> of the <paramref name="box"/> as the awaiter's continuation.</summary>
        /// <param name="box">The box object.</param>
        void AwaitUnsafeOnCompleted(IAsyncStateMachineBox box);
    }
#endif
}
