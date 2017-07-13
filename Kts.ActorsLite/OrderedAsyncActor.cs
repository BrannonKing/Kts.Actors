﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kts.ActorsLite
{
	/// <summary>
	/// Executes on the primary thread pool. If a task is already executing, the incoming request is queued on the back of that.
	/// </summary>
	public class OrderedAsyncActor<T> : OrderedAsyncActor<T, bool>
	{
		public OrderedAsyncActor(Action<T> action)
			: this((t, c) => action.Invoke(t))
		{
		}

		public OrderedAsyncActor(Action<T, CancellationToken> action)
			: base((t, c, f, l) => { action.Invoke(t, c); return true; })
		{
		}

		public OrderedAsyncActor(SetAction<T> action)
			: base((t, c, f, l) => { action.Invoke(t, c, f, l); return true; })
		{
		}
	}

	public class OrderedAsyncActor<T, R> : IActor<T, R>
	{
		private readonly SetFunc<T, R> _action;
		private Task<R> _previous = Task.FromResult(default(R));
		private readonly object _lock = new object();
		public OrderedAsyncActor(Func<T, R> action)
			: this((t, c) => action.Invoke(t))
		{
		}

		public OrderedAsyncActor(Func<T, CancellationToken, R> action)
			: this((t, c, f, l) => action.Invoke(t, c))
		{
		}

		public OrderedAsyncActor(SetFunc<T, R> action)
		{
			_action = action;
		}

		public Task<R> Push(T value)
		{
			return Push(value, CancellationToken.None);
		}

		public Task<R[]> PushMany(IReadOnlyList<T> values)
		{
			return PushMany(values, CancellationToken.None);
		}

		private int _queueCount;
		public int ScheduledTasksCount => _queueCount;

		public Task<R> Push(T value, CancellationToken token)
		{
			Interlocked.Increment(ref _queueCount);
			//var isFirst = false;
			//var newTask = new Task<R>(() =>
			//{
			//	var count = Interlocked.Decrement(ref _queueCount);
			//	var ret = _action.Invoke(value, token, isFirst, count == 0);
			//	return ret;
			//});

			//var current = _previous;
			//if (Interlocked.CompareExchange(ref _previous, newTask, current) != current)
			//{
			//	var spinner = new SpinWait();
			//	do
			//	{
			//		spinner.SpinOnce();
			//		current = _previous;
			//	}
			//	while (Interlocked.CompareExchange(ref _previous, newTask, current) != current);
			//}
			//isFirst = current.IsCompleted;
			//await current.ConfigureAwait(false);
			//return await newTask.ConfigureAwait(false);

			Task<R> task;
			lock (_lock)
			{
				var isFirst = _previous.IsCompleted;
				task = _previous.ContinueWith(prev =>
				{
					var count = Interlocked.Decrement(ref _queueCount);
					var ret = _action.Invoke(value, token, isFirst, count == 0);
					System.Diagnostics.Debug.WriteLine("First: {0}, Last: {1}", isFirst, count == 0);
					var tret = ret as Task;
					tret?.ConfigureAwait(false).GetAwaiter().GetResult(); // we can't move on until this one is done or we might get out of order
					return ret;

				}, TaskContinuationOptions.PreferFairness);
				_previous = task;
			}
			return task;
		}

		public async Task<R[]> PushMany(IReadOnlyList<T> values, CancellationToken token)
		{
			var results = new List<Task<R>>(values.Count);
			foreach (var value in values)
			{
				if (token.IsCancellationRequested)
					break;
				results.Add(Push(value, token));
			}
			await Task.WhenAll(results);
			return results.Select(r => r.Result).ToArray();
		}

		Task IActor<T>.Push(T value)
		{
			return Push(value);
		}

		Task IActor<T>.PushMany(IReadOnlyList<T> values)
		{
			return PushMany(values);
		}

		Task IActor<T>.Push(T value, CancellationToken token)
		{
			return Push(value, token);
		}

		Task IActor<T>.PushMany(IReadOnlyList<T> values, CancellationToken token)
		{
			return PushMany(values, token);
		}
	}
}
