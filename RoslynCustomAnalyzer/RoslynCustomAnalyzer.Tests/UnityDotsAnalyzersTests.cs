using System.Threading.Tasks;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<RoslynCustomAnalyzer.ReadOnlyComponentAnalyzer>;
using CodeFixVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<RoslynCustomAnalyzer.ReadOnlyComponentAnalyzer, RoslynCustomAnalyzer.UnityDotsAnalyzersCodeFixProvider>;

namespace RoslynCustomAnalyzer.Tests
{
    public class UnityDotsAnalyzersTests
    {
        // A helper constant containing minimal stubs for Unity types
        // to allow the test source code to compile.
        private const string UnityStubs = @"
namespace Unity.Collections { public class ReadOnlyAttribute : System.Attribute {} }
namespace Unity.Entities
{
    public interface IComponentData {}
    public struct Entity {}
    public struct ComponentLookup<T> where T : unmanaged, IComponentData
    {
        public bool TryGetComponent(Entity entity, out T component) { component = default; return true; }
    }
    public partial interface IJobEntity {}
    public static class SystemAPI { public static ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly) where T : unmanaged, IComponentData => default; }
    namespace ECB
    {
        public struct ParallelWriter
        {
            public void SetComponent<T>(int index, Entity entity, T component) where T : struct, IComponentData {}
        }
    }
    public struct EntityCommandBuffer { public struct ParallelWriter { public void SetComponent<T>(int index, Entity entity, T component) where T : struct, IComponentData {} }}
}
";

        // Place using statements at the top to ensure the test code compiles correctly.
        private const string Usings = @"
using Unity.Entities;
using Unity.Collections;
";

        [Fact]
        public async Task ReadOnlyComponent_TriggersWarning()
        {
            var testCode = Usings + UnityStubs + @"
public struct PrimeTarget : IComponentData { public double LastSeenTime; }

public partial struct MyJob
{
    [ReadOnly] public ComponentLookup<PrimeTarget> PrimeTargetLookup;
    public double ElapsedTime;

    void Execute(Entity entity)
    {
        if (PrimeTargetLookup.TryGetComponent(entity, out var target))
        {
            // The analyzer should create a warning on the following line
            {|#0:target.LastSeenTime = ElapsedTime|};
        }
    }
}
";
            // We expect a warning (Diagnostic) at the location marked by {|#0:...|}
            var expected = AnalyzerVerifier.Diagnostic("UnityRedDots001")
                .WithLocation(0)
                .WithArguments("target"); // The argument should be the name of the variable

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }


        [Fact]
        public async Task ReadOnlyComponent_AppliesFix()
        {
            var testCode = Usings + UnityStubs + @"
public struct PrimeTarget : IComponentData { public double LastSeenTime; }

public partial struct MyJob
{
    [ReadOnly] public ComponentLookup<PrimeTarget> PrimeTargetLookup;
    public EntityCommandBuffer.ParallelWriter Ecb;
    public double ElapsedTime;

    void Execute(int index, Entity entity)
    {
        if (PrimeTargetLookup.TryGetComponent(entity, out var target))
        {
            {|#0:target.LastSeenTime = ElapsedTime|};
        }
    }
}
";

            var fixedCode = Usings + UnityStubs + @"
public struct PrimeTarget : IComponentData { public double LastSeenTime; }

public partial struct MyJob
{
    [ReadOnly] public ComponentLookup<PrimeTarget> PrimeTargetLookup;
    public EntityCommandBuffer.ParallelWriter Ecb;
    public double ElapsedTime;

    void Execute(int index, Entity entity)
    {
        if (PrimeTargetLookup.TryGetComponent(entity, out var target))
        {
            target.LastSeenTime = ElapsedTime;
            Ecb.SetComponent(index, entity, target);
        }
    }
}
";
            var expected = CodeFixVerifier.Diagnostic("UnityRedDots001")
                .WithLocation(0)
                .WithArguments("target");

            await CodeFixVerifier.VerifyCodeFixAsync(testCode, expected, fixedCode);
        }
    }
}

