using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.Testing;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    RoslynCustomAnalyzer.EnforceGetComponentLookupParameterAnalyzer>;

// We need two different verifiers, one for each code fix provider.
using AddTrueCodeFixVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    RoslynCustomAnalyzer.EnforceGetComponentLookupParameterAnalyzer,
    RoslynCustomAnalyzer.Tests.Helpers.AddTrueCodeFixProvider>;
    
using AddFalseCodeFixVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    RoslynCustomAnalyzer.EnforceGetComponentLookupParameterAnalyzer,
    RoslynCustomAnalyzer.Tests.Helpers.AddFalseCodeFixProvider>;

namespace RoslynCustomAnalyzer.Tests
{
    public class EnforceGetComponentLookupParameterTests
    {
        // Common code stubs for Unity types to make the test code compile.
        private const string Stubs = @"
            using System;
            namespace Unity.Entities
            {
                public interface IComponentData {}
                public struct Entity {}
                public struct ComponentLookup<T> where T : unmanaged, IComponentData {}
                public static class SystemAPI 
                { 
                    public static ComponentLookup<T> GetComponentLookup<T>() where T : unmanaged, IComponentData => default;
                    public static ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly) where T : unmanaged, IComponentData => default;
                }
            }
            public struct PrimeTarget : Unity.Entities.IComponentData {}
        ";

        [Fact]
        public async Task NoParameter_TriggersWarning()
        {
            var testCode = Stubs + @"
                public partial struct MyJob
                {
                    void Execute()
                    {
                        var lookup = Unity.Entities.SystemAPI.{|#0:GetComponentLookup<PrimeTarget>()|};
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceGetComponentLookupParameterAnalyzer.DiagnosticId)
                .WithLocation(0);

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task WithParameter_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                public partial struct MyJob
                {
                    void Execute()
                    {
                        var lookup1 = Unity.Entities.SystemAPI.GetComponentLookup<PrimeTarget>(true);
                        var lookup2 = Unity.Entities.SystemAPI.GetComponentLookup<PrimeTarget>(false);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task CodeFix_AddsTrueParameter()
        {
            var testCode = Stubs + @"
                public partial struct MyJob
                {
                    void Execute()
                    {
                        var lookup = Unity.Entities.SystemAPI.{|#0:GetComponentLookup<PrimeTarget>()|};
                    }
                }";

            var fixedCode = Stubs + @"
                public partial struct MyJob
                {
                    void Execute()
                    {
                        var lookup = Unity.Entities.SystemAPI.GetComponentLookup<PrimeTarget>(true);
                    }
                }";
            
            var expected = AddTrueCodeFixVerifier.Diagnostic(EnforceGetComponentLookupParameterAnalyzer.DiagnosticId)
                .WithLocation(0);

            await AddTrueCodeFixVerifier.VerifyCodeFixAsync(testCode, expected, fixedCode);
        }
        
        [Fact]
        public async Task CodeFix_AddsFalseParameter()
        {
            var testCode = Stubs + @"
                public partial struct MyJob
                {
                    void Execute()
                    {
                        var lookup = Unity.Entities.SystemAPI.{|#0:GetComponentLookup<PrimeTarget>()|};
                    }
                }";

            var fixedCode = Stubs + @"
                public partial struct MyJob
                {
                    void Execute()
                    {
                        var lookup = Unity.Entities.SystemAPI.GetComponentLookup<PrimeTarget>(false);
                    }
                }";
            
            var expected = AddFalseCodeFixVerifier.Diagnostic(EnforceGetComponentLookupParameterAnalyzer.DiagnosticId)
                .WithLocation(0);

            await AddFalseCodeFixVerifier.VerifyCodeFixAsync(testCode, expected, fixedCode);
        }
    }
}

