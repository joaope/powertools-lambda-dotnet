using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AWS.Lambda.Powertools.Common;
using AWS.Lambda.Powertools.Common.Utils;
using AWS.Lambda.Powertools.Tracing.Internal;
using NSubstitute;
using Xunit;

#if NET8_0_OR_GREATER
using AWS.Lambda.Powertools.Tracing.Serializers;
using AWS.Lambda.Powertools.Tracing.Tests.Serializers;
#endif

namespace AWS.Lambda.Powertools.Tracing.Tests;

[Collection("Sequential")]
public class TracingAspectTests
{
    private readonly IXRayRecorder _mockXRayRecorder;
    private readonly IPowertoolsConfigurations _mockConfigurations;
    private readonly TracingAspect _handler;

    public TracingAspectTests()
    {
        // Setup mocks
        _mockXRayRecorder = Substitute.For<IXRayRecorder>();
        _mockConfigurations = Substitute.For<IPowertoolsConfigurations>();

        // Configure default behavior
        _mockConfigurations.IsLambdaEnvironment.Returns(true);
        _mockConfigurations.TracingDisabled.Returns(false);
        _mockConfigurations.Service.Returns("TestService");
        _mockConfigurations.IsServiceDefined.Returns(true);
        _mockConfigurations.TracerCaptureResponse.Returns(true);
        _mockConfigurations.TracerCaptureError.Returns(true);

        // Setup test handler with mocks
        _handler = new TracingAspect(_mockConfigurations, _mockXRayRecorder);

        // Reset static state
        RuntimeFeatureWrapper.SetIsDynamicCodeSupported(true);
        TracingAspect.ResetForTest();
    }

    [Fact]
    public void Around_SyncMethod_HandlesResponseAndSegmentCorrectly()
    {
        // Arrange
        var attribute = new TracingAttribute();
        var methodName = "TestMethod";
        var result = "test result";
        Func<object[], object> target = _ => result;

        // Act
        _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Assert
        _mockXRayRecorder.Received(1).BeginSubsegment($"## {methodName}");
        _mockXRayRecorder.Received(1).AddAnnotation("ColdStart", true);
        _mockXRayRecorder.Received(1).AddAnnotation("Service", "TestService");
        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Any<string>(),
            $"{methodName} response",
            result);
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

    [Fact]
    public async Task Around_AsyncMethod_HandlesResponseAndSegmentCorrectly()
    {
        // Arrange
        var attribute = new TracingAttribute();
        var methodName = "TestAsyncMethod";
        var result = new TestResponse { Message = "test async result" };
        Func<object[], object> target = _ => Task.FromResult<object>(result);

        // Act
        var taskResult = _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Wait for the async operation to complete
        if (taskResult is Task task)
        {
            await task;
        }

        // Assert with wait
        await Task.Delay(100); // Give time for the continuation to complete

        _mockXRayRecorder.Received(1).BeginSubsegment($"## {methodName}");
        _mockXRayRecorder.Received(1).AddAnnotation("ColdStart", true);
        _mockXRayRecorder.Received(1).AddAnnotation("Service", "TestService");
        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Any<string>(),
            $"{methodName} response",
            result);
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Around_SyncMethod_HandlesResponseAndSegmentCorrectly_AOT()
    {
        // Arrange
        RuntimeFeatureWrapper.SetIsDynamicCodeSupported(false);

        var context = new TestJsonContext(new JsonSerializerOptions());
        PowertoolsTracingSerializer.AddSerializerContext(context);

        var attribute = new TracingAttribute();
        var methodName = "TestMethod";
        var result = "test result";
        Func<object[], object> target = _ => result;

        // Act
        _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Assert
        _mockXRayRecorder.Received(1).BeginSubsegment($"## {methodName}");
        _mockXRayRecorder.Received(1).AddAnnotation("ColdStart", true);
        _mockXRayRecorder.Received(1).AddAnnotation("Service", "TestService");
        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Any<string>(),
            $"{methodName} response",
            PowertoolsTracingSerializer.Serialize(result));
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

    [Fact]
    public async Task Around_AsyncMethod_HandlesResponseAndSegmentCorrectly_AOT()
    {
        // Arrange
        RuntimeFeatureWrapper.SetIsDynamicCodeSupported(false);

        var context = new TestJsonContext(new JsonSerializerOptions());
        PowertoolsTracingSerializer.AddSerializerContext(context);

        var attribute = new TracingAttribute();
        var methodName = "TestAsyncMethod";
        var result = new TestResponse { Message = "test async result" };
        Func<object[], object> target = _ => Task.FromResult<object>(result);

        // Act
        var taskResult = _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Wait for the async operation to complete
        if (taskResult is Task task)
        {
            await task;
        }

        // Assert with wait
        await Task.Delay(100); // Give time for the continuation to complete

        _mockXRayRecorder.Received(1).BeginSubsegment($"## {methodName}");
        _mockXRayRecorder.Received(1).AddAnnotation("ColdStart", true);
        _mockXRayRecorder.Received(1).AddAnnotation("Service", "TestService");
        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Any<string>(),
            $"{methodName} response",
            PowertoolsTracingSerializer.Serialize(result));
        _mockXRayRecorder.Received(1).EndSubsegment();
    }
#endif

    [Fact]
    public async Task Around_VoidAsyncMethod_HandlesSegmentCorrectly()
    {
        // Arrange
        var attribute = new TracingAttribute();
        var methodName = "TestVoidAsyncMethod";
        Func<object[], object> target = _ => Task.CompletedTask;

        // Act
        var taskResult = _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Wait for the async operation to complete
        if (taskResult is Task task)
        {
            await task;
        }

        // Assert with wait
        await Task.Delay(100); // Give time for the continuation to complete

        _mockXRayRecorder.Received(1).BeginSubsegment($"## {methodName}");
        _mockXRayRecorder.Received(1).AddAnnotation("ColdStart", true);
        _mockXRayRecorder.Received(1).AddAnnotation("Service", "TestService");
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

    [Fact]
    public async Task Around_AsyncMethodWithException_HandlesErrorCorrectly()
    {
        // Arrange
        var attribute = new TracingAttribute();
        var methodName = "TestExceptionAsyncMethod";
        var expectedException = new Exception("Test exception");
        Func<object[], object> target = _ => Task.FromException(expectedException);

        // Act & Assert
        var taskResult = _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Wait for the async operation to complete
        if (taskResult is Task task)
        {
            await Assert.ThrowsAsync<Exception>(() => task);
        }

        // Assert with wait
        await Task.Delay(100); // Give time for the continuation to complete

        _mockXRayRecorder.Received(1).BeginSubsegment($"## {methodName}");
        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Any<string>(),
            $"{methodName} error",
            Arg.Is<string>(s => s.Contains(expectedException.Message)));
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

    [Fact]
    public void Around_TracingDisabled_DoesNotCreateSegment()
    {
        // Arrange
        _mockConfigurations.TracingDisabled.Returns(true);
        var attribute = new TracingAttribute();
        var methodName = "TestMethod";
        Func<object[], object> target = _ => "result";

        // Act
        _handler.Around(methodName, Array.Empty<object>(), target, new Attribute[] { attribute });

        // Assert
        _mockXRayRecorder.DidNotReceive().BeginSubsegment(Arg.Any<string>());
        _mockXRayRecorder.DidNotReceive().EndSubsegment();
    }

    [Fact]
    public void TracingDisabled_WhenTracingDisabledTrue_ReturnsTrue()
    {
        // Arrange
        _mockConfigurations.TracingDisabled.Returns(true);

        // Act
        var result = _handler.Around(
            "TestMethod",
            Array.Empty<object>(),
            _ => null,
            new Attribute[] { new TracingAttribute() }
        );

        // Assert
        Assert.Null(result);
        _mockXRayRecorder.DidNotReceive().BeginSubsegment(Arg.Any<string>());
    }

    [Fact]
    public void TracingDisabled_WhenNotInLambdaEnvironment_ReturnsTrue()
    {
        // Arrange
        _mockConfigurations.IsLambdaEnvironment.Returns(false);

        // Act
        var result = _handler.Around(
            "TestMethod",
            Array.Empty<object>(),
            _ => null,
            new Attribute[] { new TracingAttribute() }
        );

        // Assert
        Assert.Null(result);
        _mockXRayRecorder.DidNotReceive().BeginSubsegment(Arg.Any<string>());
    }

    [Fact]
    public async Task WrapVoidTask_SuccessfulExecution_HandlesResponseAndEndsSubsegment()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var task = tcs.Task;
        const string methodName = "TestMethod";
        const string nameSpace = "TestNamespace";

        // Act
        var wrappedTask = _handler.Around(
            methodName,
            new object[] { task },
            args => args[0],
            new Attribute[]
                { new TracingAttribute { Namespace = nameSpace, CaptureMode = TracingCaptureMode.Response } }
        ) as Task;

        tcs.SetResult(); // Complete the task
        await wrappedTask!;

        // Assert
        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Is(nameSpace),
            Arg.Is($"{methodName} response"),
            Arg.Any<object>()
        );
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

    [Fact]
    public async Task WrapVoidTask_WithException_HandlesExceptionAndEndsSubsegment()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var task = tcs.Task;
        const string methodName = "TestMethod";
        const string nameSpace = "TestNamespace";
        var expectedException = new Exception("Test exception");

        // Act
        var wrappedTask = _handler.Around(
            methodName,
            new object[] { task },
            args => args[0],
            new Attribute[]
                { new TracingAttribute { Namespace = nameSpace, CaptureMode = TracingCaptureMode.ResponseAndError } }
        ) as Task;

        tcs.SetException(expectedException); // Fail the task

        // Assert
        await Assert.ThrowsAsync<Exception>(() => wrappedTask!);

        _mockXRayRecorder.Received(1).AddMetadata(
            Arg.Is(nameSpace),
            Arg.Is($"{methodName} error"),
            Arg.Is<string>(s => s.Contains("Test exception"))
        );
        _mockXRayRecorder.Received(1).EndSubsegment();
    }

    [Fact]
    public async Task WrapVoidTask_WithCancellation_EndsSubsegment()
    {
        // Arrange
        TracingAspect.ResetForTest(); // Ensure static state is reset

        // Reinitialize mocks for this specific test
        var mockXRayRecorder = Substitute.For<IXRayRecorder>();
        var mockConfigurations = Substitute.For<IPowertoolsConfigurations>();

        // Configure all required behavior
        mockConfigurations.IsLambdaEnvironment.Returns(true);
        mockConfigurations.TracingDisabled.Returns(false);
        mockConfigurations.Service.Returns("TestService");
        mockConfigurations.IsServiceDefined.Returns(true);
        mockConfigurations.TracerCaptureResponse.Returns(true);
        mockConfigurations.TracerCaptureError.Returns(true);

        var handler = new TracingAspect(mockConfigurations, mockXRayRecorder);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();
        var task = Task.Run(async () =>
        {
            await tcs.Task;
            await Task.Delay(Timeout.Infinite, cts.Token);
        });

        const string methodName = "TestMethod";
        const string nameSpace = "TestNamespace";

        // Act
        var wrappedTask = handler.Around(
            methodName,
            new object[] { task },
            args => args[0],
            new Attribute[]
                { new TracingAttribute { Namespace = nameSpace, CaptureMode = TracingCaptureMode.ResponseAndError } }
        ) as Task;

        // Ensure the task is running before we cancel
        tcs.SetResult();
        await Task.Delay(100); // Give time for the task to start running

        // Cancel the task
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => wrappedTask!);

        // Add small delay before verification to ensure all async operations complete
        await Task.Delay(50);

        mockXRayRecorder.Received(1).EndSubsegment();

        // Verify other expected calls
        mockXRayRecorder.Received(1).BeginSubsegment(Arg.Any<string>());
        mockXRayRecorder.Received(1).SetNamespace(nameSpace);
        mockXRayRecorder.Received(1).AddAnnotation("ColdStart", true);
        mockXRayRecorder.Received(1).AddAnnotation("Service", "TestService");
    }
}