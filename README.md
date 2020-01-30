# Kts.ActorsLite
This is a small library that supports various types of asynchronous methods in .NET. It exists in part because I was dissatisfied with the functionality available in the ActionBlock class that comes with the TPL Dataflow library.

This library uses the term "actor" because of Ruby; more specifically, Bernhardt's video [Boundaries](https://www.youtube.com/watch?v=yTkzNHF6rMs). It's more like "workers", though. They are queues of functions/methods to be executed.

# Library Worker Types:

**Most Recent:** finish executing the current method and then skip to the method most recently pushed into the queue. This feature is not available in TPL Dataflow (that I can see).

**Ordered:** run all methods pushed on to the queue in order.

**Periodic:** run all methods in order but only process them every so often.

**Unordered:** pass the method straight onto the thread pool. Execution order is not guaranteed.

**Sync:** in the name of the class means execute it on the thread that calls it (immediately).

# Example:
```csharp
class ExampleClass
{
	private struct CriticalParams
	{
		public int Key;
		public string Value;
	}

	public ExampleClass()
	{
		_criticalMethod = new MostRecentAsyncActor<CriticalParams>(cp => 
		{
			Console.WriteLine(cp.Key + " = " + cp.Value);
		});
	}

	private readonly MostRecentAsyncActor<CriticalParams> _criticalMethod;

	public async void PrintAndSkipSomeIfTheyComeTooFast(int key, string value)
	{
		await _criticalMethod.Push(new CriticalParams { Key = key, Value = value });
	}
}
```
What I want is the ability to do some aspect-oriented programming with this library. I'd like to decorate a method with something like `[MostRecentWorker]` and have it insert my actor automatically. This can be done with PostSharp presently. I'll keep an eye on Roslyn's AOP abilities as well.

# Schedulers:
The library includes TaskScheduler implementaitions to match the actors. They are usefull for resolving situtions where `Task.Run` gives you out-of-order execution problems.

