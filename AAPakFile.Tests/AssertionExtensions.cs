using System.ComponentModel;

using TUnit.Assertions.Attributes;

namespace AAPakFile;

public static class AssertionExtensions
{
    /// <summary>
    /// Asserts that the actual byte sequence is equal to <paramref name="expected"/>,
    /// element-by-element, without using reflection.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertion(ExpectationMessage = "to be sequence-equal to {expected}")]
    public static bool IsSequenceEqualTo(this byte[] actual, byte[] expected)
        => actual.SequenceEqual(expected);
}