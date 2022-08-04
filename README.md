# .NET Custom SynchronizationContext sample codes.

NOTE: Introduce new library [Lepracaun](https://github.com/kekyo/Lepracaun), it is a successor of SynchContextSample library. It has some fixed problems and easier manipulations.

## What is this?

* How to implements your own custom .NET SynchronizationContext.
* How executes .NET SynchronizationContext by using .NET Task&lt;T&gt;.
* Sample code contains for two implementations:
  * Used Win32 message queue version.
  * Used BlockingCollection version.
  * BlockingCollection is simple version, you can find these code differences.
* These samples are handles for marshaling on class constructed thread.
  * Win32 message queue version likely DispatcherSynchronizationContext/WinFormsSynchronizationContext.

## License

* Copyright (c) 2016 Kouji Matsui (@kekyo2)
* Under Apache v2 http://www.apache.org/licenses/LICENSE-2.0
