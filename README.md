<h1 align='center'>
    <image src='assets/lib_icon.png' width='64' />
    <br/>
    Pulse.NET
    <br/>
    <img alt="GitHub" src="https://img.shields.io/github/license/brandonw3612/Pulse.NET?label=License">
    <img alt="Nuget (with prereleases)" src="https://img.shields.io/nuget/vpre/Pulse.NET?logo=nuget&label=NuGet%20Package&labelColor=004880">
</h1>

## 🌏Overview

Pulse.NET is a .NET library providing enhancements to the workflow management in your .NET applications. We introduce multiple debouncer and throttler implementations, both for different use cases and action types. The library is implemented with thread safety, while also exposing interfaces with great ease to use.

To be more specific, this library helps you improve the performance of your application, by limiting invoking frequency of your workflow when not necessary, while maintaining sufficient reliability thanks to our parameter-handling strategy.

## ⚙️Requirements

This library currently support following frameworks.

* .NET Framework 4.5+
* .NET Standard 1.2+ (with [NETStandard.Library](https://www.nuget.org/packages/NETStandard.Library/))
* .NET Standard 2.0+
* .NET 6.0+

## 🛠️Usage

After adding current library as a reference to your project, you are able to use the utilities we provide. Following instructions may be helpful when you use the library.

### 0. Do I need it?

As we have claimed in the first section of this documentation, this library is created for workflow management tasks. If you require instant invocation of actions in your project, please note that this toolset ***might not*** be for you.

### 1. Debouncer vs. Throttler

Typically, when a series of repeated actions are taken continuously and only the last invocation is valid, a debouncer is recommended. However, if you need to reveal intermediate effects of the actions, you can then choose to create a throttler.

### 2. Parameters Handling

There are 3 types of parameter strategy in this library.

* **No parameter** - The action to invoke contains no parameters, and by sending invoking signals to the debouncer/throttler, no parameters are attached.
* **Latest parameter** - Before the action is actually invoked, for example, we're in the debouncing/throttling period, every parameter brought by your invocation signal sent to the debouncer/throttler will replace the previous ones. Therefore, when the action is finally invoked, the parameters it invokes on are always the latest ones.
* **Cumulative parameter** - Before we invoke the action, we cumulate all input parameters into a batch and invoke the action on the batch. It is up to you to decide the type of the single-invocation parameter and the parameter batch, so if two objects of a specific type can be merged into one, you can more easily create such a debouncer/throttler to apply cumulated effects of multiple invocations while keeping perfect performance.

### 3. Synchronous vs. Asynchronous

Both are supported by our library. You can just simply choose the type you need for the task. For your information, synchronous functions are called actions and asynchronous ones are called tasks in our definition.

## 👋Contribution

Feel free to contribute to this repository by creating an issue or a pull request.