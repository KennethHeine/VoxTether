using Xunit.Sdk;

namespace VoxTether.Core.Tests.Utilities;

/// <summary>
/// Helper methods for test assertions with clear step-by-step visibility in logs.
/// </summary>
public static class TestAssertions
{
    /// <summary>
    /// Asserts a workflow step with clear output for CI visibility.
    /// </summary>
    /// <param name="stepName">Name of the step being tested.</param>
    /// <param name="assertion">The assertion to execute.</param>
    /// <param name="contextMessage">Optional context message for debugging.</param>
    public static void AssertWorkflowStep(
        string stepName,
        Action assertion,
        string? contextMessage = null)
    {
        try
        {
            assertion();
            Console.WriteLine($"✓ {stepName}");
        }
        catch (XunitException ex)
        {
            var message = $"✗ {stepName} FAILED\n";
            if (contextMessage != null)
            {
                message += $"  Context: {contextMessage}\n";
            }
            message += $"  Error: {ex.Message}";

            Console.WriteLine(message);
            throw new XunitException(message, ex);
        }
    }

    /// <summary>
    /// Asserts that a condition becomes true within the specified timeout.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="description">Description of what is being waited for.</param>
    /// <param name="interval">How often to check the condition.</param>
    public static async Task AssertEventuallyAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        string description,
        TimeSpan? interval = null)
    {
        var checkInterval = interval ?? TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                Console.WriteLine($"✓ {description} (within {timeout.TotalSeconds}s)");
                return;
            }

            await Task.Delay(checkInterval);
        }

        throw new XunitException(
            $"✗ {description} - condition not met within {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Synchronous version of AssertEventuallyAsync.
    /// </summary>
    public static async Task AssertEventuallyAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string description,
        TimeSpan? interval = null)
    {
        await AssertEventuallyAsync(
            () => Task.FromResult(condition()),
            timeout,
            description,
            interval);
    }
}
